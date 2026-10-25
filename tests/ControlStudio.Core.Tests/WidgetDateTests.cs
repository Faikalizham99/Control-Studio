using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetDateTests
{
    [Theory]
    [InlineData(WidgetDateFormat.LongDate, false, WidgetWeekdayStyle.Short, "25 October 2026")]
    [InlineData(WidgetDateFormat.LongDate, true, WidgetWeekdayStyle.Short, "Sun, 25 October 2026")]
    [InlineData(WidgetDateFormat.LongDate, true, WidgetWeekdayStyle.Full, "Sunday, 25 October 2026")]
    [InlineData(WidgetDateFormat.ShortDate, false, WidgetWeekdayStyle.Short, "25 Oct")]
    [InlineData(WidgetDateFormat.ShortDate, true, WidgetWeekdayStyle.Short, "Sun, 25 Oct")]
    [InlineData(WidgetDateFormat.ShortDate, true, WidgetWeekdayStyle.Full, "Sunday, 25 Oct")]
    [InlineData(WidgetDateFormat.Numeric, false, WidgetWeekdayStyle.Short, "25/10/2026")]
    [InlineData(WidgetDateFormat.Numeric, true, WidgetWeekdayStyle.Short, "Sun, 25/10/2026")]
    [InlineData(WidgetDateFormat.Numeric, true, WidgetWeekdayStyle.Full, "Sunday, 25/10/2026")]
    [InlineData(WidgetDateFormat.Month, false, WidgetWeekdayStyle.Short, "October")]
    [InlineData(WidgetDateFormat.Month, true, WidgetWeekdayStyle.Short, "Sun, October")]
    [InlineData(WidgetDateFormat.Month, true, WidgetWeekdayStyle.Full, "Sunday, October")]
    public void WeekdayIsIndependentOfTheDateFormatAndNeverDuplicated(WidgetDateFormat format, bool show, WidgetWeekdayStyle style, string expected)
    {
        var options = new WidgetDateOptions(Format: format, ShowWeekday: show, WeekdayStyle: style);
        WidgetDates.Validate(options);
        Assert.Equal(expected, WidgetDates.Text(options, new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));
    }
    [Fact]
    public void MissingWeekdaySettingsRetainLegacyAppearanceAndFormatChangesKeepTheCurrentChoice()
    {
        foreach (var (name, format) in new[] { ("longDate", WidgetDateFormat.LongDate), ("shortDate", WidgetDateFormat.ShortDate), ("numeric", WidgetDateFormat.Numeric), ("month", WidgetDateFormat.Month) })
        {
            var options = JsonSerializer.Deserialize<WidgetDateOptions>("{\"format\":\"" + name + "\"}", WidgetDesignRules.Json)!;
            Assert.Null(options.ShowWeekday); Assert.Equal(format == WidgetDateFormat.ShortDate, WidgetDates.IncludesWeekday(options));
            Assert.Equal(WidgetWeekdayStyle.Short, options.WeekdayStyle);
            foreach (var next in WidgetDates.Formats)
                Assert.Equal(WidgetDates.IncludesWeekday(options), WidgetDates.ChangeFormat(options, next).ShowWeekday);
        }
        Assert.False(WidgetDates.IncludesWeekday(WidgetDates.New().Date!));
        var explicitOff = new WidgetDateOptions(Format: WidgetDateFormat.ShortDate, ShowWeekday: false, WeekdayStyle: WidgetWeekdayStyle.Full);
        Assert.False(WidgetDates.IncludesWeekday(WidgetDates.ChangeFormat(explicitOff, WidgetDateFormat.Month)));
        Assert.Equal(WidgetWeekdayStyle.Full, WidgetDates.ChangeFormat(explicitOff, WidgetDateFormat.Numeric).WeekdayStyle);
    }
    [Fact]
    public void WeekdaySettingsRoundTripAndTrackTheSameLocalDayAsTheDate()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
        var options = new WidgetDateOptions(Format: WidgetDateFormat.Numeric, ShowWeekday: true, WeekdayStyle: WidgetWeekdayStyle.Full);
        var restored = JsonSerializer.Deserialize<WidgetDateOptions>(JsonSerializer.Serialize(options, WidgetDesignRules.Json), WidgetDesignRules.Json)!;
        Assert.Equal(options, restored);
        Assert.Equal("Sunday, 25/10/2026", WidgetDates.Text(restored, new(2026, 10, 25, 15, 59, 59, TimeSpan.Zero), zone));
        Assert.Equal("Monday, 26/10/2026", WidgetDates.Text(restored, new(2026, 10, 25, 16, 0, 0, TimeSpan.Zero), zone));
        var json = JsonSerializer.Serialize(options, WidgetDesignRules.Json);
        foreach (var bad in new[] { "\"unknown\"", "2", "null" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetDateOptions>(json.Replace("\"full\"", bad), WidgetDesignRules.Json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetDateOptions>(json.Replace("\"showWeekday\":true", "\"showWeekday\":\"true\""), WidgetDesignRules.Json));
        Assert.Throws<IOException>(() => WidgetDates.Validate(options with { WeekdayStyle = (WidgetWeekdayStyle)99 }));
    }
    [Theory]
    [InlineData(WidgetDateFormat.LongDate, "25 October 2026")]
    [InlineData(WidgetDateFormat.ShortDate, "Sun, 25 Oct")]
    [InlineData(WidgetDateFormat.Numeric, "25/10/2026")]
    [InlineData(WidgetDateFormat.Month, "October")]
    public void PresetsHaveStableEnglishGregorianFormatting(WidgetDateFormat format, string expected)
        => Assert.Equal(expected, WidgetDates.Text(new(Format: format), new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc));
    [Fact]
    public void DateTracksLocalMidnightLeapDaysAndYearChanges()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
        var options = new WidgetDateOptions(Format: WidgetDateFormat.Numeric);
        Assert.Equal("25/10/2026", WidgetDates.Text(options, new(2026, 10, 25, 15, 59, 59, TimeSpan.Zero), zone));
        Assert.Equal("26/10/2026", WidgetDates.Text(options, new(2026, 10, 25, 16, 0, 0, TimeSpan.Zero), zone));
        Assert.Equal("29/02/2028", WidgetDates.Text(options, new(2028, 2, 28, 16, 0, 0, TimeSpan.Zero), zone));
        Assert.Equal("01/03/2028", WidgetDates.Text(options, new(2028, 2, 29, 16, 0, 0, TimeSpan.Zero), zone));
        Assert.Equal("01/01/2027", WidgetDates.Text(options, new(2026, 12, 31, 16, 0, 0, TimeSpan.Zero), zone));
    }
    [Fact]
    public void AllSharedFontsWeightsAndAlignmentsRoundTripWithoutClockOnlySettings()
    {
        foreach (var font in Enum.GetValues<WidgetClockFont>())
        foreach (var weight in WidgetClockWeights.Available(font).Select(x => (WidgetClockWeight?)x).Prepend(null))
        foreach (var alignment in Enum.GetValues<WidgetClockAlignment>())
        {
            var layer = WidgetDates.New() with { Date = new(WidgetDateFormat.ShortDate, font, "#12ABEF", .6, alignment, weight, true, WidgetWeekdayStyle.Full), Locked = true };
            WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [layer] });
            var json = JsonSerializer.Serialize(layer, WidgetDesignRules.Json);
            var restored = JsonSerializer.Deserialize<WidgetLayer>(json, WidgetDesignRules.Json)!;
            Assert.Equal(layer, restored); Assert.Null(restored.Clock);
            Assert.DoesNotContain("twentyFourHour", json); Assert.DoesNotContain("showSeconds", json); Assert.DoesNotContain("showAmPm", json);
            var changed = WidgetTextLayers.WithStyle(layer, WidgetTextLayers.Style(layer) with { ColourHex = "#FFFFFF", FontScale = .4 });
            Assert.Equal(layer.Date.Format, changed.Date!.Format); Assert.Equal(.4, changed.Date.FontScale); Assert.Null(changed.Clock);
            Assert.True(changed.Date.ShowWeekday); Assert.Equal(WidgetWeekdayStyle.Full, changed.Date.WeekdayStyle);
        }
    }
    [Fact]
    public void InvalidDateAndMixedPayloadsCannotBeSaved()
    {
        var date = WidgetDates.New(); var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 1, 1, new());
        foreach (var bad in new[] { date with { Date = null }, date with { Clock = new() }, photo with { Date = new() },
            date with { Kind = WidgetLayerKind.Clock }, date with { Date = new(Format: (WidgetDateFormat)99) },
            date with { Date = new(FontScale: double.NaN) }, date with { Date = new(ColourHex: "#bad") },
            date with { Date = new(Font: WidgetClockFont.Rounded, Weight: WidgetClockWeight.Light) },
            date with { BlendMode = WidgetBlendMode.Multiply }, date with { FlipHorizontal = true } })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [bad] }));
        var json = JsonSerializer.Serialize(date, WidgetDesignRules.Json);
        foreach (var value in new[] { "\"unknown\"", "4", "null" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetLayer>(json.Replace("\"longDate\"", value), WidgetDesignRules.Json));
    }
    [Fact]
    public void DateSharesStablePlacementLocksAndSceneOrdering()
    {
        var date = WidgetDates.New(); var clock = WidgetClocks.New();
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 1, 1, new());
        var design = WidgetDesignRules.New() with { Layers = [photo, date, photo with { Id = Guid.NewGuid() }, clock] };
        var scene = WidgetScenes.Manifest(design); Assert.Equal(2, scene.SchemaVersion); Assert.Equal(date, scene.Parts[1].Date); Assert.Equal(clock, scene.Parts[3].Clock);
        Assert.Equal(new int?[] { 0, null, 2, null }, scene.Parts.Select(part => part.ImageIndex));
        WidgetScenes.Validate(scene, design); Assert.True(WidgetScenes.IsLive(design));
        Assert.Throws<IOException>(() => WidgetScenes.Validate(scene with { SchemaVersion = 1 }, design));
        Assert.Same(date, WidgetLayerEdits.Rotate(date, 1));
        var moved = WidgetLayerEdits.Move(date, -.2, .1); var resized = WidgetLayerEdits.ScaleCentered(moved, 1.7);
        Assert.Equal(moved.Frame.X + moved.Frame.Width / 2, resized.Frame.X + resized.Frame.Width / 2, 8);
        var locked = date with { Locked = true }; Assert.Same(locked, WidgetLayerEdits.Move(locked, .2, .3)); Assert.Same(locked, WidgetLayerEdits.ScaleCentered(locked, 2));
        Assert.False(WidgetScenes.IsLive(design with { Layers = [date with { Visible = false }, clock with { Opacity = 0 }] }));
    }
    [Fact]
    public void NewDateFixtureAndOlderClockScenesBothDecodeAndUseOnlyTheirRequiredSchema()
    {
        var scene = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-scene-v2-date.example.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var date = scene.Parts.Single().Date!; var design = WidgetDesignRules.New() with { Layers = [date] }; WidgetDesignRules.Validate(design); WidgetScenes.Validate(scene, design);
        Assert.Equal(WidgetClockWeight.Light, date.Date!.Weight); Assert.Equal(WidgetClockFont.SfProRounded, date.Date.Font);
        var clock = WidgetClocks.New(); var old = WidgetScenes.Manifest(design with { Layers = [clock] });
        Assert.Equal(1, old.SchemaVersion); Assert.DoesNotContain("\"date\":", JsonSerializer.Serialize(old, WidgetDesignRules.JsonContext.WidgetScene));
    }
}
