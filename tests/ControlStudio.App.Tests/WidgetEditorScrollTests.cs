using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetEditorScrollTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private sealed class LayoutProbe : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(widthConstraint, 70);
    }
    private sealed class ScrollHandler : IViewHandler
    {
        public readonly List<ScrollToRequest> Requests = [];
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
        public void Invoke(string command, object? args = null)
        {
            if (args is ScrollToRequest request)
            {
                Requests.Add(request);
                ((ScrollView)VirtualView).SetScrolledPosition(request.HorizontalOffset, request.VerticalOffset);
            }
        }
        public Size GetDesiredSize(double width, double height) => new(width, height);
        public void PlatformArrange(Rect rect) { }
    }
    private (WidgetEditorScrollView Scroll, Grid Branch, Border Row, ScrollHandler Handler) Anchored(MauiTestContext? context = null)
    {
        var row = new Border(); var branch = new Grid { Children = { row } };
        var content = new WidgetEditorForm { Children = { branch } };
        var scroll = new WidgetEditorScrollView { Content = content }; (context ?? host).Attach(scroll);
        var handler = new ScrollHandler(); scroll.Handler = handler;
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 600));
        ((IView)content).Arrange(new Rect(0, 0, 350, 2400));
        ((IView)branch).Arrange(new Rect(0, 1200, 350, 500));
        ((IView)row).Arrange(new Rect(0, 50, 350, 70)); scroll.SetScrolledPosition(0, 1100);
        return (scroll, branch, row, handler);
    }
    [Theory]
    [InlineData(900, 800)] [InlineData(1500, 1400)] [InlineData(1200, 1100)]
    public void AnchorCompensatesForContentAboveTheRowOnceWithoutRevealingIt(double nextTop, double expectedOffset)
    {
        var (scroll, branch, row, handler) = Anchored(); scroll.SetScrolledPosition(19, 1100);
        scroll.PreserveAnchor(row); ((IView)branch).Arrange(new Rect(0, nextTop, 350, 500)); scroll.CompleteReflow();
        Assert.Equal(expectedOffset, scroll.ScrollY); Assert.Equal(19, scroll.ScrollX);
        Assert.Equal(150, branch.Y + row.Y - scroll.ScrollY);
        Assert.Equal(nextTop == 1200 ? 0 : 1, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.True(request.Instant));
        scroll.CompleteReflow(); Assert.Equal(nextTop == 1200 ? 0 : 1, handler.Requests.Count);
    }
    [Theory]
    [InlineData("manual")] [InlineData("resize")] [InlineData("hidden")]
    [InlineData("removed")] [InlineData("cancel")] [InlineData("replaced")]
    public void ReflowNeverOverridesUserNavigationOrSurvivesInvalidatedGeometry(string reason)
    {
        var (scroll, branch, row, handler) = Anchored(); scroll.PreserveAnchor(row);
        switch (reason)
        {
            case "manual": scroll.SetScrolledPosition(0, 900); break;
            case "resize": ((IView)scroll).Arrange(new Rect(0, 0, 350, 400)); break;
            case "hidden": branch.IsVisible = false; break;
            case "removed": branch.Children.Remove(row); break;
            case "cancel": scroll.CancelAnchor(); break;
            case "replaced": scroll.Content = new Grid(); break;
        }
        ((IView)branch).Arrange(new Rect(0, 800, 350, 500)); scroll.CompleteReflow(); Assert.Empty(handler.Requests);
    }
    [Fact]
    public void GrowingContentAtTheBottomWaitsForTheNewExtentBeforeCorrecting()
    {
        var (scroll, branch, row, handler) = Anchored();
        ((IView)scroll.Content).Arrange(new Rect(0, 0, 350, 1700)); scroll.PreserveAnchor(row);
        ((IView)branch).Arrange(new Rect(0, 1400, 350, 500)); scroll.CompleteReflow(); Assert.Empty(handler.Requests);
        Assert.Equal(1900, scroll.Content.MinimumHeightRequest);
        ((IView)scroll.Content).Arrange(new Rect(0, 0, 350, 1900)); scroll.CompleteReflow();
        Assert.Equal(1300, scroll.ScrollY); Assert.Single(handler.Requests);
    }
    [Fact]
    public void FormCompletionRunsAfterItsChildrenAreArranged()
    {
        var row = new LayoutProbe(); var form = new WidgetEditorForm { Children = { row } }; host.Attach(form);
        var called = false;
        form.Arranged += (_, _) => { called = true; Assert.Equal(70, row.Height); Assert.True(row.Width > 0); };
        var layout = (Microsoft.Maui.ILayout)form;
        var size = layout.CrossPlatformMeasure(350, double.PositiveInfinity); layout.CrossPlatformArrange(new Rect(0, 0, 350, size.Height));
        Assert.True(called);
    }
    [Fact]
    public void ClosingAPanelKeepsTheNextVisibleSectionInPlace()
    {
        var (scroll, branch, row, handler) = Anchored();
        var next = new Border(); ((WidgetEditorForm)scroll.Content).Children.Add(next);
        ((IView)next).Arrange(new Rect(0, 1420, 350, 300)); scroll.PreserveAnchor(row);
        branch.IsVisible = false; ((IView)next).Arrange(new Rect(0, 1220, 350, 300)); scroll.CompleteReflow();
        Assert.Equal(900, scroll.ScrollY); Assert.Equal(320, next.Y - scroll.ScrollY); Assert.Single(handler.Requests);
    }
    [Fact]
    public void OffscreenControlsAndDetachedHandlersNeverQueueALaterReveal()
    {
        var (scroll, branch, row, handler) = Anchored(); scroll.SetScrolledPosition(0, 0); scroll.PreserveAnchor(row);
        ((IView)branch).Arrange(new Rect(0, 1300, 350, 500)); scroll.CompleteReflow(); Assert.Empty(handler.Requests);
        scroll.SetScrolledPosition(0, 1100); scroll.PreserveAnchor(row); scroll.Handler = null;
        ((IView)branch).Arrange(new Rect(0, 1000, 350, 500)); scroll.CompleteReflow(); scroll.Handler = handler;
        Assert.Empty(handler.Requests);
    }
    [Theory]
    [InlineData("cancel")] [InlineData("manual")] [InlineData("detach")] [InlineData("resize")]
    public void DeferredLayoutCallbacksAreInvalidatedBeforeTheyCanMoveThePage(string reason)
    {
        var queue = new Queue<Action>(); using var queuedHost = new MauiTestContext(queue);
        var (scroll, branch, row, handler) = Anchored(queuedHost); queue.Clear();
        scroll.PreserveAnchor(row); ((IView)branch).Arrange(new Rect(0, 900, 350, 500)); scroll.CompleteReflow(); Assert.NotEmpty(queue);
        switch (reason)
        {
            case "cancel": scroll.CancelAnchor(); break;
            case "manual": scroll.SetScrolledPosition(0, 1000); break;
            case "detach": scroll.Handler = null; scroll.Handler = handler; break;
            case "resize": ((IView)scroll).Arrange(new Rect(0, 0, 350, 500)); break;
        }
        while (queue.TryDequeue(out var callback)) callback();
        Assert.Empty(handler.Requests);
    }
    [Theory]
    [InlineData(320, 400, 120)] [InlineData(390, 700, 850)] [InlineData(430, 900, 1400)]
    public void ReflowKeepsTheCurrentViewportReachableWithoutRequestingAScroll(double width, double height, double offset)
    {
        var card = new Border { HeightRequest = 1200 }; var content = new VerticalStackLayout { Children = { card } };
        var scroll = new WidgetEditorScrollView { Content = content }; host.Attach(scroll);
        ((IView)scroll).Arrange(new Rect(0, 0, width, height)); scroll.SetScrolledPosition(0, offset);
        var requests = 0; scroll.ScrollToRequested += (_, _) => requests++;
        scroll.PreserveViewport(); card.HeightRequest = 100;
        Assert.Equal(offset + height, content.MinimumHeightRequest, 8);
        Assert.Equal(offset, scroll.ScrollY); Assert.Equal(0, requests);
        // The guard contributes only a minimum content height; card size/spacing stay natural.
        Assert.Equal(100, card.HeightRequest); Assert.Equal(-1, content.HeightRequest);
        scroll.PreserveViewport(); Assert.Equal(offset + height, content.MinimumHeightRequest, 8); Assert.Equal(0, requests);
    }
    [Fact]
    public void ManualScrollingReturnsTemporaryTailSpaceWithoutScrollingBackOrLosingOriginalMinimum()
    {
        var content = new VerticalStackLayout { MinimumHeightRequest = 40 };
        var scroll = new WidgetEditorScrollView { Content = content }; host.Attach(scroll);
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 600)); scroll.SetScrolledPosition(0, 700); scroll.PreserveViewport();
        Assert.Equal(1300, content.MinimumHeightRequest);
        var requests = 0; scroll.ScrollToRequested += (_, _) => requests++;
        scroll.SetScrolledPosition(0, 300); Assert.Equal(900, content.MinimumHeightRequest); Assert.Equal(300, scroll.ScrollY);
        scroll.SetScrolledPosition(0, 0); Assert.Equal(40, content.MinimumHeightRequest); Assert.Equal(0, requests);
        scroll.SetScrolledPosition(0, 200); scroll.PreserveViewport(); Assert.Equal(800, content.MinimumHeightRequest);
        scroll.ReleaseViewport(); Assert.Equal(40, content.MinimumHeightRequest); Assert.Equal(0, requests);
    }
    [Fact]
    public void ResizeContentReplacementAndRemovalReleaseTheOldConstraint()
    {
        var content = new VerticalStackLayout { MinimumHeightRequest = 20 };
        var scroll = new WidgetEditorScrollView { Content = content }; host.Attach(scroll);
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 600)); scroll.SetScrolledPosition(0, 500); scroll.PreserveViewport();
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 400)); Assert.Equal(20, content.MinimumHeightRequest);
        scroll.PreserveViewport(); var replacement = new VerticalStackLayout { MinimumHeightRequest = 25 }; scroll.Content = replacement;
        Assert.Equal(20, content.MinimumHeightRequest); Assert.Equal(25, replacement.MinimumHeightRequest);
        scroll.PreserveViewport(); host.Attach(new Grid()); Assert.Equal(25, replacement.MinimumHeightRequest);
    }
    [Fact]
    public void MissingGeometryAndBounceDoNotCreateInvalidHeightAndExistingMinimumIsRetained()
    {
        var content = new VerticalStackLayout { MinimumHeightRequest = 1200 }; var scroll = new WidgetEditorScrollView { Content = content }; host.Attach(scroll);
        scroll.PreserveViewport(); Assert.Equal(1200, content.MinimumHeightRequest);
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 600)); scroll.SetScrolledPosition(0, -20); scroll.PreserveViewport(); Assert.Equal(1200, content.MinimumHeightRequest);
        scroll.SetScrolledPosition(0, 100); scroll.PreserveViewport(); Assert.Equal(1200, content.MinimumHeightRequest);
        scroll.ReleaseViewport(); Assert.Equal(1200, content.MinimumHeightRequest);
    }
    public void Dispose() => host.Dispose();
}
