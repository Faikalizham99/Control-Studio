using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetBatteryDrawingTests
{
    [Fact]
    public void ShapesKeepFiniteArtworkAndUnknownNeverDrawsAZeroPercentFillOrChargingMark()
    {
        foreach (var shape in Enum.GetValues<WidgetBatteryShape>())
        {
            var options = WidgetBatteries.Defaults() with { Shape = shape, ShowPercentage = false };
            foreach (var reading in new[] { new WidgetBatteryReading(), new(0), new(1), new(50, true), new(100, true, true) })
            {
                var drawing = new Recording(); WidgetBatteryDrawing.Draw(drawing, options, reading);
                Assert.All(drawing.Points, p => { Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y)); Assert.InRange(p.X, 0, 160); Assert.InRange(p.Y, 0, 100); });
                if (!reading.Known) { Assert.Contains("—", drawing.Texts); Assert.DoesNotContain("0%", drawing.Texts); Assert.All(drawing.Colours, colour => Assert.Equal(Color.FromArgb("#8E8E93").Red, colour.Red)); }
                if (shape == WidgetBatteryShape.Percentage) Assert.Contains(reading.Text, drawing.Texts);
                else if (reading.Known) Assert.Empty(drawing.Texts);
            }
        }
    }
    [Fact]
    public void PercentageAndChargingSwitchesAffectOnlyTheirOwnArtwork()
    {
        var options = WidgetBatteries.Defaults(); var baseline = new Recording(); WidgetBatteryDrawing.Draw(baseline, options, new(50));
        Assert.Contains("50%", baseline.Texts);
        var charging = new Recording(); WidgetBatteryDrawing.Draw(charging, options, new(50, true)); Assert.Equal(baseline.Paths + 1, charging.Paths);
        var full = new Recording(); WidgetBatteryDrawing.Draw(full, options, new(100, true, true)); Assert.Equal(baseline.Paths + 1, full.Paths);
        var hidden = new Recording(); WidgetBatteryDrawing.Draw(hidden, options with { ShowPercentage = false, ShowCharging = false }, new(50, true));
        Assert.Empty(hidden.Texts); Assert.Equal(baseline.Paths, hidden.Paths);
    }
    private sealed class Recording : IWidgetBatteryCanvas
    {
        public List<PointF> Points { get; } = []; public List<string> Texts { get; } = []; public List<Color> Colours { get; } = []; public int Paths { get; private set; }
        public void Rectangle(float x, float y, float width, float height, float radius, Color colour, bool fill, float stroke)
        { Assert.True(width > 0 && height > 0); Assert.InRange(radius, 0, Math.Min(width, height) / 2); Points.Add(new(x, y)); Points.Add(new(x + width, y + height)); Colours.Add(colour); }
        public void Path(IReadOnlyList<PointF> points, Color colour, float stroke, bool fill) { Points.AddRange(points); Colours.Add(colour); Paths++; }
        public void Text(string text, float x, float y, float width, float height, float size, Color colour) { Texts.Add(text); Colours.Add(colour); }
    }
}
