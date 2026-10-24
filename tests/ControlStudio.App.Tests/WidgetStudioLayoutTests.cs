using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetStudioLayoutTests : IDisposable
{
    private readonly MauiTestContext host = new();
    [Fact]
    public void EveryPreviewFitsNarrowAndWidePagesWithoutCroppingOrChangingProportions()
    {
        foreach (var family in WidgetDesignRules.SupportedSizes)
        foreach (var width in new[] { 140d, 270, 360, 820 })
        {
            var frame = WidgetPreviewFrame.Fit(family, width); var canvas = WidgetDesignRules.Canvas(family);
            Assert.InRange(frame.Width, 1, Math.Min(width, canvas.Width));
            Assert.Equal(canvas.Width / canvas.Height, frame.Width / frame.Height, 10);
            Assert.True(frame.Height <= canvas.Height);
        }
        Assert.True(WidgetPreviewFrame.Fit(WidgetSize.Small, 360).Width < WidgetPreviewFrame.Fit(WidgetSize.Medium, 360).Width);
        foreach (var width in new[] { 0d, -1, double.NaN, double.PositiveInfinity }) Assert.Equal(Size.Zero, WidgetPreviewFrame.Fit(WidgetSize.Small, width));
    }
    [Fact]
    public void WidgetPreviewCornersScaleWithSavedGeometryInsteadOfBecomingPills()
    {
        foreach (var size in WidgetDesignRules.SupportedSizes)
        foreach (var measured in new[] { false, true })
        {
            var saved = size switch { WidgetSize.Small => new WidgetCanvasSize(158, 158), WidgetSize.Medium => new(338, 158),
                WidgetSize.Large => new(338, 354), _ => new(338, 550) };
            var defaultSize = WidgetDesignRules.Canvas(size);
            var dimensions = measured ? saved : new(defaultSize.Width, defaultSize.Height);
            var preview = new WidgetPreviewFrame(size, new Grid(), initialWidth: dimensions.Width, dimensions: measured ? saved : null); host.Attach(preview);
            var border = Assert.IsType<Border>(preview.Content);
            var shape = Assert.IsType<Microsoft.Maui.Controls.Shapes.RoundRectangle>(border.StrokeShape); var fullRadius = shape.CornerRadius.TopLeft;
            foreach (var width in new[] { 112d, 220d, 360d, 600d, 112d })
            {
                ((IView)preview).Arrange(new Rect(0, 0, width, preview.HeightRequest));
                var radius = shape.CornerRadius.TopLeft;
                Assert.Equal(border.WidthRequest / dimensions.Width, radius / fullRadius, 10);
                Assert.True(radius > 0 && radius < Math.Min(border.WidthRequest, border.HeightRequest) / 4);
                Assert.Equal(dimensions.Width / dimensions.Height, border.WidthRequest / border.HeightRequest, 10);
                Assert.Same(shape, border.StrokeShape);
            }
        }
    }
    [Fact]
    public void SelectedSegmentsFollowAccentChangesAndAnnounceTheirSelection()
    {
        host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.Blue);
        var control = new WidgetSegments(["Small", "Medium"], "Size", _ => { }); host.Attach(control);
        var grid = Assert.IsType<Grid>(Assert.IsType<Border>(control.Content).Content);
        var first = Assert.IsType<Grid>(grid.Children[0]); var second = Assert.IsType<Grid>(grid.Children[1]);
        var selected = Assert.IsType<AccentSurface>(first.Children[2]);
        Assert.True(selected.IsVisible);
        host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.Lime);
        Assert.Equal(Colors.Lime, Assert.IsType<SolidColorBrush>(selected.Background).Color);
        Assert.True(AppearanceColors.Contrast(selected.Foreground, Colors.Lime) >= 4.5);
        control.Update(1);
        Assert.False(selected.IsVisible); Assert.True(Assert.IsType<AccentSurface>(second.Children[2]).IsVisible);
        Assert.Equal("Medium, selected", SemanticProperties.GetDescription(Assert.IsType<Button>(second.Children[0])));
        Assert.Throws<ArgumentOutOfRangeException>(() => control.Update(2));
    }
    [Fact]
    public async Task FailedActionsRestoreControlsAndInactivePagesIgnoreActions()
    {
        var page = new ProbePage(); page.Appear();
        await page.Run((_, _) => Task.FromException(new IOException("The saved library is unavailable.")));
        Assert.True(page.Enabled); Assert.Equal("The saved library is unavailable.", Assert.Single(page.Failures));
        page.Disappear(); var invoked = false;
        await page.Run((_, _) => { invoked = true; return Task.CompletedTask; });
        Assert.False(invoked); Assert.False(page.Enabled);
    }
    [Fact]
    public async Task ReturningToPageDiscardsAnOldReadAndQueuesAFreshRead()
    {
        var oldRead = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new ProbePage();
        page.Read = _ => ++page.Reads == 1 ? oldRead.Task : Task.FromResult("current");
        page.Appear(); Assert.False(page.Enabled);
        page.Disappear(); Assert.True(page.FirstToken.IsCancellationRequested);
        page.Appear(); oldRead.SetResult("stale");
        await page.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "current" }, page.Applied); Assert.Equal(2, page.Reads);
        Assert.True(page.Enabled); Assert.Empty(page.Failures); page.Disappear();
    }
    private sealed class ProbePage : WidgetStudioPage
    {
        public Func<CancellationToken, Task<string>> Read = _ => Task.FromResult("");
        public int Reads;
        public bool Enabled;
        public CancellationToken FirstToken;
        public List<string> Applied = [], Failures = [];
        public TaskCompletionSource Completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ProbePage() : base(NullLogger.Instance) { }
        public void Appear() => OnAppearing();
        public void Disappear() => OnDisappearing();
        public Task Run(Func<CancellationToken, int, Task> action) => RunAsync(action);
        protected override async Task LoadAsync(CancellationToken token, int version)
        {
            if (Reads == 0) FirstToken = token;
            var result = await Read(token);
            if (IsCurrent(version)) Applied.Add(result);
        }
        protected override void SetActionsEnabled(bool enabled)
        {
            Enabled = enabled;
            if (enabled && Applied.Count > 0) Completed.TrySetResult();
        }
        protected override void ShowFailure(string message) => Failures.Add(message);
    }
    public void Dispose() => host.Dispose();
}
