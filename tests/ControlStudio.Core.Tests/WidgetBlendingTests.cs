using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetBlendingTests
{
    private static WidgetLayer Photo(WidgetBlendMode mode = WidgetBlendMode.Normal) => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 600, 400,
        new(-.3, .1, 1.5, .7), QuarterTurns: 1, FlipHorizontal: true, Opacity: .45, FitPhoto: true, BlendMode: mode);
    public static IEnumerable<object[]> Modes => WidgetBlending.Modes.Select(x => new object[] { x });
    [Theory, MemberData(nameof(Modes))]
    public void EveryModeRoundTripsWithItsOtherPhotoEdits(WidgetBlendMode mode)
    {
        var draft = WidgetDesignRules.New() with { Layers = [Photo(mode)] };
        WidgetDesignRules.Validate(draft);
        var json = JsonSerializer.Serialize(draft, WidgetDesignRules.JsonContext.WidgetDesign);
        var restored = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
        Assert.Equal(draft.Layers, restored.Layers);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.String, parsed.RootElement.GetProperty("layers")[0].GetProperty("blendMode").ValueKind);
        Assert.InRange(WidgetBlending.Group(mode), 0, WidgetBlending.Groups.Count - 1);
        Assert.False(string.IsNullOrWhiteSpace(WidgetBlending.Name(mode)));
    }
    [Fact]
    public void OldCatalogsUseNormalWithoutLosingOpacityOrPhotoTransforms()
    {
        var photo = Photo(); var json = JsonSerializer.Serialize(photo, WidgetDesignRules.Json);
        using var doc = JsonDocument.Parse(json);
        var fields = doc.RootElement.EnumerateObject().Where(x => x.Name != "blendMode").ToDictionary(x => x.Name, x => x.Value.Clone());
        var restored = JsonSerializer.Deserialize<WidgetLayer>(JsonSerializer.Serialize(fields), WidgetDesignRules.Json)!;
        Assert.Equal(photo, restored); Assert.Equal(WidgetBlendMode.Normal, restored.BlendMode);
    }
    [Theory]
    [InlineData("\"futureMode\"")] [InlineData("999")] [InlineData("null")]
    public void UnknownModesAreRejectedRatherThanRenderedAsNormal(string value)
    {
        var draft = WidgetDesignRules.New() with { Layers = [Photo()] };
        var json = JsonSerializer.Serialize(draft, WidgetDesignRules.JsonContext.WidgetDesign).Replace("\"blendMode\":\"normal\"", "\"blendMode\":" + value);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign));
    }
    [Fact]
    public void InvalidInMemoryModeIsRejectedAndResetRestoresNormal()
    {
        var photo = Photo((WidgetBlendMode)999);
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [photo] }));
        Assert.Equal(WidgetBlendMode.Normal, WidgetLayerEdits.Reset(photo, WidgetSize.Small).BlendMode);
    }
    [Fact]
    public void OrdinaryTransformsRetainTheChosenMode()
    {
        var layer = Photo(WidgetBlendMode.SoftLight);
        foreach (var next in new[] { WidgetLayerEdits.Move(layer, .2, -.2), WidgetLayerEdits.Scale(layer, 1.3),
            WidgetLayerEdits.Resize(layer, 3, .2, .1), WidgetLayerEdits.Rotate(layer, 1, WidgetSize.Medium), WidgetLayerEdits.Center(layer) })
            Assert.Equal(layer.BlendMode, next.BlendMode);
    }
}
