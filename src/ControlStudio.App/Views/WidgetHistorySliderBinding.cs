using System.ComponentModel;

namespace ControlStudio.App.Views;

/// <summary>Ends an editor slider step even when UIKit cancels or its panel disappears.</summary>
internal sealed class WidgetHistorySliderBinding : IDisposable
{
    private readonly Slider slider;
    private readonly Func<bool> canStart;
    private readonly Action begin, end;
    private readonly List<VisualElement> ancestors = [];
    private bool active, interrupting, disposed;
#if IOS
    private UIKit.UISlider? native;
#endif
    public WidgetHistorySliderBinding(Slider slider, VisualElement root, Func<bool> canStart, Action begin, Action end)
    {
        this.slider = slider; this.canStart = canStart; this.begin = begin; this.end = end;
        for (Element? item = slider; item is not null; item = item.Parent)
        {
            if (item is VisualElement view) { ancestors.Add(view); view.PropertyChanged += AvailabilityChanged; }
            if (ReferenceEquals(item, root)) break;
        }
        slider.DragStarted += Started; slider.DragCompleted += Completed;
        slider.HandlerChanging += HandlerChanging; slider.HandlerChanged += HandlerChanged;
        slider.Loaded += Loaded; slider.Unloaded += Unloaded;
        AttachNative();
    }
    private void Started(object? sender, EventArgs args)
    {
        if (disposed || active || !slider.IsEnabled || ancestors.Any(x => !x.IsVisible || !x.IsEnabled) || !canStart()) return;
        active = true; begin();
    }
    private void Completed(object? sender, EventArgs args)
    { if (active) { active = false; end(); } }
    private void AvailabilityChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(VisualElement.IsVisible) or nameof(VisualElement.IsEnabled)
            && ancestors.Any(x => !x.IsVisible || !x.IsEnabled)) Interrupt();
    }
    public void Interrupt()
    {
        if (!active || interrupting) return;
        interrupting = true;
        try { ((ISliderController)slider).SendDragCompleted(); }
        finally { try { Completed(slider, EventArgs.Empty); } finally { interrupting = false; } }
    }
    private void HandlerChanging(object? sender, HandlerChangingEventArgs args) { try { Interrupt(); } finally { DetachNative(); } }
    private void HandlerChanged(object? sender, EventArgs args) => AttachNative();
    private void Loaded(object? sender, EventArgs args) => AttachNative();
    private void Unloaded(object? sender, EventArgs args) { try { Interrupt(); } finally { DetachNative(); } }
    private void AttachNative()
    {
        DetachNative();
#if IOS
        if (!disposed && slider.Handler?.PlatformView is UIKit.UISlider value) { native = value; native.TouchCancel += Cancelled; }
#endif
    }
    private void DetachNative()
    {
#if IOS
        var previous = native; native = null;
        if (previous is not null && previous.Handle != IntPtr.Zero) previous.TouchCancel -= Cancelled;
#endif
    }
#if IOS
    private void Cancelled(object? sender, EventArgs args)
    {
        // Commit the last value accepted by the editor; UIKit must display that
        // same value after cancellation, rather than an unreported native value.
        if (native is not { } target || target.Handle == IntPtr.Zero || sender is not UIKit.UISlider source || source.Handle != target.Handle) return;
        target.SetValue((float)slider.Value, false); Interrupt();
    }
#endif
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { Interrupt(); }
        finally
        {
            DetachNative();
            slider.DragStarted -= Started; slider.DragCompleted -= Completed;
            slider.HandlerChanging -= HandlerChanging; slider.HandlerChanged -= HandlerChanged;
            slider.Loaded -= Loaded; slider.Unloaded -= Unloaded;
            foreach (var view in ancestors) view.PropertyChanged -= AvailabilityChanged;
            ancestors.Clear();
        }
    }
}
