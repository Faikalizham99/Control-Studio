using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class ProfilePhotoCropper
{
    public static async Task<UIImage?> CropAsync(UIImage source, string accentHex)
    {
        var presenter = Platform.GetCurrentUIViewController() ?? throw new IOException("The photo editor is unavailable. Try again.");
        using var editor = new CropController(source, Color.FromArgb(accentHex));
        await presenter.PresentViewControllerAsync(editor, true);
        try { return await editor.Completion.Task; }
        finally
        {
            if (editor.PresentingViewController is not null) await editor.DismissViewControllerAsync(true);
        }
    }

    private sealed class CropController : UIViewController
    {
        private readonly UIImage original;
        private readonly Color accent;
        private UIImage? rotated;
        private readonly UIImageView image = new();
        private readonly UIScrollView crop = new()
        {
            Bounces = false, BouncesZoom = false, ShowsHorizontalScrollIndicator = false, ShowsVerticalScrollIndicator = false,
            ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never,
            BackgroundColor = UIColor.SecondarySystemBackground, ClipsToBounds = true,
            AccessibilityIdentifier = "ProfileCropPreview"
        };
        private readonly UILabel title = new() { Text = "Adjust photo", Font = PreferredFont(UIFontTextStyle.Title2, 22), TextColor = UIColor.Label };
        private readonly UILabel instructions = new()
        {
            Text = "Drag to position. Pinch or use Zoom to resize. The circle shows your profile picture.",
            Lines = 0, Font = PreferredFont(UIFontTextStyle.Subheadline, 15), TextColor = UIColor.SecondaryLabel
        };
        private readonly UILabel zoomTitle = new() { Text = "Zoom", Font = PreferredFont(UIFontTextStyle.Subheadline, 15), TextColor = UIColor.Label };
        private readonly UISlider zoom = new() { MinValue = 1, MaxValue = 4, Value = 1, AccessibilityLabel = "Photo zoom", AccessibilityIdentifier = "ProfileCropZoom" };
        private readonly UIButton turn = Button("Rotate", "ProfileCropRotate");
        private readonly UIButton reset = Button("Reset", "ProfileCropReset");
        private readonly UIButton cancel = Button("Cancel", "ProfileCropCancel");
        private readonly UIButton use = Button("Use photo", "ProfileCropConfirm");
        private readonly UILabel status = new() { Lines = 0, TextColor = UIColor.SecondaryLabel, Font = PreferredFont(UIFontTextStyle.Caption1, 12) };
        private int turns;
        private nfloat cropSide;
        private bool adjusting;
        private UIImage Current => rotated ?? original;
        public TaskCompletionSource<UIImage?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CropController(UIImage original, Color accent)
        {
            this.original = original;
            this.accent = accent;
            ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
            ModalInPresentation = true;
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            View!.BackgroundColor = UIColor.SystemBackground;
            image.Image = original;
            crop.AddSubview(image);
            crop.ViewForZoomingInScrollView = _ => image;
            crop.DidZoom += DidZoom;
            zoom.ValueChanged += ZoomChanged;
            turn.TouchUpInside += Rotate;
            reset.TouchUpInside += Reset;
            cancel.TouchUpInside += Cancel;
            use.TouchUpInside += Confirm;
            use.BackgroundColor = accent.ToPlatform();
            use.SetTitleColor(AppearanceColors.ButtonForeground(accent).ToPlatform(), UIControlState.Normal);
            use.Layer.CornerRadius = 14;
            crop.Layer.BorderColor = UIColor.Separator.CGColor;
            crop.Layer.BorderWidth = 1;
            foreach (var control in new UIView[] { title, instructions, crop, zoomTitle, zoom, turn, reset, cancel, use, status }) View.AddSubview(control);
        }

        public override void ViewDidLayoutSubviews()
        {
            base.ViewDidLayoutSubviews();
            var bounds = View!.Bounds;
            View.TintColor = AppearanceColors.TextAccent(accent, TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark).ToPlatform();
            var safe = View.SafeAreaInsets;
            var width = bounds.Width - safe.Left - safe.Right;
            var left = safe.Left + 20;
            var top = safe.Top;
            title.Frame = new CGRect(left, top + 8, width - 120, 40);
            cancel.Frame = new CGRect(bounds.Width - safe.Right - 100, top + 6, 80, 44);
            instructions.Frame = new CGRect(left, top + 54, width - 40, 64);
            var side = (nfloat)Math.Max(80, Math.Min(Math.Min((double)width - 40, 400), (double)(bounds.Height - safe.Bottom - top) - 350));
            crop.Frame = new CGRect(safe.Left + (width - side) / 2, top + 130, side, side);
            crop.Layer.CornerRadius = side / 2;
            var below = crop.Frame.Bottom + 16;
            zoomTitle.Frame = new CGRect(left, below, 65, 44);
            zoom.Frame = new CGRect(left + 70, below, width - 110, 44);
            turn.Frame = new CGRect(left, below + 48, (width - 48) / 2, 44);
            reset.Frame = new CGRect(left + (width - 48) / 2 + 8, below + 48, (width - 48) / 2, 44);
            use.Frame = new CGRect(left, below + 102, width - 40, 48);
            status.Frame = new CGRect(left, below + 154, width - 40, 40);
            if (cropSide != side) { cropSide = side; FitImage(); }
        }

        private void FitImage()
        {
            if (cropSide <= 0) return;
            adjusting = true;
            try
            {
                crop.MinimumZoomScale = .0001f; crop.MaximumZoomScale = 10;
                crop.SetZoomScale(1, false);
                image.Frame = new CGRect(CGPoint.Empty, Current.Size);
                crop.ContentSize = Current.Size;
                var fit = (nfloat)Math.Max((double)(cropSide / Current.Size.Width), (double)(cropSide / Current.Size.Height));
                crop.MaximumZoomScale = fit * 4; crop.MinimumZoomScale = fit;
                crop.SetZoomScale(fit, false);
                crop.ContentOffset = new CGPoint(Math.Max(0, (double)(Current.Size.Width * fit - cropSide) / 2), Math.Max(0, (double)(Current.Size.Height * fit - cropSide) / 2));
                zoom.Value = 1;
            }
            finally { adjusting = false; }
        }

        private void DidZoom(object? sender, EventArgs args)
        {
            if (!adjusting) zoom.Value = (float)(crop.ZoomScale / crop.MinimumZoomScale);
        }

        private void ZoomChanged(object? sender, EventArgs args)
        {
            var scale = crop.MinimumZoomScale * zoom.Value;
            // Keep the center of the visible crop fixed when adjusting the slider.
            var side = cropSide / scale;
            var centerX = (crop.ContentOffset.X + cropSide / 2) / crop.ZoomScale;
            var centerY = (crop.ContentOffset.Y + cropSide / 2) / crop.ZoomScale;
            var x = Math.Clamp((double)(centerX - side / 2), 0, Math.Max(0, (double)(Current.Size.Width - side)));
            var y = Math.Clamp((double)(centerY - side / 2), 0, Math.Max(0, (double)(Current.Size.Height - side)));
            crop.ZoomToRect(new CGRect(x, y, side, side), false);
        }

        private void Rotate(object? sender, EventArgs args)
        {
            try
            {
                var nextTurns = (turns + 1) % 4;
                UIImage? next = null;
                if (nextTurns != 0)
                {
                    var size = nextTurns % 2 == 0 ? original.Size : new CGSize(original.Size.Height, original.Size.Width);
                    using var format = UIGraphicsImageRendererFormat.DefaultFormat;
                    format.Scale = 1; format.Opaque = true;
                    using var renderer = new UIGraphicsImageRenderer(size, format);
                    next = renderer.CreateImage(context =>
                    {
                        UIColor.White.SetFill(); context.FillRect(new CGRect(CGPoint.Empty, size));
                        context.CGContext.TranslateCTM(size.Width / 2, size.Height / 2);
                        context.CGContext.RotateCTM((nfloat)(nextTurns * Math.PI / 2));
                        original.Draw(new CGRect(-original.Size.Width / 2, -original.Size.Height / 2, original.Size.Width, original.Size.Height));
                    });
                }
                var previous = rotated; turns = nextTurns; rotated = next; image.Image = Current; previous?.Dispose();
                FitImage(); status.Text = "";
            }
            catch { status.Text = "The photo could not be rotated. Try Reset."; }
        }

        private void Reset(object? sender, EventArgs args)
        {
            turns = 0; image.Image = original; rotated?.Dispose(); rotated = null;
            FitImage(); status.Text = "";
        }

        private void Cancel(object? sender, EventArgs args) => Completion.TrySetResult(null);

        private void Confirm(object? sender, EventArgs args)
        {
            if (Completion.Task.IsCompleted || cropSide <= 0) return;
            try
            {
                var rect = PhotoCropGeometry.SourceSquare((double)Current.Size.Width, (double)Current.Size.Height,
                    (double)cropSide, (double)crop.ZoomScale, (double)crop.ContentOffset.X, (double)crop.ContentOffset.Y);
                using var format = UIGraphicsImageRendererFormat.DefaultFormat;
                format.Scale = 1; format.Opaque = true;
                using var renderer = new UIGraphicsImageRenderer(new CGSize(512, 512), format);
                var result = renderer.CreateImage(context =>
                {
                    UIColor.White.SetFill(); context.FillRect(new CGRect(0, 0, 512, 512));
                    var scale = 512 / rect.Width;
                    Current.Draw(new CGRect(-rect.X * scale, -rect.Y * scale, (double)Current.Size.Width * scale, (double)Current.Size.Height * scale));
                });
                if (!Completion.TrySetResult(result)) result.Dispose();
            }
            catch { status.Text = "The crop could not be created. Try Reset or another photo."; }
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            if (PresentingViewController is null) Completion.TrySetResult(null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                crop.DidZoom -= DidZoom; crop.ViewForZoomingInScrollView = null;
                zoom.ValueChanged -= ZoomChanged; turn.TouchUpInside -= Rotate; reset.TouchUpInside -= Reset;
                cancel.TouchUpInside -= Cancel; use.TouchUpInside -= Confirm;
                image.Image = null; rotated?.Dispose(); rotated = null;
            }
            base.Dispose(disposing);
        }

        private static UIButton Button(string title, string identifier)
        {
            var button = new UIButton(UIButtonType.System) { AccessibilityIdentifier = identifier };
            button.SetTitle(title, UIControlState.Normal);
            button.TitleLabel!.Font = PreferredFont(UIFontTextStyle.Body, 17);
            return button;
        }

        private static UIFont PreferredFont(UIFontTextStyle style, nfloat size) => UIFont.GetPreferredFontForTextStyle(style) ?? UIFont.SystemFontOfSize(size)!;
    }
}
