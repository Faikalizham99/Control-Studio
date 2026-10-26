using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetTextTests
{
    [Fact]
    public void SharedTextFixtureUsesStaticRenderingAndKeepsTheCatalogSchema()
    {
        var catalog = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-text-v1.example.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        WidgetDesignRules.Validate(catalog); var design = Assert.Single(catalog.Designs); var text = Assert.Single(design.Layers);
        Assert.Equal(1, catalog.SchemaVersion); Assert.Equal("Hello\n世界 🌸", text.Text!.Content); Assert.False(WidgetScenes.IsLive(design));
        Assert.Single(WidgetScenes.Steps(design));
    }
    [Fact]
    public void ContentSupportsMultilineUnicodeAndNormalizesWithoutSplittingGraphemes()
    {
        Assert.Equal("Hello\n日本語 🌸\nA\tB", WidgetTexts.NormalizeContent("Hello\r\n日本語 🌸\rA\tB\0"));
        Assert.Equal("", WidgetTexts.NormalizeContent(null));
        Assert.Equal(new string('a', 499), WidgetTexts.NormalizeContent(new string('a', 499) + "🌸"));
        Assert.Equal(new string('a', 498) + "e\u0301", WidgetTexts.NormalizeContent(new string('a', 498) + "e\u0301x"));
        Assert.Equal(new string('a', 499), WidgetTexts.NormalizeContent(new string('a', 499) + "e\u0301"));
        Assert.Equal("�", WidgetTexts.NormalizeContent("\ud800"));
        Assert.Equal(500, WidgetTexts.NormalizeContent(new string('a', 501)).Length);
    }

    [Fact]
    public void AppearanceIsSharedWithoutClockOrDateSwitchesAndRoundTripsEveryFontAndWeight()
    {
        foreach (var font in Enum.GetValues<WidgetClockFont>())
        foreach (var weight in WidgetClockWeights.Available(font))
        {
            var layer = WidgetTexts.New() with { Text = new("Hello\n世界 🌸", font, "#12ABEF", .6, WidgetClockAlignment.Right, weight) };
            var design = WidgetDesignRules.New() with { Layers = [layer] }; WidgetDesignRules.Validate(design);
            var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
            var restored = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
            Assert.Equal(layer, restored.Layers.Single()); Assert.DoesNotContain("showSeconds", json); Assert.DoesNotContain("showWeekday", json);
            var changed = WidgetTextLayers.WithStyle(layer, WidgetTextLayers.Style(layer) with { Alignment = WidgetClockAlignment.Left, FontScale = .4 });
            Assert.Equal(layer.Text.Content, changed.Text!.Content); Assert.Equal(.4, changed.Text.FontScale); Assert.Equal(WidgetClockAlignment.Left, changed.Text.Alignment);
            Assert.Null(changed.Date); Assert.Null(changed.Clock); Assert.Equal("text", WidgetTextLayers.Name(layer)); Assert.True(WidgetTextLayers.IsText(layer));
            Assert.False(WidgetTextLayers.IsLive(layer)); Assert.Equal(layer.Text.Content, WidgetTextLayers.Text(layer, DateTimeOffset.MinValue));
        }
        var old = JsonSerializer.Serialize(WidgetClocks.New(), WidgetDesignRules.Json);
        Assert.DoesNotContain("\"text\":", old); Assert.Null(JsonSerializer.Deserialize<WidgetLayer>(old, WidgetDesignRules.Json)!.Text);
    }

    [Fact]
    public void InvalidContentAppearanceAndMixedPayloadsCannotBeSaved()
    {
        var text = WidgetTexts.New(); var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 1, 1, new());
        foreach (var invalid in new[]
        {
            text with { Text = null }, text with { Clock = new() }, text with { Date = new() }, photo with { Text = new() },
            WidgetClocks.New() with { Text = new() }, WidgetDates.New() with { Text = new() },
            text with { Text = new(Content: null!) }, text with { Text = new(new string('x', 501)) },
            text with { Text = new("bad\0") }, text with { Text = new("bad\ud800") },
            text with { Text = new(ColourHex: "#bad") }, text with { Text = new(FontScale: double.NaN) },
            text with { Text = new(Font: WidgetClockFont.Rounded, Weight: WidgetClockWeight.Light) },
            text with { BlendMode = WidgetBlendMode.Multiply }, text with { FlipVertical = true }, text with { AssetKey = photo.AssetKey }
        }) Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [invalid] }));
        WidgetTexts.Validate(new("")); WidgetTexts.Validate(new("\n\t"));
    }

    [Fact]
    public void StaticTextGroupsKeepTheirStackingAroundLiveClockAndDateLayers()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 1, 1, new());
        var text = WidgetTexts.New(); var clock = WidgetClocks.New(); var date = WidgetDates.New();
        var top = text with { Id = Guid.NewGuid(), Name = "Top", Text = text.Text! with { Content = "Above" } };
        var design = WidgetDesignRules.New() with { Layers = [photo, text, clock, top, date] };
        var steps = WidgetScenes.Steps(design);
        Assert.Equal(4, steps.Count); Assert.Equal(new[] { photo, text }, steps[0].Photos); Assert.Equal(clock, steps[1].TextLayer);
        Assert.Equal(new[] { top }, steps[2].Photos); Assert.Equal(date, steps[3].TextLayer);
        var scene = WidgetScenes.Manifest(design); Assert.Equal(2, scene.SchemaVersion); WidgetScenes.Validate(scene, design);
        Assert.Equal(new int?[] { 0, null, 2, null }, scene.Parts.Select(x => x.ImageIndex));
        var textOnly = design with { Layers = [photo, text, top] }; Assert.False(WidgetScenes.IsLive(textOnly)); Assert.Single(WidgetScenes.Steps(textOnly));
        Assert.False(WidgetScenes.IsLive(design with { Layers = [text, clock with { Visible = false }, date with { Opacity = 0 }] }));
        var hiddenText = design with { Layers = [text with { Visible = false }, clock] };
        Assert.Equal(clock, Assert.Single(WidgetScenes.Steps(hiddenText)).TextLayer);
    }

    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void TextSharesCenteredPinchesPositionArrowsLocksAndIndependentDuplicates(WidgetSize size)
    {
        var text = WidgetTexts.New() with { Frame = new(-.2, .5, .7, .3), Text = new("Line one\nLine two") };
        var resized = WidgetLayerEdits.ScaleCentered(text, 1.5);
        Assert.Equal(text.Frame.X + text.Frame.Width / 2, resized.Frame.X + resized.Frame.Width / 2, 10);
        Assert.Equal(text.Frame.Y + text.Frame.Height / 2, resized.Frame.Y + resized.Frame.Height / 2, 10);
        var nudged = WidgetLayerEdits.Nudge(resized, WidgetMoveDirection.Right, size);
        Assert.Equal(1, (nudged.Frame.X - resized.Frame.X) * WidgetDesignRules.Canvas(size).Width, 10);
        var locked = nudged with { Locked = true }; Assert.Same(locked, WidgetLayerEdits.Move(locked, 1, 1)); Assert.Same(locked, WidgetLayerEdits.ScaleCentered(locked, 2));
        Assert.Same(text, WidgetLayerEdits.Rotate(text, 1));
        var result = WidgetLayerEdits.Duplicate(WidgetDesignRules.New() with { Size = size, Layers = [text] }, text.Id);
        var edited = result.Copy with { Text = result.Copy.Text! with { Content = "Independent" } };
        Assert.Equal(text.Text.Content, result.Design.Layers[0].Text!.Content); Assert.Equal("Independent", edited.Text!.Content);
        Assert.Equal(text.Text, result.Copy.Text); Assert.NotEqual(text.Id, result.Copy.Id);
    }
}
