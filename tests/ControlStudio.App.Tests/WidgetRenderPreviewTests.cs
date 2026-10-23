using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetRenderPreviewTests : IDisposable
{
    private readonly MauiTestContext host = new();
    [Fact]
    public void FullRenderLoadsOnDemandAndReleasesWithoutRestartingForUnchangedVisibility()
    {
        var preview = new WidgetRenderPreview("/private/renders/full.png", Colors.Black); host.Attach(preview);
        var image = Assert.IsType<Image>(preview.Content); Assert.Null(image.Source); Assert.Equal(Aspect.AspectFit, image.Aspect);
        preview.SetVisible(true); var source = Assert.IsType<FileImageSource>(image.Source);
        Assert.Equal("/private/renders/full.png", source.File);
        preview.SetVisible(true); Assert.Same(source, image.Source);
        preview.SetVisible(false); Assert.Null(image.Source);
        preview.SetVisible(true); Assert.Equal(source.File, Assert.IsType<FileImageSource>(image.Source).File);
        preview.Dispose(); Assert.Null(image.Source);
        preview.SetVisible(true); Assert.Null(image.Source); preview.Dispose();
        Assert.True(preview.InputTransparent); Assert.False(AutomationProperties.GetIsInAccessibleTree(preview));
    }
    [Fact]
    public void MissingRenderKeepsReadableGuidanceAndDoesNotFallBackToAnEnlargedThumbnail()
    {
        foreach (var background in new[] { Colors.Black, Colors.White, Colors.Cyan })
        {
            var preview = new WidgetRenderPreview(null, background);
            var label = Assert.IsType<Label>(preview.Content); Assert.Contains("Open Edit", label.Text);
            preview.SetVisible(true); preview.SetVisible(false); Assert.Same(label, preview.Content);
            Assert.True(AppearanceColors.Contrast(label.TextColor, background) >= 4.5); preview.Dispose();
        }
    }
    [Fact]
    public void ScrollingChangesOnlyTheVisibleWindowEvenForAFiftyDesignLibrary()
    {
        var cards = Enumerable.Range(0, 50).Select(i => new Rect(0, i * 400, 300, 300)).ToArray();
        Assert.Equal(new[] { 0, 1 }, WidgetPreviewWindow.Select(cards, 0, 500).Order().ToArray());
        Assert.Equal(new[] { 1, 2 }, WidgetPreviewWindow.Select(cards, 400, 600).Order().ToArray());
        Assert.Equal(new[] { 25, 26 }, WidgetPreviewWindow.Select(cards, 10_000, 500).Order().ToArray());
        foreach (var invalid in new[] { 0d, -1, double.NaN, double.PositiveInfinity }) Assert.Empty(WidgetPreviewWindow.Select(cards, 0, invalid));
        Assert.Empty(WidgetPreviewWindow.Select(cards, double.NaN, 500));
        Assert.Empty(WidgetPreviewWindow.Select([Rect.Zero, new Rect(0, double.NaN, 100, 100)], 0, 500));
    }
    [Fact]
    public void IntermediateLayoutsStayBoundedAndOnScreenCardsWinOverPrefetchCandidates()
    {
        var overlapping = Enumerable.Repeat(new Rect(0, 0, 300, 300), 50).ToArray();
        Assert.Equal(WidgetPreviewWindow.MaximumLoaded, WidgetPreviewWindow.Select(overlapping, 0, 600).Count);
        var candidates = Enumerable.Range(0, 6).Select(i => new Rect(0, -60 + i * 8, 300, 6))
            .Append(new Rect(0, 10, 300, 300)).ToArray();
        var selected = WidgetPreviewWindow.Select(candidates, 0, 500);
        Assert.Equal(WidgetPreviewWindow.MaximumLoaded, selected.Count); Assert.Contains(6, selected);
        Assert.Single(WidgetPreviewWindow.Select([new Rect(0, 0, 300, 300)], -100, 100));
    }
    public void Dispose() => host.Dispose();
}
