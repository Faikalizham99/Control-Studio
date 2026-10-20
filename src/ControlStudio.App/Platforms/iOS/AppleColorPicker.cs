using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class AppleColorPicker
{
    private Session? active;

    public Task<string> PickAsync(string initialHex, Action<string> preview)
    {
        if (active is not null) throw new InvalidOperationException("The colour picker is already open.");
        var presenter = Platform.GetCurrentUIViewController() ?? throw new InvalidOperationException("The colour picker is unavailable.");
        var session = new Session(this, initialHex, preview);
        active = session;
        try { session.Present(presenter); }
        catch { session.Release(); throw; }
        return session.Completion.Task;
    }

    private sealed class Session : UIColorPickerViewControllerDelegate
    {
        private readonly AppleColorPicker owner;
        private readonly Action<string> preview;
        private readonly UIColorPickerViewController picker;
        private readonly DismissDelegate dismissal;
        private string lastHex;
        private bool completed;
        public TaskCompletionSource<string> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Session(AppleColorPicker owner, string hex, Action<string> preview)
        {
            this.owner = owner; this.preview = preview; lastHex = hex;
            picker = new UIColorPickerViewController { SupportsAlpha = false, SelectedColor = Color.FromArgb(hex).ToPlatform() };
            dismissal = new DismissDelegate(Complete);
            picker.Delegate = this;
        }
        public void Present(UIViewController presenter)
        {
            presenter.PresentViewController(picker, true, null);
            if (picker.PresentationController is { } presentation) presentation.Delegate = dismissal;
        }
        public override void DidSelectColor(UIColorPickerViewController viewController, UIColor color, bool continuously)
        {
            if (color.ToColor() is not { } selected) return;
            lastHex = "#" + selected.ToArgbHex()[3..];
            preview(lastHex);
        }
        public override void DidFinish(UIColorPickerViewController viewController) => viewController.DismissViewController(true, Complete);
        private void Complete()
        {
            if (completed) return;
            completed = true;
            var hex = picker.SelectedColor.ToColor() is { } selected ? "#" + selected.ToArgbHex()[3..] : lastHex;
            Completion.TrySetResult(hex);
            Release();
        }
        public void Release()
        {
            picker.Delegate = null;
            if (picker.PresentationController is { } presentation) presentation.Delegate = null!;
            picker.Dispose(); dismissal.Dispose();
            if (ReferenceEquals(owner.active, this)) owner.active = null;
            Dispose();
        }
    }

    private sealed class DismissDelegate(Action completed) : UIAdaptivePresentationControllerDelegate
    {
        public override void DidDismiss(UIPresentationController presentationController) => completed();
    }
}
