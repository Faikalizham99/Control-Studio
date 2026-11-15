using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class PhotoCropEditor
{
    public static async Task<string?> EditAsync(UIWindow owner, Func<bool> canPresent, Action didPresent, UIImage image,
        int width, int height, PhotoCropRect? initial, Color accent, Func<PhotoCropRect, CancellationToken, Task<string>> save)
    {
        try
        {
            var presenter = await PhotoCutoutEditor.WaitForPresenterAsync(owner, canPresent, "Crop");
            if (presenter is null) return null;
            using var editor = new CropController(image, new(width, height, initial), accent, save)
                { ModalPresentationStyle = UIModalPresentationStyle.FullScreen, ModalInPresentation = true };
            try
            {
                await presenter.PresentViewControllerAsync(editor, true);
                if (!canPresent()) return null;
                editor.VerifyLayout(); didPresent();
                return await editor.Completion.Task;
            }
            finally
            {
                await editor.Work;
                if (editor.PresentingViewController is not null)
                    await editor.DismissViewControllerAsync(UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active);
            }
        }
        catch (Exception ex) when (ex is not IOException)
        { throw new IOException("The crop screen could not open. Your photo is unchanged. Tap Crop to try again.", ex); }
    }
    private sealed class CropController(UIImage image, PhotoCropSession session, Color accent,
        Func<PhotoCropRect, CancellationToken, Task<string>> save) : UIViewController
    {
        private readonly PhotoCutoutEditor.CutoutPageScrollView page = new() { AlwaysBounceVertical = true };
        private readonly UIStackView stack = new() { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12 };
        private readonly UIScrollView zoom = new() { BouncesZoom = false, ClipsToBounds = true,
            ShowsVerticalScrollIndicator = false, ShowsHorizontalScrollIndicator = false,
            ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
        private readonly CropCanvas canvas = new(image, session, accent);
        private readonly UIButton cancel = Button("Cancel", "CancelPhotoCrop"), apply = Button("Apply", "ApplyPhotoCrop"),
            undo = Button("Undo", "UndoPhotoCrop"), redo = Button("Redo", "RedoPhotoCrop"), reset = Button("Reset crop", "ResetPhotoCrop");
        private readonly UISegmentedControl aspects = new(["Free", "Original", "1:1", "4:3", "16:9"]);
        private readonly UISlider magnification = new() { MinValue = 1, MaxValue = 8, Value = 1, AccessibilityLabel = "Crop preview zoom" };
        private readonly UILabel status = Label("");
        private readonly UIActivityIndicatorView progress = new(UIActivityIndicatorViewStyle.Medium) { HidesWhenStopped = true, AccessibilityLabel = "Saving crop" };
        private UIPanGestureRecognizer? drag;
        private PhotoCropHandle? handle;
        private NSLayoutConstraint? previewHeight;
        private NSObject? background, activation;
        private CancellationTokenSource? cancellation;
        private CGSize viewport;
        private nfloat fit;
        private bool busy, closing, dismissed;
        public Task Work { get; private set; } = Task.CompletedTask;
        public TaskCompletionSource<string?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override void ViewDidLoad()
        {
            base.ViewDidLoad(); ModalInPresentation = true; View!.BackgroundColor = UIColor.SystemBackground;
            View.AddSubview(page); page.AddSubview(stack); page.TranslatesAutoresizingMaskIntoConstraints = stack.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints([
                page.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor), page.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
                page.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor), page.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
                stack.LeadingAnchor.ConstraintEqualTo(page.ContentLayoutGuide.LeadingAnchor, 16), stack.TrailingAnchor.ConstraintEqualTo(page.ContentLayoutGuide.TrailingAnchor, -16),
                stack.TopAnchor.ConstraintEqualTo(page.ContentLayoutGuide.TopAnchor, 8), stack.BottomAnchor.ConstraintEqualTo(page.ContentLayoutGuide.BottomAnchor, -20),
                stack.WidthAnchor.ConstraintEqualTo(page.FrameLayoutGuide.WidthAnchor, multiplier: 1, constant: -32)]);
            var title = Label("Crop photo"); title.TextAlignment = UITextAlignment.Center; title.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline)!;
            stack.AddArrangedSubview(Row(cancel, title, apply));
            stack.AddArrangedSubview(Label("Drag an edge, corner or the selection. Pinch to zoom; move the view with two fingers. Layer rotation stays unchanged."));
            zoom.AddSubview(canvas); zoom.PanGestureRecognizer.MinimumNumberOfTouches = 2; page.Canvas = zoom;
            zoom.ViewForZoomingInScrollView = _ => canvas; zoom.DidZoom += Zoomed;
            zoom.Layer.CornerRadius = 16; zoom.Layer.BorderWidth = 1; stack.AddArrangedSubview(zoom);
            previewHeight = zoom.HeightAnchor.ConstraintEqualTo(300); previewHeight.Active = true;
            drag = new UIPanGestureRecognizer(Drag) { MaximumNumberOfTouches = 1 };
            drag.ShouldBegin = _ =>
            {
                if (!Editable) return false;
                var point = drag.LocationInView(canvas); var delta = drag.TranslationInView(canvas);
                // A pan starts after moving past UIKit's threshold. Hit-test
                // where the finger began so a fast corner drag keeps its handle.
                handle = canvas.Hit(new CGPoint(point.X - delta.X, point.Y - delta.Y), 22 / Math.Max(.0001, (double)zoom.ZoomScale));
                if (handle is null) return false;
                session.Begin(); Update(); return true;
            };
            zoom.AddGestureRecognizer(drag);
            aspects.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true; aspects.AccessibilityLabel = "Crop aspect ratio";
            aspects.ValueChanged += AspectChanged; stack.AddArrangedSubview(aspects);
            stack.AddArrangedSubview(Row(undo, redo, reset)); stack.AddArrangedSubview(Label("Zoom"));
            magnification.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true; magnification.ValueChanged += ZoomChanged;
            stack.AddArrangedSubview(magnification); stack.AddArrangedSubview(progress); stack.AddArrangedSubview(status);
            stack.AddArrangedSubview(Label("The full photo and your edits are kept. Apply fits the crop inside this layer's existing frame. Save widget publishes it to your Home Screen."));
            cancel.TouchUpInside += Cancel; apply.TouchUpInside += Apply; undo.TouchUpInside += Undo; redo.TouchUpInside += Redo; reset.TouchUpInside += Reset;
            canvas.IsAccessibilityElement = true; canvas.AccessibilityLabel = "Photo crop selection";
            canvas.AccessibilityHint = "Use actions to move or resize the selection, or use the aspect ratio controls.";
            canvas.AccessibilityCustomActions = [Action("Move crop left", () => Accessible(PhotoCropHandle.Move, -1, 0)),
                Action("Move crop right", () => Accessible(PhotoCropHandle.Move, 1, 0)), Action("Move crop up", () => Accessible(PhotoCropHandle.Move, 0, -1)),
                Action("Move crop down", () => Accessible(PhotoCropHandle.Move, 0, 1)), Action("Make crop smaller", () => Accessible(PhotoCropHandle.BottomRight, -Math.Max(1, session.Rect.Width * .05), -Math.Max(1, session.Rect.Height * .05))),
                Action("Make crop larger", () => Accessible(PhotoCropHandle.BottomRight, Math.Max(1, session.Rect.Width * .05), Math.Max(1, session.Rect.Height * .05)))];
            background = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillResignActiveNotification, _ =>
            { EndDrag(true); if (busy) { closing = true; cancellation?.Cancel(); Update(); status.Text = "Closing crop editor…"; } });
            activation = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification, _ =>
            {
                if (dismissed || closing) return;
                // Enabled states were last calculated while inactive. Restore
                // availability explicitly; foregrounding need not relayout the view.
                var message = status.Text; Update(); status.Text = message;
            });
            ModalPresentationStyle = UIModalPresentationStyle.FullScreen; Update();
        }
        public void VerifyLayout()
        {
            View!.LayoutIfNeeded(); stack.LayoutIfNeeded();
            if (Math.Abs((double)(stack.Bounds.Width - page.Bounds.Width + 32)) > 1 || zoom.Bounds.Width <= 0 || canvas.Bounds.Width <= 0
                || new[] { cancel, apply, reset }.Any(x => x.Bounds.Width < 44 || x.Bounds.Height < 44))
                throw new IOException("The crop screen could not fit this display. Your photo is unchanged.");
        }
        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            previewHeight!.Constant = (nfloat)Math.Clamp((double)(View!.Bounds.Height - View.SafeAreaInsets.Top - View.SafeAreaInsets.Bottom) * .43, 180, 440);
            var dark = TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
            View.TintColor = AppearanceColors.TextAccent(accent, dark).ToPlatform(); zoom.Layer.BorderColor = UIColor.Separator.CGColor;
            apply.BackgroundColor = accent.ToPlatform(); apply.SetTitleColor(AppearanceColors.ButtonForeground(accent).ToPlatform(), UIControlState.Normal);
            aspects.SelectedSegmentTintColor = accent.ToPlatform();
            aspects.SetTitleTextAttributes(new UIStringAttributes { ForegroundColor = AppearanceColors.ButtonForeground(accent).ToPlatform() }, UIControlState.Selected);
            if (zoom.Bounds.Width > 0 && zoom.Bounds.Height > 0 && viewport != zoom.Bounds.Size)
            {
                EndDrag(true); viewport = zoom.Bounds.Size;
                zoom.MinimumZoomScale = .0001f; zoom.MaximumZoomScale = 16; zoom.SetZoomScale(1, false);
                canvas.Frame = new CGRect(0, 0, session.Width, session.Height); zoom.ContentSize = canvas.Frame.Size;
                // A halo keeps the entire edge/corner touch target reachable.
                fit = (nfloat)Math.Min(Math.Max(1, viewport.Width - 48) / session.Width, Math.Max(1, viewport.Height - 48) / session.Height);
                zoom.MaximumZoomScale = fit * 8; zoom.MinimumZoomScale = fit; zoom.SetZoomScale(fit, false);
                Center(); zoom.ContentOffset = new CGPoint(-zoom.ContentInset.Left, -zoom.ContentInset.Top); magnification.Value = 1;
            }
            canvas.SetNeedsDisplay();
        }
        private bool Editable => !busy && !closing && !dismissed && UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active;
        private void Drag(UIPanGestureRecognizer recognizer)
        {
            if (recognizer.State == UIGestureRecognizerState.Changed && recognizer.NumberOfTouches != 1) { EndDrag(true); return; }
            if (recognizer.State == UIGestureRecognizerState.Changed && Editable && handle is { } selected)
            { var delta = recognizer.TranslationInView(canvas); session.Drag(selected, delta.X, delta.Y); canvas.SetNeedsDisplay(); Describe(); }
            if (recognizer.State == UIGestureRecognizerState.Ended)
            {
                if (Editable && handle is { } final) { var delta = recognizer.TranslationInView(canvas); session.Drag(final, delta.X, delta.Y); }
                EndDrag(false);
            }
            else if (recognizer.State is UIGestureRecognizerState.Failed or UIGestureRecognizerState.Cancelled) EndDrag(true);
        }
        private void EndDrag(bool cancel) { handle = null; session.End(cancel); if (IsViewLoaded) Update(); }
        private void Center() => zoom.ContentInset = new UIEdgeInsets((nfloat)Math.Max(0, (zoom.Bounds.Height - zoom.ContentSize.Height) / 2),
            (nfloat)Math.Max(0, (zoom.Bounds.Width - zoom.ContentSize.Width) / 2), 0, 0);
        private void Zoomed(object? sender, EventArgs e) { EndDrag(true); Center(); canvas.Scale = zoom.ZoomScale; magnification.Value = fit > 0 ? (float)(zoom.ZoomScale / fit) : 1; canvas.SetNeedsDisplay(); }
        private void ZoomChanged(object? sender, EventArgs e)
        {
            if (fit <= 0) return;
            var x = (zoom.ContentOffset.X + zoom.Bounds.Width / 2) / zoom.ZoomScale; var y = (zoom.ContentOffset.Y + zoom.Bounds.Height / 2) / zoom.ZoomScale;
            zoom.SetZoomScale(fit * magnification.Value, false); Center();
            zoom.ContentOffset = new CGPoint(Math.Clamp((double)(x * zoom.ZoomScale - zoom.Bounds.Width / 2), -(double)zoom.ContentInset.Left, Math.Max(-(double)zoom.ContentInset.Left, (double)(zoom.ContentSize.Width - zoom.Bounds.Width))),
                Math.Clamp((double)(y * zoom.ZoomScale - zoom.Bounds.Height / 2), -(double)zoom.ContentInset.Top, Math.Max(-(double)zoom.ContentInset.Top, (double)(zoom.ContentSize.Height - zoom.Bounds.Height))));
        }
        private void AspectChanged(object? sender, EventArgs e) { if (!Editable || session.IsInteracting) return; session.SetAspect((PhotoCropAspect)(int)aspects.SelectedSegment); Update(); }
        private void Undo(object? sender, EventArgs e) { if (!Editable) return; session.Undo(); Update(); }
        private void Redo(object? sender, EventArgs e) { if (!Editable) return; session.Redo(); Update(); }
        private void Reset(object? sender, EventArgs e) { if (!Editable) return; session.Reset(); Update(); }
        private bool Accessible(PhotoCropHandle part, double dx, double dy)
        {
            if (!Editable || session.IsInteracting) return false;
            session.Begin(); session.Drag(part, dx, dy); session.End(); Update();
            UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, new NSString(status.Text ?? "Crop updated")); return true;
        }
        private void Describe() { var r = session.Rect; status.Text = $"{r.Width} × {r.Height} pixels · Left {r.X}, Top {r.Y}"; canvas.AccessibilityValue = status.Text; }
        private void Message(string text)
        {
            status.Text = text;
            if (View?.Window is not null)
            { using var announcement = new NSString(text); UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, announcement); }
        }
        private void Update()
        {
            var edit = Editable && !session.IsInteracting;
            cancel.Enabled = !dismissed && !closing; apply.Enabled = edit; aspects.Enabled = reset.Enabled = edit;
            undo.Enabled = edit && session.CanUndo; redo.Enabled = edit && session.CanRedo;
            magnification.Enabled = zoom.UserInteractionEnabled = !busy && !dismissed && !closing;
            aspects.SelectedSegment = (int)session.Aspect;
            if (busy) progress.StartAnimating(); else progress.StopAnimating(); Describe(); canvas.SetNeedsDisplay();
        }
        private void Cancel(object? sender, EventArgs e)
        {
            if (dismissed || closing) return; EndDrag(true);
            if (busy) { closing = true; cancellation?.Cancel(); Update(); Message("Finishing before closing…"); }
            else Complete(null);
        }
        private void Apply(object? sender, EventArgs e) { if (Editable && !session.IsInteracting) Work = SaveAsync(); }
        private async Task SaveAsync()
        {
            busy = true; Update(); Message("Saving crop…"); using var token = new CancellationTokenSource(); cancellation = token;
            try { var key = await save(session.Rect, token.Token); if (!closing && !dismissed) Complete(key); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (!closing && !dismissed) Message(ex is IOException ? ex.Message : "The crop could not be saved. Your selection is kept; try Apply again."); }
            finally { cancellation = null; busy = false; if (closing) Complete(null); else if (!dismissed) { var message = status.Text; Update(); status.Text = message; } }
        }
        private void Complete(string? value) { if (dismissed) return; dismissed = true; Update(); Completion.TrySetResult(value); }
        public override void ViewDidDisappear(bool animated)
        { base.ViewDidDisappear(animated); EndDrag(true); if (PresentingViewController is null) { closing = true; cancellation?.Cancel(); Complete(null); } }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                background?.Dispose(); background = null; activation?.Dispose(); activation = null;
                page.Canvas = null; zoom.ViewForZoomingInScrollView = null; zoom.DidZoom -= Zoomed;
                cancel.TouchUpInside -= Cancel; apply.TouchUpInside -= Apply; undo.TouchUpInside -= Undo; redo.TouchUpInside -= Redo; reset.TouchUpInside -= Reset;
                aspects.ValueChanged -= AspectChanged; magnification.ValueChanged -= ZoomChanged;
                if (drag is not null) { zoom.RemoveGestureRecognizer(drag); drag.Dispose(); drag = null; }
                if (canvas.AccessibilityCustomActions is { } actions) { canvas.AccessibilityCustomActions = null; foreach (var action in actions) action.Dispose(); }
                canvas.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    private sealed class CropCanvas(UIImage image, PhotoCropSession session, Color accent) : UIView
    {
        public nfloat Scale { get; set; } = 1;
        private UIImage? pattern;
        private UIUserInterfaceStyle patternStyle;
        private (PhotoCropHandle Handle, CGPoint Point)[] Handles()
        {
            var r = session.Rect; double l = r.X, t = r.Y, w = r.Width, h = r.Height;
            return [(PhotoCropHandle.TopLeft, new(l, t)), (PhotoCropHandle.Top, new(l + w / 2, t)), (PhotoCropHandle.TopRight, new(l + w, t)),
                (PhotoCropHandle.Right, new(l + w, t + h / 2)), (PhotoCropHandle.BottomRight, new(l + w, t + h)),
                (PhotoCropHandle.Bottom, new(l + w / 2, t + h)), (PhotoCropHandle.BottomLeft, new(l, t + h)), (PhotoCropHandle.Left, new(l, t + h / 2))];
        }
        public PhotoCropHandle? Hit(CGPoint point, double radius)
        {
            var hit = Handles().Select(x => (x.Handle, Distance: Math.Pow(x.Point.X - point.X, 2) + Math.Pow(x.Point.Y - point.Y, 2))).OrderBy(x => x.Distance).First();
            if (hit.Distance <= radius * radius) return hit.Handle;
            var r = session.Rect; return new CGRect(r.X, r.Y, r.Width, r.Height).Contains(point) ? PhotoCropHandle.Move : null;
        }
        public override void Draw(CGRect rect)
        {
            using var context = UIGraphics.GetCurrentContext(); if (context is null) return;
            if (pattern is null || patternStyle != TraitCollection.UserInterfaceStyle)
            {
                pattern?.Dispose(); patternStyle = TraitCollection.UserInterfaceStyle;
                using var format = UIGraphicsImageRendererFormat.DefaultFormat; format.Scale = 1; format.Opaque = true;
                using var renderer = new UIGraphicsImageRenderer(new CGSize(24, 24), format);
                pattern = renderer.CreateImage(ctx => { UIColor.SystemGray5.SetFill(); ctx.FillRect(new CGRect(0, 0, 24, 24)); UIColor.SystemGray4.SetFill(); ctx.FillRect(new CGRect(0, 0, 12, 12)); ctx.FillRect(new CGRect(12, 12, 12, 12)); });
            }
            using var checker = UIColor.FromPatternImage(pattern); checker.SetFill(); context.FillRect(Bounds); image.Draw(Bounds);
            var r = session.Rect; using var shade = UIColor.Black.ColorWithAlpha(.55f); context.SetFillColor(shade.CGColor);
            context.FillRect(new CGRect(0, 0, Bounds.Width, r.Y)); context.FillRect(new CGRect(0, r.Y + r.Height, Bounds.Width, Bounds.Height - r.Y - r.Height));
            context.FillRect(new CGRect(0, r.Y, r.X, r.Height)); context.FillRect(new CGRect(r.X + r.Width, r.Y, Bounds.Width - r.X - r.Width, r.Height));
            var scale = Math.Max(.0001, (double)Scale); context.SetStrokeColor(UIColor.White.CGColor); context.SetLineWidth((nfloat)(3 / scale)); context.StrokeRect(new CGRect(r.X, r.Y, r.Width, r.Height));
            context.SetStrokeColor(accent.ToPlatform().CGColor); context.SetLineWidth((nfloat)(1.5 / scale)); context.StrokeRect(new CGRect(r.X, r.Y, r.Width, r.Height));
            using var grid = UIColor.White.ColorWithAlpha(.6f); context.SetStrokeColor(grid.CGColor); context.SetLineWidth((nfloat)(.5 / scale));
            for (var n = 1; n < 3; n++)
            {
                var gx = (nfloat)(r.X + r.Width * n / 3d); var gy = (nfloat)(r.Y + r.Height * n / 3d);
                context.MoveTo(gx, r.Y); context.AddLineToPoint(gx, r.Y + r.Height);
                context.MoveTo(r.X, gy); context.AddLineToPoint(r.X + r.Width, gy);
            }
            context.StrokePath();
            foreach (var (_, p) in Handles())
            { var radius = 4 / scale; context.SetFillColor(UIColor.White.CGColor); context.FillEllipseInRect(new CGRect(p.X - radius, p.Y - radius, radius * 2, radius * 2)); }
        }
        protected override void Dispose(bool disposing) { if (disposing) { pattern?.Dispose(); pattern = null; } base.Dispose(disposing); }
    }
    private static UILabel Label(string text) => new() { Text = text, Lines = 0, TextColor = UIColor.Label,
        Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Subheadline)!, AdjustsFontForContentSizeCategory = true };
    private static UIButton Button(string text, string id)
    {
        var button = new UIButton(UIButtonType.System) { AccessibilityIdentifier = id }; button.SetTitle(text, UIControlState.Normal);
        button.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body)!; button.TitleLabel.Lines = 0;
        button.TitleLabel.LineBreakMode = UILineBreakMode.WordWrap; button.TitleLabel.TextAlignment = UITextAlignment.Center;
        button.TitleLabel.AdjustsFontForContentSizeCategory = true; button.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true; button.Layer.CornerRadius = 14; return button;
    }
    private static UIStackView Row(params UIView[] views) => new(views) { Axis = UILayoutConstraintAxis.Horizontal, Distribution = UIStackViewDistribution.FillEqually, Spacing = 8 };
    private static UIAccessibilityCustomAction Action(string text, Func<bool> action) => new(text, new UIAccessibilityCustomActionHandler(_ => action()));
}
