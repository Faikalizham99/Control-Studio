using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetLayerNudgeTests
{
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)]
    [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void EveryKindMovesOneWidgetPointOnBothAxesWithoutChangingOtherSettings(WidgetSize size)
    {
        foreach (var dimensions in new WidgetCanvasSize?[] { null, WidgetCanvasSizes.Estimate(size, 390, 844).Canvas })
        foreach (var layer in new[]
        {
            new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(),
                Zoom: 2, CropX: .3, QuarterTurns: 1, FlipHorizontal: true, BlendMode: WidgetBlendMode.SoftLight),
            WidgetClocks.New() with { Clock = new(ShowAmPm: false) },
            WidgetDates.New() with { Date = new(ShowWeekday: true, WeekdayStyle: WidgetWeekdayStyle.Full) }
        })
        {
            var source = layer with { Frame = new(0, 0, .4, .6), Visible = false, Opacity = .6 };
            var canvas = dimensions ?? new WidgetCanvasSize(WidgetDesignRules.Canvas(size).Width, WidgetDesignRules.Canvas(size).Height);
            foreach (var direction in Enum.GetValues<WidgetMoveDirection>())
            {
                var moved = WidgetLayerEdits.Nudge(source, direction, size, dimensions);
                var x = direction == WidgetMoveDirection.Left ? -1 : direction == WidgetMoveDirection.Right ? 1 : 0;
                var y = direction == WidgetMoveDirection.Up ? -1 : direction == WidgetMoveDirection.Down ? 1 : 0;
                Assert.Equal(x, (moved.Frame.X - source.Frame.X) * canvas.Width, 10);
                Assert.Equal(y, (moved.Frame.Y - source.Frame.Y) * canvas.Height, 10);
                Assert.Equal(source with { Frame = moved.Frame }, moved);
                Assert.Equal(source.Frame.Width, moved.Frame.Width); Assert.Equal(source.Frame.Height, moved.Frame.Height);
                WidgetDesignRules.Validate(WidgetDesignRules.New() with { Size = size, CanvasDimensions = dimensions, Layers = [moved] });
            }
        }
    }

    [Fact]
    public void RepeatedTapsAreReversibleAndRespectLocksAndWorkspaceLimits()
    {
        var source = WidgetClocks.New() with { Frame = new(0, 0, .4, .3) }; var moved = source;
        for (var i = 0; i < 100; i++) moved = WidgetLayerEdits.Nudge(moved, WidgetMoveDirection.Left, WidgetSize.Medium);
        Assert.Equal(-100d / 364, moved.Frame.X, 10);
        for (var i = 0; i < 100; i++) moved = WidgetLayerEdits.Nudge(moved, WidgetMoveDirection.Right, WidgetSize.Medium);
        Assert.Equal(source.Frame.X, moved.Frame.X, 10);
        foreach (var direction in Enum.GetValues<WidgetMoveDirection>())
        {
            var locked = source with { Locked = true }; Assert.Same(locked, WidgetLayerEdits.Nudge(locked, direction, WidgetSize.Small));
            foreach (var offset in new[] { -4d, 4d })
            {
                var edge = source with { Frame = source.Frame with { X = offset, Y = offset } };
                var result = WidgetLayerEdits.Nudge(edge, direction, WidgetSize.Small);
                WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [result] });
                Assert.InRange(result.Frame.X, -4, 4); Assert.InRange(result.Frame.Y, -4, 4);
            }
        }
        Assert.Same(source, WidgetLayerEdits.Nudge(source, (WidgetMoveDirection)999, WidgetSize.Small));
        Assert.Same(source, WidgetLayerEdits.Nudge(source, WidgetMoveDirection.Up, WidgetSize.Medium, new(0, 170)));
        Assert.Same(source, WidgetLayerEdits.Nudge(source, WidgetMoveDirection.Up, WidgetSize.Small, new(double.NaN, 170)));
    }
}
