using System.Text.Json;
using System.Text.Json.Nodes;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetClockWeightTests
{
    [Theory]
    [InlineData(WidgetClockFont.System, WidgetClockWeight.Semibold)] [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Semibold)]
    [InlineData(WidgetClockFont.Rounded, WidgetClockWeight.Bold)] [InlineData(WidgetClockFont.Monospaced, WidgetClockWeight.Bold)]
    [InlineData(WidgetClockFont.Serif, WidgetClockWeight.Bold)] [InlineData(WidgetClockFont.RocknRollOne, WidgetClockWeight.Regular)]
    [InlineData(WidgetClockFont.DelaGothicOne, WidgetClockWeight.Regular)] [InlineData(WidgetClockFont.KaiseiDecol, WidgetClockWeight.Regular)]
    public void MissingWeightRetainsEachLegacyFace(WidgetClockFont font, WidgetClockWeight expected)
    {
        var clock = WidgetClocks.New() with { Clock = new(Font: font) };
        var json = JsonNode.Parse(JsonSerializer.Serialize(clock, WidgetDesignRules.Json))!;
        json["clock"]!.AsObject().Remove("weight");
        var restored = JsonSerializer.Deserialize<WidgetLayer>(json.ToJsonString(), WidgetDesignRules.Json)!;
        Assert.Null(restored.Clock!.Weight); Assert.Equal(expected, WidgetClockWeights.Resolve(restored.Clock)); WidgetClocks.Validate(restored.Clock);
    }
    [Theory]
    [InlineData(WidgetClockFont.System, WidgetClockWeight.Light, WidgetClockFont.SfProRounded, WidgetClockWeight.Light)]
    [InlineData(WidgetClockFont.System, WidgetClockWeight.Heavy, WidgetClockFont.Monospaced, WidgetClockWeight.Bold)]
    [InlineData(WidgetClockFont.Monospaced, WidgetClockWeight.Regular, WidgetClockFont.Serif, WidgetClockWeight.Regular)]
    [InlineData(WidgetClockFont.Monospaced, WidgetClockWeight.Bold, WidgetClockFont.KaiseiDecol, WidgetClockWeight.Regular)]
    public void ChangingFontKeepsCompatibleWeightAndUsesRealDefaultsOtherwise(WidgetClockFont before, WidgetClockWeight weight, WidgetClockFont after, WidgetClockWeight expected)
    {
        var clock = new WidgetClockOptions(Font: before, Weight: weight, ShowAmPm: false, ColourHex: "#12ABEF");
        var changed = WidgetClockWeights.ChangeFont(clock, after); WidgetClocks.Validate(changed);
        Assert.Equal(expected, WidgetClockWeights.Resolve(changed)); Assert.Equal(clock.ShowAmPm, changed.ShowAmPm); Assert.Equal(clock.ColourHex, changed.ColourHex);
        Assert.Equal(weight, clock.Weight);
    }
    [Theory]
    [InlineData("42")] [InlineData("true")] [InlineData("\"imaginary\"")]
    public void InvalidWeightWireValuesAreRejected(string value)
    {
        var json = JsonSerializer.Serialize(WidgetClocks.New(), WidgetDesignRules.Json).Replace("\"weight\":null", "\"weight\":" + value);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetLayer>(json, WidgetDesignRules.Json));
    }
}
