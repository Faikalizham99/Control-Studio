using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetLayerLockTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480,
        new(-.2, .3, 1.4, .7), Zoom: 2, QuarterTurns: 1, FitPhoto: true, BlendMode: WidgetBlendMode.SoftLight, Locked: true);
    [Fact]
    public void LockedPlacementCannotChangeWhileRenderingVisibilityAndStackOrderRemainAvailable()
    {
        var locked = Photo();
        foreach (var changed in new[] { WidgetLayerEdits.Move(locked, .5, -.2), WidgetLayerEdits.Scale(locked, 2),
            WidgetLayerEdits.Resize(locked, 3, .5, .5), WidgetLayerEdits.Rotate(locked, 1, WidgetSize.Medium),
            WidgetLayerEdits.Center(locked), WidgetLayerEdits.Reset(locked, WidgetSize.Small) }) Assert.Same(locked, changed);
        var unlocked = locked with { Locked = false }; Assert.NotEqual(unlocked.Frame, WidgetLayerEdits.Move(unlocked, .2, 0).Frame);
        Assert.Equal(WidgetPhotoGeometry.Image(unlocked, 200, 150), WidgetPhotoGeometry.Image(locked, 200, 150));
        Assert.True(WidgetLayerEdits.Contains(locked, .1, .5));
        var second = locked with { Id = Guid.NewGuid(), Name = "Second", Visible = false };
        Assert.Equal(new[] { second, locked }, WidgetLayerEdits.Reorder([locked, second], locked.Id, second.Id));
    }
    [Fact]
    public void LockRoundTripsAndLegacyLayersRemainUnlockedWithoutLosingTheirEdits()
    {
        var locked = Photo(); var design = WidgetDesignRules.New() with { Layers = [locked] };
        WidgetDesignRules.Validate(design);
        var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
        Assert.Equal(locked, Assert.Single(JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!.Layers));
        using var layer = JsonDocument.Parse(JsonSerializer.Serialize(locked, WidgetDesignRules.Json));
        var legacy = layer.RootElement.EnumerateObject().Where(x => x.Name != "locked").ToDictionary(x => x.Name, x => x.Value.Clone());
        Assert.Equal(locked with { Locked = false }, JsonSerializer.Deserialize<WidgetLayer>(JsonSerializer.Serialize(legacy), WidgetDesignRules.Json));
        foreach (var invalid in new[] { "null", "1", "\"true\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.Replace("\"locked\":true", "\"locked\":" + invalid), WidgetDesignRules.JsonContext.WidgetDesign));
    }
}
