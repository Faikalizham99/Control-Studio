using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using System.Text.Json;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetBatteryDrawingTests
{
    [Fact]
    public void EveryShapeCentresOneLabelInTheSharedNativeBoxIncludingZeroFullAndUnknown()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-battery-labels-v1.example.json")));
        foreach (var row in fixture.RootElement.GetProperty("labels").EnumerateArray())
        {
            var shape = Enum.Parse<WidgetBatteryShape>(row.GetProperty("shape").GetString()!, true);
            var expected = new Rect(row.GetProperty("x").GetDouble(), row.GetProperty("y").GetDouble(), row.GetProperty("width").GetDouble(), row.GetProperty("height").GetDouble());
            foreach (var percent in new int?[] { null, 0, 1, 50, 99, 100 })
            {
                var drawing = new Recording(); WidgetBatteryDrawing.Draw(drawing, WidgetBatteries.Defaults() with { Shape = shape }, new(percent));
                var label = Assert.Single(drawing.Labels); Assert.Equal(expected, label.Bounds); Assert.Equal(row.GetProperty("fontSize").GetSingle(), label.FontSize);
                Assert.InRange(label.Bounds.Top, 0, 50); Assert.InRange(label.Bounds.Bottom, 0, 100);
                if (shape != WidgetBatteryShape.Percentage) Assert.NotNull(label.Outline);
                else Assert.Null(label.Outline);
            }
        }
    }
    [Fact]
    public void EmbeddedNumbersChooseReadableInkWithoutChangingTheBatteryColour()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-battery-labels-v1.example.json")));
        foreach (var row in fixture.RootElement.GetProperty("contrast").EnumerateArray())
        {
            var hex = row.GetProperty("colourHex").GetString()!; var colour = Color.FromArgb(hex);
            var options = WidgetBatteries.Defaults() with { Stops = [new(0, hex), new(100, hex)] };
            var drawing = new Recording(); WidgetBatteryDrawing.Draw(drawing, options, new(75));
            var label = Assert.Single(drawing.Labels); Assert.Equal(row.GetProperty("white").GetBoolean() ? Colors.White : Colors.Black, label.Ink);
            Assert.True(AppearanceColors.Contrast(colour, label.Ink) >= 4.5);
            Assert.Equal(21, AppearanceColors.Contrast(label.Ink, label.Outline!), 8);
            Assert.Contains(colour, drawing.Colours);
            var number = new Recording(); WidgetBatteryDrawing.Draw(number, options with { Shape = WidgetBatteryShape.Percentage }, new(75));
            Assert.Equal(colour, Assert.Single(number.Labels).Ink); Assert.Null(number.Labels[0].Outline);
        }
    }
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
        public List<(Rect Bounds, float FontSize, Color Ink, Color? Outline)> Labels { get; } = [];
        public void Rectangle(float x, float y, float width, float height, float radius, Color colour, bool fill, float stroke)
        { Assert.True(width > 0 && height > 0); Assert.InRange(radius, 0, Math.Min(width, height) / 2); Points.Add(new(x, y)); Points.Add(new(x + width, y + height)); Colours.Add(colour); }
        public void Path(IReadOnlyList<PointF> points, Color colour, float stroke, bool fill) { Points.AddRange(points); Colours.Add(colour); Paths++; }
        public void Text(string text, float x, float y, float width, float height, float size, Color colour, Color? outline)
        { Texts.Add(text); Labels.Add((new(x, y, width, height), size, colour, outline)); }
    }
}
