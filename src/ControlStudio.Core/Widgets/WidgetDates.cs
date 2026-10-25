using System.Globalization;

namespace ControlStudio.Core.Widgets;

public enum WidgetDateFormat { LongDate, ShortDate, Numeric, Month }
public enum WidgetWeekdayStyle { Short, Full }
public sealed record WidgetDateOptions(WidgetDateFormat Format = WidgetDateFormat.LongDate,
    WidgetClockFont Font = WidgetClockFont.Rounded, string ColourHex = "#FFFFFF", double FontScale = .72,
    WidgetClockAlignment Alignment = WidgetClockAlignment.Centre, WidgetClockWeight? Weight = null,
    bool? ShowWeekday = null, WidgetWeekdayStyle WeekdayStyle = WidgetWeekdayStyle.Short);

public static class WidgetDates
{
    public static IReadOnlyList<WidgetDateFormat> Formats { get; } = Array.AsReadOnly(Enum.GetValues<WidgetDateFormat>());
    public static WidgetLayer New(string name = "Date 1", string colourHex = "#FFFFFF") => new(Guid.NewGuid(), name, "", 1, 1,
        new(.05, .35, .9, .3), Kind: WidgetLayerKind.Date, Date: new(ColourHex: colourHex, ShowWeekday: false));
    // Earlier short-date presets included the weekday; the others did not.
    public static bool IncludesWeekday(WidgetDateOptions date) => date.ShowWeekday ?? date.Format == WidgetDateFormat.ShortDate;
    public static WidgetDateOptions ChangeFormat(WidgetDateOptions date, WidgetDateFormat format) => date with
    { Format = format, ShowWeekday = IncludesWeekday(date) };
    public static string Title(WidgetDateFormat format) => format switch
    {
        WidgetDateFormat.LongDate => "Full date", WidgetDateFormat.ShortDate => "Short date",
        WidgetDateFormat.Numeric => "Numeric date", WidgetDateFormat.Month => "Month", _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
    public static string Text(WidgetDateOptions options, DateTimeOffset date, TimeZoneInfo? zone = null)
    {
        var format = options.Format switch
        {
            WidgetDateFormat.LongDate => "d MMMM yyyy", WidgetDateFormat.ShortDate => "d MMM",
            WidgetDateFormat.Numeric => "dd/MM/yyyy", WidgetDateFormat.Month => "MMMM",
            _ => throw new IOException("Choose a valid date format.")
        };
        if (IncludesWeekday(options)) format = (options.WeekdayStyle == WidgetWeekdayStyle.Full ? "dddd, " : "ddd, ") + format;
        return TimeZoneInfo.ConvertTime(date, zone ?? TimeZoneInfo.Local).ToString(format, CultureInfo.InvariantCulture);
    }
    public static void Validate(WidgetDateOptions date)
    {
        if (!Enum.IsDefined(date.Format) || !Enum.IsDefined(date.WeekdayStyle)) throw new IOException("Choose valid date settings.");
        try { WidgetClocks.Validate(Style(date)); }
        catch (IOException ex) { throw new IOException("Choose valid date settings before saving.", ex); }
    }
    // Shared text appearance is adapted without storing clock-only switches on a date.
    public static WidgetClockOptions Style(WidgetDateOptions date) => new(Font: date.Font, ColourHex: date.ColourHex,
        FontScale: date.FontScale, Alignment: date.Alignment, Weight: date.Weight);
    public static WidgetDateOptions WithStyle(WidgetDateOptions date, WidgetClockOptions style) => date with
    { Font = style.Font, ColourHex = style.ColourHex, FontScale = style.FontScale, Alignment = style.Alignment, Weight = style.Weight };
}

public static class WidgetTextLayers
{
    public static bool IsText(WidgetLayer layer) => layer.Kind is WidgetLayerKind.Clock or WidgetLayerKind.Date;
    public static string Name(WidgetLayer layer) => layer.Kind == WidgetLayerKind.Date ? "date" : "clock";
    public static WidgetClockOptions Style(WidgetLayer layer) => layer.Kind == WidgetLayerKind.Date ? WidgetDates.Style(layer.Date!) : layer.Clock!;
    public static WidgetLayer WithStyle(WidgetLayer layer, WidgetClockOptions style) => layer.Kind == WidgetLayerKind.Date
        ? layer with { Date = WidgetDates.WithStyle(layer.Date!, style) } : layer with { Clock = style };
    public static string Text(WidgetLayer layer, DateTimeOffset time, TimeZoneInfo? zone = null) => layer.Kind == WidgetLayerKind.Date
        ? WidgetDates.Text(layer.Date!, time, zone) : WidgetClocks.Text(layer.Clock!, time, zone);
}
