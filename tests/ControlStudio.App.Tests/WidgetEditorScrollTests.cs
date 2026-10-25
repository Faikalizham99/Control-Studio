using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetEditorScrollTests : IDisposable
{
    private readonly MauiTestContext host = new();
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
