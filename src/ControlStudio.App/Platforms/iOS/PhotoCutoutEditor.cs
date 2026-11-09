using ControlStudio.Core.Widgets;
using System.Diagnostics;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class PhotoCutoutEditor
{
    internal sealed record Result(byte[] Mask, byte[] Png, byte[]? Working, bool Changed);
    public static async Task<string?> EditAsync(UIWindow owner, Func<bool> canPresent, Action didPresent, PhotoCutoutImaging imaging, byte[]? pixels, byte[]? working, Color accent, Func<Result, Task<string>> saveResult)
    {
        try
        {
            var presenter = await WaitForPresenterAsync(owner, canPresent);
            if (presenter is null) return null;
            using var editor = new CutoutController(imaging, pixels, working, accent, saveResult);
            try
            {
                await presenter.PresentViewControllerAsync(editor, true);
                editor.VerifyInitialLayout();
                // Retain the widget image through the opening animation, then
                // release its composition before background removal can run.
                didPresent();
                return await editor.Completion.Task;
            }
            finally
            {
                // Vision/rendering must release the source before the caller disposes it.
                await editor.Work;
                if (editor.PresentingViewController is not null) await editor.DismissViewControllerAsync(true);
            }
        }
        catch (Exception ex) when (ex is not IOException)
        {
            throw new IOException("The cut-out screen could not open. Your photo is unchanged. Tap Edit photo to try again.", ex);
        }
    }
    private static async Task<UIViewController?> WaitForPresenterAsync(UIWindow owner, Func<bool> canPresent)
    {
        // Use the initiating MAUI window rather than the global topmost controller,
        // which can be an alert that is still dismissing. Yield only while UIKit
        // reports an actual transition, with a bound if a panel remains open.
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(2))
        {
            if (!canPresent()) return null; // Page/tab changed while decoding the source or dismissing a panel.
            if (owner.Handle == IntPtr.Zero || owner.RootViewController is not { } candidate
                || UIApplication.SharedApplication.ApplicationState != UIApplicationState.Active
                || owner.WindowScene?.ActivationState != UISceneActivationState.ForegroundActive)
                throw new IOException("Return to Control Studio, then tap Edit photo again.");
            var transitioning = false;
            while (true)
            {
                transitioning |= candidate.IsBeingDismissed || candidate.IsBeingPresented || candidate.GetTransitionCoordinator() is not null;
                if (candidate.PresentedViewController is not { } next) break;
                candidate = next;
            }
            if (!transitioning && candidate is not UIAlertController && candidate.ViewIfLoaded?.Window?.Handle == owner.Handle)
                return candidate;
            await Task.Delay(16);
        }
        throw new IOException("Close the open panel, then tap Edit photo again. Your photo is unchanged.");
    }

    private sealed class CutoutPageScrollView : UIScrollView
    {
        public UIView? Canvas { get; set; }
        public override bool GestureRecognizerShouldBegin(UIGestureRecognizer recognizer)
        {
            // The scroll view remains the delegate of its built-in recognizer.
            // Filtering through the view override avoids replacing that delegate.
            if (recognizer.Handle == PanGestureRecognizer.Handle && Canvas is { } canvas
                && canvas.Bounds.Contains(recognizer.LocationInView(canvas))) return false;
            return base.GestureRecognizerShouldBegin(recognizer);
        }
    }

    private sealed class CutoutController : UIViewController
    {
        private readonly PhotoCutoutImaging imaging;
        private readonly PhotoEditSession session;
        private readonly Color accent;
        private readonly Func<Result, Task<string>> saveResult;
        private readonly CutoutPageScrollView page = new() { AlwaysBounceVertical = true };
        private readonly UIStackView stack = new() { Axis = UILayoutConstraintAxis.Vertical, Spacing = 12 };
        private readonly UIScrollView zoom = new()
        {
            MinimumZoomScale = 1, MaximumZoomScale = 8, BouncesZoom = false, ClipsToBounds = true,
            ShowsHorizontalScrollIndicator = false, ShowsVerticalScrollIndicator = false,
            ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never
        };
        private readonly PaintView preview;
        private readonly UIButton cancel = Button("Cancel", "CancelPhotoCutout");
        private readonly UIButton apply = Button("Apply", "ApplyPhotoCutout");
        private readonly UIButton automatic = Button("Remove background", "AutomaticPhotoCutout");
        private readonly UIButton undo = Button("Undo", "UndoPhotoCutout");
        private readonly UIButton redo = Button("Redo", "RedoPhotoCutout");
        private readonly UIButton before = Button("Show original", "ComparePhotoCutout");
        private readonly UIButton reset = Button("Reset photo", "ResetPhotoCutout");
        private readonly UIButton removeObject = Button("Remove selection", "RemovePhotoObject");
        private readonly UIButton clearSelection = Button("Clear selection", "ClearPhotoObjectSelection");
        private readonly UISegmentedControl selectionMode = new(["Select", "Deselect"]);
        private readonly UIStackView repairControls = new() { Axis = UILayoutConstraintAxis.Vertical, Spacing = 8 };
        private readonly UIActivityIndicatorView progress = new(UIActivityIndicatorViewStyle.Medium)
        { HidesWhenStopped = true, AccessibilityLabel = "Processing photo" };
        private CancellationTokenSource? removalCancellation;
        private readonly UISegmentedControl mode = new(["Erase", "Restore", "Remove object"]);
        private readonly UISlider size = new() { MinValue = 2, MaxValue = 160, Value = 32, AccessibilityLabel = "Brush diameter in image pixels" };
        private readonly UISlider softness = new() { MinValue = 0, MaxValue = 1, Value = .35f, AccessibilityLabel = "Brush softness" };
        private readonly UISlider magnification = new() { MinValue = 1, MaxValue = 8, Value = 1, AccessibilityLabel = "Preview zoom" };
        private readonly UILabel sizeLabel = Label("Brush size: 32 px"), softnessLabel = Label("Softness: 35%"), status = Label("");
        private NSLayoutConstraint? previewHeight;
        private NSObject? backgroundObserver;
        private UIPanGestureRecognizer? brush;
        private UITapGestureRecognizer? dab;
        private bool busy, compare, dismissed, cancelling;
        private CGSize lastViewport;
        public Task Work { get; private set; } = Task.CompletedTask;
        public TaskCompletionSource<string?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CutoutController(PhotoCutoutImaging imaging, byte[]? pixels, byte[]? working, Color accent, Func<Result, Task<string>> saveResult)
        {
            this.imaging = imaging; this.accent = accent; this.saveResult = saveResult; session = new(imaging.Width, imaging.Height, imaging.Pixels, pixels, working);
            preview = new(imaging, session, accent) { ContentMode = UIViewContentMode.Redraw,
                IsAccessibilityElement = true, AccessibilityLabel = "Photo editing canvas",
                AccessibilityHint = "Use actions to paint at the visible centre or move the canvas. Brush size, softness and zoom are adjustable below." };
            preview.RenderFailed = () => Message("The preview could not be updated. Your edits are kept; try Undo or Apply again.");
            ModalPresentationStyle = UIModalPresentationStyle.FullScreen; ModalInPresentation = true;
        }
        public override void ViewDidLoad()
        {
            base.ViewDidLoad(); View!.BackgroundColor = UIColor.SystemBackground;
            View.AddSubview(page); page.AddSubview(stack);
            page.TranslatesAutoresizingMaskIntoConstraints = false; stack.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints([
                page.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor), page.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
                page.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor), page.BottomAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.BottomAnchor),
                stack.LeadingAnchor.ConstraintEqualTo(page.ContentLayoutGuide.LeadingAnchor, 16), stack.TrailingAnchor.ConstraintEqualTo(page.ContentLayoutGuide.TrailingAnchor, -16),
                stack.TopAnchor.ConstraintEqualTo(page.ContentLayoutGuide.TopAnchor, 8), stack.BottomAnchor.ConstraintEqualTo(page.ContentLayoutGuide.BottomAnchor, -20),
                // NSLayoutDimension's second argument is a multiplier, unlike
                // position anchors' offset argument. Always name both values.
                stack.WidthAnchor.ConstraintEqualTo(page.FrameLayoutGuide.WidthAnchor, multiplier: 1, constant: -32)]);
            var title = Label("Edit photo"); title.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Headline)!; title.TextAlignment = UITextAlignment.Center;
            stack.AddArrangedSubview(Row(cancel, title, apply));
            stack.AddArrangedSubview(Label("Erase makes pixels transparent. Remove object fills your selection from the surrounding photo. Pinch to zoom; move with two fingers."));
            zoom.AddSubview(preview); zoom.ViewForZoomingInScrollView = _ => preview;
            zoom.PanGestureRecognizer.MinimumNumberOfTouches = 2;
            zoom.Layer.CornerRadius = 16; zoom.Layer.BorderWidth = 1;
            preview.AccessibilityCustomActions = [
                AccessibleAction("Erase at visible centre", () => AccessiblePaint(false)),
                AccessibleAction("Restore at visible centre", () => AccessiblePaint(true)),
                AccessibleAction("Select object at visible centre", () => AccessibleSelect()),
                AccessibleAction("Deselect object at visible centre", () => AccessibleSelect(false)),
                AccessibleAction("Move view left", () => AccessiblePan(-1, 0)),
                AccessibleAction("Move view right", () => AccessiblePan(1, 0)),
                AccessibleAction("Move view up", () => AccessiblePan(0, -1)),
                AccessibleAction("Move view down", () => AccessiblePan(0, 1))];
            // The surrounding controls scroll with one finger. That recognizer must
            // never steal a brush stroke or a two-finger canvas gesture.
            page.Canvas = zoom;
            zoom.DidZoom += ZoomChanged;
            stack.AddArrangedSubview(zoom); previewHeight = zoom.HeightAnchor.ConstraintEqualTo(300); previewHeight.Active = true;
            brush = new UIPanGestureRecognizer(Paint) { MinimumNumberOfTouches = 1, MaximumNumberOfTouches = 1 };
            dab = new UITapGestureRecognizer(Dab); dab.RequireGestureRecognizerToFail(brush);
            preview.AddGestureRecognizer(brush); preview.AddGestureRecognizer(dab);
            mode.SelectedSegment = 0; mode.AccessibilityLabel = "Photo editing tool";
            mode.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true;
            stack.AddArrangedSubview(automatic); stack.AddArrangedSubview(mode);
            selectionMode.SelectedSegment = 0; selectionMode.AccessibilityLabel = "Object selection brush";
            selectionMode.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true;
            repairControls.AddArrangedSubview(selectionMode);
            repairControls.AddArrangedSubview(Row(removeObject, clearSelection));
            repairControls.AddArrangedSubview(Label("Brush over the complete object, including its shadow. The selected area will be filled, not made transparent. Smaller areas and simple backgrounds work best."));
            stack.AddArrangedSubview(repairControls);
            stack.AddArrangedSubview(Row(undo, redo, before));
            stack.AddArrangedSubview(sizeLabel); stack.AddArrangedSubview(size);
            stack.AddArrangedSubview(softnessLabel); stack.AddArrangedSubview(softness);
            stack.AddArrangedSubview(Label("Zoom")); stack.AddArrangedSubview(magnification);
            stack.AddArrangedSubview(reset); stack.AddArrangedSubview(progress); stack.AddArrangedSubview(status);
            stack.AddArrangedSubview(Label("Your original photo stays unchanged. Apply updates only this layer; Save widget publishes it to your Home Screen."));
            foreach (var slider in new[] { size, softness, magnification }) slider.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true;
            cancel.TouchUpInside += Cancel; apply.TouchUpInside += Apply;
            automatic.TouchUpInside += Automatic; undo.TouchUpInside += Undo; redo.TouchUpInside += Redo;
            before.TouchUpInside += Compare; reset.TouchUpInside += Reset;
            removeObject.TouchUpInside += RemoveObject; clearSelection.TouchUpInside += ClearSelection;
            mode.ValueChanged += ToolChanged;
            selectionMode.ValueChanged += SelectionModeChanged;
            size.ValueChanged += BrushChanged; softness.ValueChanged += BrushChanged; magnification.ValueChanged += MagnificationChanged;
            backgroundObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.WillResignActiveNotification, _ => { EndPainting(cancelled: true); removalCancellation?.Cancel(); });
            Update();
        }
        public void VerifyInitialLayout()
        {
            View!.LayoutIfNeeded(); page.LayoutIfNeeded(); stack.LayoutIfNeeded();
            var expectedWidth = (double)page.Bounds.Width - 32;
            var actualWidth = (double)stack.Bounds.Width;
            if (!double.IsFinite(expectedWidth) || !double.IsFinite(actualWidth) || expectedWidth < 44
                || Math.Abs(actualWidth - expectedWidth) > 1 || zoom.Bounds.Width <= 0 || zoom.Bounds.Height <= 0
                || preview.Bounds.Width <= 0 || preview.Bounds.Height <= 0
                || new[] { cancel, apply, automatic }.Any(button => button.Bounds.Width < 44 || button.Bounds.Height < 44))
                throw new IOException("The photo editor could not fit this screen. Your photo is unchanged. Return to the widget and try Edit photo again.");
        }
        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            previewHeight!.Constant = (nfloat)Math.Clamp((double)(View!.Bounds.Height - View.SafeAreaInsets.Top - View.SafeAreaInsets.Bottom) * .43, 180, 440);
            var viewport = zoom.Bounds.Size;
            if (viewport.Width > 0 && viewport.Height > 0 && viewport != lastViewport)
            {
                EndPainting(cancelled: true); lastViewport = viewport; zoom.SetZoomScale(1, false);
                var fit = Math.Min(viewport.Width / imaging.Width, viewport.Height / imaging.Height);
                preview.Frame = new CGRect(0, 0, imaging.Width * fit, imaging.Height * fit);
                zoom.ContentSize = preview.Frame.Size; CenterPreview();
            }
            var dark = TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark;
            View.TintColor = AppearanceColors.TextAccent(accent, dark).ToPlatform();
            zoom.Layer.BorderColor = UIColor.Separator.CGColor;
            apply.BackgroundColor = accent.ToPlatform(); apply.SetTitleColor(AppearanceColors.ButtonForeground(accent).ToPlatform(), UIControlState.Normal);
            mode.SelectedSegmentTintColor = accent.ToPlatform();
            selectionMode.SelectedSegmentTintColor = accent.ToPlatform();
            mode.SetTitleTextAttributes(new UIStringAttributes { ForegroundColor = AppearanceColors.ButtonForeground(accent).ToPlatform() }, UIControlState.Selected);
            selectionMode.SetTitleTextAttributes(new UIStringAttributes { ForegroundColor = AppearanceColors.ButtonForeground(accent).ToPlatform() }, UIControlState.Selected);
        }
        private void CenterPreview()
        {
            zoom.ContentInset = new UIEdgeInsets((nfloat)Math.Max(0, (zoom.Bounds.Height - zoom.ContentSize.Height) / 2),
                (nfloat)Math.Max(0, (zoom.Bounds.Width - zoom.ContentSize.Width) / 2), 0, 0);
        }
        private void ZoomChanged(object? sender, EventArgs e) { magnification.Value = (float)zoom.ZoomScale; CenterPreview(); }
        private void MagnificationChanged(object? sender, EventArgs e) => zoom.SetZoomScale(magnification.Value, false);
        private bool CanPaint => !busy && !compare && !dismissed && UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active;
        private void Paint(UIPanGestureRecognizer recognizer)
        {
            try
            {
                if (recognizer.State == UIGestureRecognizerState.Began && CanPaint)
                {
                    session.BeginStroke(mode.SelectedSegment == 2); Update();
                    var point = recognizer.LocationInView(preview); var translation = recognizer.TranslationInView(preview);
                    PaintAt(new CGPoint(point.X - translation.X, point.Y - translation.Y));
                }
                if (recognizer.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed && CanPaint && recognizer.NumberOfTouches == 1)
                    PaintAt(recognizer.LocationInView(preview));
                if (recognizer.State == UIGestureRecognizerState.Ended) EndPainting(false);
                else if (recognizer.State is UIGestureRecognizerState.Cancelled or UIGestureRecognizerState.Failed) EndPainting(true);
            }
            catch { EndPainting(true); Message("This brush stroke could not be applied. Try again."); }
        }
        private void Dab(UITapGestureRecognizer recognizer)
        {
            if (!CanPaint || recognizer.State != UIGestureRecognizerState.Ended) return;
            try { session.BeginStroke(mode.SelectedSegment == 2); PaintAt(recognizer.LocationInView(preview)); EndPainting(false); }
            catch { EndPainting(true); Message("This brush stroke could not be applied. Try again."); }
        }
        private void PaintAt(CGPoint point)
        {
            if (preview.Bounds.Width <= 0 || preview.Bounds.Height <= 0) return;
            var x = (double)(point.X / preview.Bounds.Width) * imaging.Width; var y = (double)(point.Y / preview.Bounds.Height) * imaging.Height;
            session.Paint(x, y, size.Value / 2, softness.Value, mode.SelectedSegment == 2 ? selectionMode.SelectedSegment == 0 : mode.SelectedSegment == 1);
            preview.InvalidateMask(point, (nfloat)(size.Value / imaging.Width * preview.Bounds.Width / 2));
        }
        private void EndPainting(bool cancelled)
        { if (!session.IsPainting) return; session.EndStroke(cancelled); preview.InvalidateMask(); if (IsViewLoaded) Update(); }
        private void BrushChanged(object? sender, EventArgs e)
        { sizeLabel.Text = $"Brush size: {Math.Round(size.Value)} px"; softnessLabel.Text = $"Softness: {Math.Round(softness.Value * 100)}%"; }
        private void ToolChanged(object? sender, EventArgs e)
        {
            EndPainting(true); Update();
            if (mode.SelectedSegment == 2) Message("Select an object, then tap Remove selection. This runs on your iPhone; the first use prepares the model.");
        }
        private void ClearSelection(object? sender, EventArgs e)
        { if (!CanPaint || session.IsPainting) return; session.ClearSelection(); preview.InvalidateMask(); Update(); }
        private void SelectionModeChanged(object? sender, EventArgs e)
        { mode.SelectedSegment = 2; EndPainting(true); Update(); Message(selectionMode.SelectedSegment == 0 ? "Brush over the object to select it." : "Brush over the selection to deselect it."); }
        private bool AccessibleSelect(bool selecting = true)
        {
            if (!CanPaint || session.IsPainting) return false;
            mode.SelectedSegment = 2; selectionMode.SelectedSegment = selecting ? 0 : 1;
            try
            {
                session.BeginStroke(true);
                PaintAt(preview.ConvertPointFromView(new CGPoint(zoom.Bounds.X + zoom.Bounds.Width / 2, zoom.Bounds.Y + zoom.Bounds.Height / 2), zoom));
                EndPainting(false); Message(selecting ? "Object selected at visible centre. Tap Remove selection to fill the area." : "Object deselected at visible centre."); return true;
            }
            catch { EndPainting(true); Message("The selection could not be applied."); return false; }
        }
        private bool AccessiblePaint(bool restore)
        {
            if (!CanPaint || session.IsPainting) return false;
            try
            {
                mode.SelectedSegment = restore ? 1 : 0; session.BeginStroke(mode.SelectedSegment == 2);
                PaintAt(preview.ConvertPointFromView(new CGPoint(zoom.Bounds.X + zoom.Bounds.Width / 2, zoom.Bounds.Y + zoom.Bounds.Height / 2), zoom));
                EndPainting(false); Message(restore ? "Restored at visible centre." : "Erased at visible centre."); return true;
            }
            catch { EndPainting(true); Message("The brush could not be applied. Try again."); return false; }
        }
        private bool AccessiblePan(int x, int y)
        {
            if (busy || dismissed || session.IsPainting) return false;
            var nextX = Math.Clamp((double)(zoom.ContentOffset.X + x * zoom.Bounds.Width / 3), -(double)zoom.ContentInset.Left, Math.Max(-(double)zoom.ContentInset.Left, (double)(zoom.ContentSize.Width - zoom.Bounds.Width)));
            var nextY = Math.Clamp((double)(zoom.ContentOffset.Y + y * zoom.Bounds.Height / 3), -(double)zoom.ContentInset.Top, Math.Max(-(double)zoom.ContentInset.Top, (double)(zoom.ContentSize.Height - zoom.Bounds.Height)));
            zoom.SetContentOffset(new CGPoint(nextX, nextY), false); return true;
        }
        private void Undo(object? sender, EventArgs e) { if (!CanPaint) return; session.Undo(); preview.InvalidateMask(); Update(); }
        private void Redo(object? sender, EventArgs e) { if (!CanPaint) return; session.Redo(); preview.InvalidateMask(); Update(); }
        private void Reset(object? sender, EventArgs e)
        { if (!CanPaint) return; session.Reset(); preview.InvalidateMask(); Update(); }
        private void Compare(object? sender, EventArgs e)
        {
            if (busy || dismissed || session.IsPainting) return;
            compare = !compare; preview.ShowOriginal = compare; preview.SetNeedsDisplay(); Update();
            Message(compare ? "Original shown. Choose Show edited to continue editing or apply." : "Edited photo shown.");
        }
        private void Cancel(object? sender, EventArgs e)
        {
            if (dismissed || cancelling) return;
            EndPainting(true);
            if (busy) { cancelling = true; removalCancellation?.Cancel(); Update(); Message("Finishing the current operation before closing…"); }
            else Complete(null);
        }
        private void Automatic(object? sender, EventArgs e) { if (!CanPaint || session.IsPainting) return; Work = RunAsync(false); }
        private void Apply(object? sender, EventArgs e) { if (busy || dismissed || compare || session.IsPainting || session.HasSelection) return; Work = RunAsync(true); }
        private void RemoveObject(object? sender, EventArgs e)
        { if (!CanPaint || session.IsPainting || !session.HasSelection) return; Work = RemoveObjectAsync(); }
        private async Task RemoveObjectAsync()
        {
            busy = true; Update(); Message("Removing selection on this iPhone… First use may take longer while the model is prepared.");
            using var cancelWork = new CancellationTokenSource(); removalCancellation = cancelWork;
            try
            {
                var generated = await PhotoObjectRemovalImaging.RemoveAsync(imaging.Width, imaging.Height, session.Pixels, session.Selection.Pixels.ToArray(), cancelWork.Token);
                if (!dismissed && !cancelling && !cancelWork.IsCancellationRequested)
                { session.AcceptRepair(generated); preview.InvalidateMask(); Message("Selection filled. Check the result, Undo to retry, or Apply to keep it."); }
            }
            catch (OperationCanceledException) { if (!cancelling && !dismissed) Message("Removal stopped. Your selection and photo are unchanged."); }
            catch (Exception ex) { if (!cancelling && !dismissed) Message(ex is IOException ? ex.Message : "Removal could not finish. Your photo and selection are kept; try a smaller area."); }
            finally { removalCancellation = null; busy = false; if (cancelling) Complete(null); if (!dismissed) Update(); }
        }
        private void Complete(string? key)
        {
            if (dismissed) return;
            // Freeze all callbacks before the asynchronous dismissal. A second tap
            // must not start work after the owner has begun releasing this modal.
            dismissed = true; Update(); Completion.TrySetResult(key);
        }
        private async Task RunAsync(bool saving)
        {
            busy = true; Update(); Message(saving ? "Preparing photo…" : "Removing background on this iPhone…");
            try
            {
                if (saving)
                {
                    var pixels = session.Mask.Pixels.ToArray();
                    var colours = session.Pixels;
                    var png = await Task.Run(() =>
                    {
                        using var pool = new NSAutoreleasePool(); using var image = imaging.Render(colours, pixels);
                        using var encoded = image.AsPNG() ?? throw new IOException("The edited photo could not be prepared.");
                        if (encoded.Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("This edited photo is too detailed to save.");
                        return encoded.ToArray();
                    });
                    if (!dismissed && !cancelling)
                    {
                        // Keep this editor open on a storage failure so fine brush work
                        // can be retried rather than lost after dismissing the modal.
                        var working = session.HasRepairs ? await Task.Run(() =>
                        {
                            using var pool = new NSAutoreleasePool(); using var image = imaging.Image(colours);
                            using var encoded = image.AsPNG() ?? throw new IOException("The repaired photo could not be prepared.");
                            if (encoded.Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("This repaired photo is too detailed to save.");
                            return encoded.ToArray();
                        }) : null;
                        if (dismissed || cancelling) return;
                        var key = await saveResult(new(pixels, png, working, session.HasChanges));
                        if (!dismissed && !cancelling) Complete(key);
                    }
                }
                else
                {
                    var generated = await Task.Run(() => imaging.AutomaticMask(session.HasRepairs ? session.Pixels : null));
                    if (!dismissed && !cancelling) { session.ReplaceMask(generated); preview.InvalidateMask(); Message("Background removed. Use Erase and Restore to refine the edges."); }
                }
            }
            catch (Exception ex) { if (!dismissed && !cancelling) Message(ex is IOException ? ex.Message : "This photo could not be processed. Try manual Erase and Restore."); }
            finally
            {
                busy = false;
                if (cancelling) Complete(null);
                if (!dismissed) Update();
            }
        }
        private void Update()
        {
            if (busy && !dismissed) progress.StartAnimating(); else progress.StopAnimating();
            var editable = !dismissed && !busy && !compare && !session.IsPainting;
            foreach (var button in new[] { automatic, reset }) button.Enabled = editable;
            apply.Enabled = !dismissed && !busy && !compare && !session.IsPainting && !session.HasSelection; cancel.Enabled = !dismissed && !cancelling;
            before.Enabled = !dismissed && !busy && !session.IsPainting; before.SetTitle(compare ? "Show edited" : "Show original", UIControlState.Normal);
            undo.Enabled = editable && session.CanUndo; redo.Enabled = editable && session.CanRedo;
            mode.Enabled = size.Enabled = softness.Enabled = editable;
            repairControls.Hidden = mode.SelectedSegment != 2 && !session.HasSelection;
            selectionMode.Enabled = editable;
            removeObject.Enabled = clearSelection.Enabled = editable && session.HasSelection;
            zoom.UserInteractionEnabled = magnification.Enabled = !dismissed && !busy;
        }
        private void Message(string text)
        { status.Text = text; if (View?.Window is not null) UIAccessibility.PostNotification(UIAccessibilityPostNotification.Announcement, new NSString(text)); }
        public override void ViewDidDisappear(bool animated)
        { base.ViewDidDisappear(animated); EndPainting(true); if (PresentingViewController is null) { removalCancellation?.Cancel(); dismissed = true; Completion.TrySetResult(null); } }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dismissed = true; backgroundObserver?.Dispose(); backgroundObserver = null;
                zoom.ViewForZoomingInScrollView = null; zoom.DidZoom -= ZoomChanged;
                page.Canvas = null;
                cancel.TouchUpInside -= Cancel; apply.TouchUpInside -= Apply; automatic.TouchUpInside -= Automatic;
                undo.TouchUpInside -= Undo; redo.TouchUpInside -= Redo; before.TouchUpInside -= Compare; reset.TouchUpInside -= Reset;
                mode.ValueChanged -= ToolChanged; removeObject.TouchUpInside -= RemoveObject; clearSelection.TouchUpInside -= ClearSelection;
                selectionMode.ValueChanged -= SelectionModeChanged;
                size.ValueChanged -= BrushChanged; softness.ValueChanged -= BrushChanged; magnification.ValueChanged -= MagnificationChanged;
                if (brush is not null) { preview.RemoveGestureRecognizer(brush); brush.Dispose(); }
                if (dab is not null) { preview.RemoveGestureRecognizer(dab); dab.Dispose(); }
                if (preview.AccessibilityCustomActions is { } actions) { preview.AccessibilityCustomActions = null; foreach (var action in actions) action.Dispose(); }
                preview.RenderFailed = null; preview.Dispose();
            }
            base.Dispose(disposing);
        }
        private static UILabel Label(string text) => new()
        { Text = text, Lines = 0, TextColor = UIColor.Label, Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Subheadline)!, AdjustsFontForContentSizeCategory = true };
        private static UIButton Button(string title, string identifier)
        {
            var result = new UIButton(UIButtonType.System) { AccessibilityIdentifier = identifier };
            result.SetTitle(title, UIControlState.Normal); result.TitleLabel!.Font = UIFont.GetPreferredFontForTextStyle(UIFontTextStyle.Body)!;
            result.TitleLabel.AdjustsFontForContentSizeCategory = true; result.TitleLabel.Lines = 0;
            result.HeightAnchor.ConstraintGreaterThanOrEqualTo(44).Active = true; result.Layer.CornerRadius = 14; return result;
        }
        private static UIStackView Row(params UIView[] views) => new(views)
        { Axis = UILayoutConstraintAxis.Horizontal, Distribution = UIStackViewDistribution.FillEqually, Spacing = 8 };
        private static UIAccessibilityCustomAction AccessibleAction(string title, Func<bool> action) => new(title, new UIAccessibilityCustomActionHandler(_ => action()));
    }

    private sealed class PaintView(PhotoCutoutImaging imaging, PhotoEditSession session, Color accent) : UIView
    {
        private UIImage? rendered;
        private UIImage? selection;
        private bool dirty = true;
        private CGPoint? pointer;
        private nfloat radius;
        public bool ShowOriginal { get; set; }
        public Action? RenderFailed { get; set; }
        public void InvalidateMask(CGPoint? point = null, nfloat brushRadius = default)
        { dirty = true; pointer = point; radius = brushRadius; SetNeedsDisplay(); }
        public override void Draw(CGRect rect)
        {
            using var context = UIGraphics.GetCurrentContext(); if (context is null) return;
            var step = 12;
            for (var y = 0; y < Bounds.Height; y += step) for (var x = 0; x < Bounds.Width; x += step)
            {
                context.SetFillColor(((x / step + y / step) % 2 == 0 ? UIColor.SystemGray4 : UIColor.SystemGray5).CGColor);
                context.FillRect(new CGRect(x, y, step, step));
            }
            if (!ShowOriginal && dirty)
            {
                try
                {
                    var next = imaging.Render(session.Pixels, session.Mask.Pixels); var old = rendered; rendered = next; old?.Dispose();
                    selection?.Dispose(); selection = null;
                    if (session.HasSelection)
                    {
                        var rgba = new byte[imaging.Width * imaging.Height * 4];
                        for (var p = 0; p < session.Selection.Pixels.Length; p++)
                        {
                            var alpha = (byte)(session.Selection.Pixels[p] / 2);
                            rgba[p * 4] = (byte)(accent.Red * alpha); rgba[p * 4 + 1] = (byte)(accent.Green * alpha);
                            rgba[p * 4 + 2] = (byte)(accent.Blue * alpha); rgba[p * 4 + 3] = alpha;
                            // A contrast stripe keeps white/yellow accents legible on pale photos.
                            if ((p % imaging.Width + p / imaging.Width) / 8 % 3 == 0)
                            {
                                var light = session.Pixels[p * 4] * 299 + session.Pixels[p * 4 + 1] * 587 + session.Pixels[p * 4 + 2] * 114 > 128000;
                                rgba[p * 4] = rgba[p * 4 + 1] = rgba[p * 4 + 2] = light ? (byte)0 : alpha;
                            }
                        }
                        selection = imaging.Image(rgba);
                    }
                    dirty = false;
                }
                catch { dirty = false; RenderFailed?.Invoke(); }
            }
            (ShowOriginal ? imaging.Original : rendered)?.Draw(Bounds);
            if (!ShowOriginal) selection?.Draw(Bounds);
            if (pointer is { } point)
            {
                context.SetStrokeColor(accent.ToPlatform().CGColor); context.SetLineWidth((nfloat)(1 / Math.Max(1, (double)Transform.A)));
                context.StrokeEllipseInRect(new CGRect(point.X - radius, point.Y - radius, radius * 2, radius * 2));
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) { rendered?.Dispose(); rendered = null; selection?.Dispose(); selection = null; } base.Dispose(disposing); }
    }
}
