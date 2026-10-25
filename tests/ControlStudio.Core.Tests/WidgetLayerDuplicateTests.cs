using System.Globalization;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetLayerDuplicateTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480,
        new(-.2, .7, 1.4, .5), Zoom: 2.5, CropX: .2, CropY: .8, Visible: false, QuarterTurns: 3,
        FlipHorizontal: true, FlipVertical: true, Opacity: .4, BlendMode: WidgetBlendMode.SoftLight, Locked: true);
    [Theory]
    [InlineData(WidgetLayerKind.Photo)] [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)]
    public void DuplicateChangesOnlyIdentityAndNameAndInsertsAboveItsSource(WidgetLayerKind kind)
    {
        var source = kind switch
        {
            WidgetLayerKind.Photo => Photo(),
            WidgetLayerKind.Clock => WidgetClocks.New() with { Clock = new(false, false, WidgetClockFont.SfProRounded, "#12ABEF", .5, WidgetClockAlignment.Right, false, WidgetClockWeight.Light) },
            _ => WidgetDates.New() with { Date = new(WidgetDateFormat.Numeric, WidgetClockFont.SfProRounded, "#12ABEF", .5, WidgetClockAlignment.Right, WidgetClockWeight.Light, true, WidgetWeekdayStyle.Full) }
        };
        source = source with { Visible = false, Locked = true, Opacity = .4 };
        var below = Photo(); var above = Photo(); var original = WidgetDesignRules.New() with { Layers = [below, source, above] };
        var (design, copy) = WidgetLayerEdits.Duplicate(original, source.Id);
        WidgetDesignRules.Validate(design);
        Assert.Equal(new[] { below, source, copy, above }, design.Layers); Assert.Equal(new[] { below, source, above }, original.Layers);
        Assert.NotEqual(source.Id, copy.Id); Assert.NotEqual(Guid.Empty, copy.Id); Assert.Equal(source.Name + " copy", copy.Name);
        Assert.Equal(source with { Id = copy.Id, Name = copy.Name }, copy);
        Assert.Equal(original with { Layers = design.Layers }, design);
        var edited = copy with { Visible = true, Locked = false, Opacity = .9, Frame = copy.Frame with { X = .7 } };
        if (kind == WidgetLayerKind.Clock) edited = edited with { Clock = copy.Clock! with { ShowSeconds = true } };
        if (kind == WidgetLayerKind.Date) edited = edited with { Date = copy.Date! with { ShowWeekday = false } };
        Assert.NotEqual(source.Frame, edited.Frame); Assert.False(source.Visible); Assert.True(source.Locked); Assert.Equal(.4, source.Opacity);
        if (kind == WidgetLayerKind.Clock) Assert.False(source.Clock!.ShowSeconds);
        if (kind == WidgetLayerKind.Date) Assert.True(source.Date!.ShowWeekday);
        if (kind == WidgetLayerKind.Photo) Assert.Equal(source.AssetKey, copy.AssetKey);
    }
    [Fact]
    public void RepeatedCopiesHaveUniqueNamesAndNeverExceedTheLayerLimit()
    {
        var source = WidgetClocks.New(); var design = WidgetDesignRules.New() with { Layers = [source] };
        for (var count = 2; count <= WidgetDesignRules.MaximumLayers; count++)
        {
            var result = WidgetLayerEdits.Duplicate(design, source.Id); design = result.Design;
            Assert.Equal(count, design.Layers.Count); Assert.Equal(result.Copy.Id, design.Layers[1].Id);
            Assert.Equal(count, design.Layers.Select(layer => layer.Id).Distinct().Count()); Assert.Equal(count, design.Layers.Select(layer => layer.Name).Distinct().Count());
        }
        var before = design.Layers;
        Assert.Throws<IOException>(() => WidgetLayerEdits.Duplicate(design, source.Id)); Assert.Same(before, design.Layers);
        Assert.Throws<IOException>(() => WidgetLayerEdits.Duplicate(WidgetDesignRules.New() with { Layers = [source] }, Guid.NewGuid()));
    }
    [Theory]
    [InlineData("ascii")] [InlineData("emoji")] [InlineData("combining")]
    public void CopyNamesFitTheLimitWithoutSplittingUnicodeElementsAndAvoidCaseInsensitiveCollisions(string type)
    {
        var name = type switch { "ascii" => new string('A', 40), "emoji" => string.Concat(Enumerable.Repeat("😀", 20)), _ => string.Concat(Enumerable.Repeat("e\u0301", 20)) };
        var source = Photo() with { Name = name }; var original = WidgetDesignRules.New() with { Layers = [source] };
        var first = WidgetLayerEdits.Duplicate(original, source.Id);
        Assert.InRange(first.Copy.Name.Length, 1, 40); Assert.EndsWith(" copy", first.Copy.Name);
        var stem = first.Copy.Name[..^5]; Assert.StartsWith(stem, name);
        if (type == "emoji") Assert.Equal(0, stem.Length % 2);
        if (type == "combining") Assert.Equal(0, stem.Length % 2);
        Assert.True(StringInfo.ParseCombiningCharacters(stem).Length > 0);
        var collision = first.Copy with { Name = first.Copy.Name.ToUpperInvariant() };
        var second = WidgetLayerEdits.Duplicate(original with { Layers = [source, collision] }, source.Id);
        Assert.EndsWith(" copy 2", second.Copy.Name); Assert.InRange(second.Copy.Name.Length, 1, 40);
        WidgetDesignRules.Validate(second.Design);
    }
}
