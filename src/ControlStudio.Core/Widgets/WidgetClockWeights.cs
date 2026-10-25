namespace ControlStudio.Core.Widgets;

public static class WidgetClockWeights
{
    private static readonly IReadOnlyList<WidgetClockWeight> system = Array.AsReadOnly(Enum.GetValues<WidgetClockWeight>());
    private static readonly IReadOnlyList<WidgetClockWeight> regularBold = Array.AsReadOnly(new[] { WidgetClockWeight.Regular, WidgetClockWeight.Bold });
    private static readonly IReadOnlyList<WidgetClockWeight> bold = Array.AsReadOnly(new[] { WidgetClockWeight.Bold });
    private static readonly IReadOnlyList<WidgetClockWeight> regular = Array.AsReadOnly(new[] { WidgetClockWeight.Regular });
    public static IReadOnlyList<WidgetClockWeight> Available(WidgetClockFont font) => font switch
    {
        WidgetClockFont.System or WidgetClockFont.SfProRounded => system,
        WidgetClockFont.Monospaced or WidgetClockFont.Serif => regularBold,
        WidgetClockFont.Rounded => bold,
        WidgetClockFont.RocknRollOne or WidgetClockFont.DelaGothicOne or WidgetClockFont.KaiseiDecol => regular,
        _ => throw new ArgumentOutOfRangeException(nameof(font))
    };
    // Missing weight preserves every pre-weight clock's actual native face.
    public static WidgetClockWeight Default(WidgetClockFont font) => font switch
    {
        WidgetClockFont.System or WidgetClockFont.SfProRounded => WidgetClockWeight.Semibold,
        WidgetClockFont.Rounded or WidgetClockFont.Monospaced or WidgetClockFont.Serif => WidgetClockWeight.Bold,
        WidgetClockFont.RocknRollOne or WidgetClockFont.DelaGothicOne or WidgetClockFont.KaiseiDecol => WidgetClockWeight.Regular,
        _ => throw new ArgumentOutOfRangeException(nameof(font))
    };
    public static WidgetClockWeight Resolve(WidgetClockOptions clock) => clock.Weight ?? Default(clock.Font);
    public static WidgetClockOptions ChangeFont(WidgetClockOptions clock, WidgetClockFont font) => clock with
    {
        Font = font, Weight = clock.Weight is { } weight && Available(font).Contains(weight) ? weight : null
    };
}
