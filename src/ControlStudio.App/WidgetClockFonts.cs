using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

/// <summary>Use the same PostScript names for picker samples, live drawing and saved previews.</summary>
internal static class WidgetClockFonts
{
    public static IReadOnlyList<WidgetClockFont> All { get; } = Array.AsReadOnly(Enum.GetValues<WidgetClockFont>());
    public static string Title(WidgetClockFont font) => font switch
    {
        WidgetClockFont.System => "System",
        WidgetClockFont.Rounded => "Arial Rounded",
        WidgetClockFont.Monospaced => "Mono",
        WidgetClockFont.Serif => "Serif",
        WidgetClockFont.RocknRollOne => "RocknRoll One",
        WidgetClockFont.DelaGothicOne => "Dela Gothic One",
        WidgetClockFont.KaiseiDecol => "Kaisei Decol",
        WidgetClockFont.SfProRounded => "SF Pro Rounded",
        _ => throw new ArgumentOutOfRangeException(nameof(font))
    };
    public static string? Family(WidgetClockFont font, WidgetClockWeight? weight = null) => font switch
    {
        WidgetClockFont.System => null,
        WidgetClockFont.SfProRounded => null,
        WidgetClockFont.Rounded => "ArialRoundedMTBold",
        WidgetClockFont.Monospaced => weight == WidgetClockWeight.Regular ? "Menlo-Regular" : "Menlo-Bold",
        WidgetClockFont.Serif => weight == WidgetClockWeight.Regular ? "Georgia" : "Georgia-Bold",
        WidgetClockFont.RocknRollOne => "RocknRollOne-Regular",
        WidgetClockFont.DelaGothicOne => "DelaGothicOne-Regular",
        WidgetClockFont.KaiseiDecol => "KaiseiDecol-Regular",
        _ => throw new ArgumentOutOfRangeException(nameof(font))
    };
}
