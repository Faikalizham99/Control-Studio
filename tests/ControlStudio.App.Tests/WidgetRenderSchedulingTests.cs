using ControlStudio.App.Rendering;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetRenderSchedulingTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 28, 12, 30, 0, TimeSpan.Zero);
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 100, 200, new());
    private static WidgetRenderIdentity Key(WidgetDesign design, DateTimeOffset? time = null, WidgetBatteryReading battery = default) => WidgetRenderIdentity.Create(design, time ?? Time, battery);
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(30)]
    public void NamesLocksAndSavedMetadataDoNotRedrawUnchangedPixels(int count)
    {
        var design = WidgetDesignRules.New() with { Layers = Enumerable.Range(0, count).Select(_ => Photo()).ToArray() };
        var changed = design with { Id = Guid.NewGuid(), Name = "Renamed", RenderKey = Guid.NewGuid().ToString("N"), Revision = 2,
            Layers = design.Layers.Select(x => x with { Name = "Changed", Locked = true }).ToArray() };
        Assert.True(Key(design).SamePixels(Key(changed)));
    }
    [Fact]
    public void EveryRenderedLayerPropertyAndOrderingStillInvalidates()
    {
        var photo = Photo(); var design = WidgetDesignRules.New() with { Layers = [photo, Photo()] }; var key = Key(design);
        foreach (var layer in new[] { photo with { Frame = photo.Frame with { X = .1 } }, photo with { Zoom = 2 }, photo with { CropX = .2 }, photo with { CropY = .2 },
            photo with { Opacity = .5 }, photo with { Visible = false }, photo with { RotationDegrees = 24 }, photo with { QuarterTurns = 1 },
            photo with { FlipHorizontal = true }, photo with { FlipVertical = true }, photo with { FitPhoto = true }, photo with { BlendMode = WidgetBlendMode.Multiply },
            photo with { AssetKey = Guid.NewGuid().ToString("N") }, photo with { PixelWidth = 80 } })
            Assert.False(key.SamePixels(Key(design with { Layers = [layer, design.Layers[1]] })));
        Assert.False(key.SamePixels(Key(design with { Layers = design.Layers.Reverse().ToArray() })));
        Assert.False(key.SamePixels(Key(design with { BackgroundHex = "#FF0000" })));
        Assert.False(key.SamePixels(Key(design with { TransparentBackground = false })));
        Assert.False(key.SamePixels(Key(design with { Size = WidgetSize.Medium })));
        Assert.False(key.SamePixels(Key(design with { CanvasDimensions = new(158, 158) })));
        Assert.False(key.SamePixels(null));
    }
    [Fact]
    public void HiddenAndZeroOpacityLayersDoNotRedrawButAreStillValidated()
    {
        var photo = Photo(); var clock = WidgetClocks.New() with { Visible = false };
        var design = WidgetDesignRules.New() with { Layers = [photo, clock] };
        Assert.True(Key(design).SamePixels(Key(design with { Layers = [photo] })));
        Assert.True(Key(design).SamePixels(Key(design with { Layers = [photo, clock with { Visible = true, Opacity = 0 }] })));
        Assert.Throws<IOException>(() => Key(design with { Layers = [photo, clock with { RotationDegrees = double.NaN }] }));
        Assert.Throws<IOException>(() => Key(design with { Name = "" }));
    }
    [Fact]
    public void SecondsMinutesAndDatesRedrawOnlyWhenTheirDisplayedTextChanges()
    {
        var clock = WidgetClocks.New(); var design = WidgetDesignRules.New() with { Layers = [clock] };
        Assert.True(Key(design).SamePixels(Key(design, Time.AddMilliseconds(500))));
        Assert.False(Key(design).SamePixels(Key(design, Time.AddSeconds(1))));
        design = design with { Layers = [clock with { Clock = clock.Clock! with { ShowSeconds = false } }] };
        Assert.True(Key(design).SamePixels(Key(design, Time.AddSeconds(1))));
        Assert.False(Key(design).SamePixels(Key(design, Time.AddMinutes(1))));
        design = design with { Layers = [WidgetDates.New()] };
        Assert.True(Key(design).SamePixels(Key(design, Time.AddMinutes(1))));
        Assert.False(Key(design).SamePixels(Key(design, Time.AddDays(1))));
    }
    [Theory]
    [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)]
    public void TextStylesGradientsAndContentRemainPixelInputs(WidgetLayerKind kind)
    {
        var layer = kind switch { WidgetLayerKind.Clock => WidgetClocks.New(), WidgetLayerKind.Date => WidgetDates.New(), _ => WidgetTexts.New() };
        var design = WidgetDesignRules.New() with { Layers = [layer] };
        var changed = WidgetTextLayers.WithStyle(layer, WidgetTextLayers.Style(layer) with { Gradient = new(true, "#FF0000", "#00FF00", "#0000FF") });
        Assert.False(Key(design).SamePixels(Key(design with { Layers = [changed] })));
        if (kind == WidgetLayerKind.Text)
            Assert.False(Key(design).SamePixels(Key(design with { Layers = [layer with { Text = layer.Text! with { Content = "Changed" } }] })));
    }
    [Fact]
    public void BatteryLevelAndChargeStateInvalidateOnlyDesignsWithVisibleBatteries()
    {
        var design = WidgetDesignRules.New() with { Layers = [WidgetBatteries.New()] };
        Assert.False(Key(design, battery: new(30)).SamePixels(Key(design, battery: new(31))));
        Assert.False(Key(design, battery: new(30)).SamePixels(Key(design, battery: new(30, true))));
        design = design with { Layers = [design.Layers[0] with { Visible = false }] };
        Assert.True(Key(design, battery: new(30)).SamePixels(Key(design, battery: new(31))));
    }
    [Fact]
    public void BurstRendersLatestDraftOnceThenAcceptsNextFrame()
    {
        var posted = new Queue<Action>(); var current = 0; var rendered = new List<int>();
        var queue = new WidgetRenderQueue(posted.Enqueue, () => rendered.Add(current));
        for (var i = 1; i <= 100; i++) { current = i; queue.Request(); }
        Assert.Single(posted); posted.Dequeue()(); Assert.Equal(new[] { 100 }, rendered);
        current = 101; queue.Request(); posted.Dequeue()(); Assert.Equal(new[] { 100, 101 }, rendered);
    }
    [Fact]
    public void UnloadCancelCannotResurrectAnOldRenderOrCancelANewOne()
    {
        var posted = new Queue<Action>(); var calls = 0; var queue = new WidgetRenderQueue(posted.Enqueue, () => calls++);
        queue.Request(); queue.Cancel(); queue.Request();
        posted.Dequeue()(); Assert.Equal(0, calls); queue.Request(); Assert.Single(posted);
        posted.Dequeue()(); Assert.Equal(1, calls);
        queue.Request(); queue.Cancel(); posted.Dequeue()(); Assert.Equal(1, calls);
    }
    [Fact]
    public void RequestsDuringRenderAreNotLostAndFailedPostsCanRetry()
    {
        var posted = new Queue<Action>(); var calls = 0; var failPost = true; WidgetRenderQueue queue = null!;
        queue = new(action => { if (failPost) throw new IOException(); posted.Enqueue(action); }, () => { if (++calls == 1) queue.Request(); });
        Assert.Throws<IOException>(queue.Request); failPost = false; queue.Request();
        posted.Dequeue()(); Assert.Single(posted); posted.Dequeue()(); Assert.Equal(2, calls);
    }
}
