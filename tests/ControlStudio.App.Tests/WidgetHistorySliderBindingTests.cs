using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetHistorySliderBindingTests : IDisposable
{
    private readonly MauiTestContext host = new();
    [Fact]
    public void InterruptDeliversCompletionOnceAndDisposalUnsubscribesAllCallbacks()
    {
        var slider = new Slider(); var root = new VerticalStackLayout { Children = { slider } }; host.Attach(root);
        var events = new List<string>(); slider.DragCompleted += (_, _) => events.Add("control-completed");
        var binding = new WidgetHistorySliderBinding(slider, root, () => true, () => events.Add("begin"), () => events.Add("end"));
        ((ISliderController)slider).SendDragStarted(); binding.Interrupt(); binding.Interrupt();
        Assert.Equal(new[] { "begin", "control-completed", "end" }, events);
        ((ISliderController)slider).SendDragStarted(); binding.Dispose(); binding.Dispose();
        Assert.Equal(new[] { "begin", "control-completed", "end", "begin", "control-completed", "end" }, events);
        events.Clear(); ((ISliderController)slider).SendDragStarted(); root.IsVisible = false; ((ISliderController)slider).SendDragCompleted();
        Assert.Equal(new[] { "control-completed" }, events);
    }
    [Fact]
    public void HandlerReplacementFinishesAnActiveStepAndTheReplacementAcceptsAnother()
    {
        var slider = new Slider { Handler = new Handler() }; var root = new VerticalStackLayout { Children = { slider } }; host.Attach(root);
        var starts = 0; var ends = 0;
        using var binding = new WidgetHistorySliderBinding(slider, root, () => true, () => starts++, () => ends++);
        ((ISliderController)slider).SendDragStarted(); slider.Handler = null; Assert.Equal(1, ends);
        slider.Handler = new Handler(); ((ISliderController)slider).SendDragStarted(); ((ISliderController)slider).SendDragCompleted();
        Assert.Equal(2, starts); Assert.Equal(2, ends);
    }
    [Fact]
    public void HiddenOrDisabledAncestorsRejectNewDragGroupsAndHidingAnAncestorEndsAnExistingOne()
    {
        var slider = new Slider(); var panel = new VerticalStackLayout { Children = { slider } }; var root = new ContentView { Content = panel }; host.Attach(root);
        var starts = 0; var ends = 0;
        using var binding = new WidgetHistorySliderBinding(slider, root, () => true, () => starts++, () => ends++);
        panel.IsVisible = false; ((ISliderController)slider).SendDragStarted(); Assert.Equal(0, starts);
        panel.IsVisible = true; ((ISliderController)slider).SendDragStarted(); panel.IsVisible = false; Assert.Equal(1, ends);
        panel.IsVisible = true; root.IsEnabled = false; ((ISliderController)slider).SendDragStarted(); Assert.Equal(1, starts);
        root.IsEnabled = true; ((ISliderController)slider).SendDragStarted(); Assert.Equal(2, starts); binding.Interrupt(); Assert.Equal(2, ends);
    }
    private sealed class Handler : IViewHandler
    {
        public IView VirtualView { get; private set; } = null!;
        IElement IElementHandler.VirtualView => VirtualView;
        public object PlatformView { get; } = new();
        public object ContainerView => PlatformView;
        public IMauiContext MauiContext => null!;
        public bool HasContainer { get; set; }
        public void SetMauiContext(IMauiContext context) { }
        public void SetVirtualView(IElement view) => VirtualView = (IView)view;
        public void DisconnectHandler() { }
        public void UpdateValue(string property) { }
        public void Invoke(string command, object? args = null) { }
        public Size GetDesiredSize(double width, double height) => new(width, height);
        public void PlatformArrange(Rect rect) { }
    }
    public void Dispose() => host.Dispose();
}
