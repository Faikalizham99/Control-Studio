namespace ControlStudio.Core.Widgets;

public sealed record WidgetDiagnosticSettings(int SchemaVersion, string Session, bool Enabled, bool TextOnly, long StartedAtUnixMilliseconds);
public sealed record WidgetDiagnosticEvent(int SchemaVersion, string Session, string Request, int Sequence, long TimestampUnixMilliseconds,
    string Source, string Stage, string Detail);
public sealed record WidgetDiagnosticSnapshot(WidgetDiagnosticSettings? Settings, IReadOnlyList<WidgetDiagnosticEvent> Events, int UnreadableEvents = 0);

public static class WidgetDiagnosticRules
{
    public const int MaximumEvents = 128, MaximumFileBytes = 2048;
    public static bool ValidId(string? value) => WidgetDesignRules.ValidKey(value) && value != new string('0', 32);
    public static void Validate(WidgetDiagnosticSettings settings)
    {
        if (settings.SchemaVersion != 1 || !ValidId(settings.Session) || !ValidTime(settings.StartedAtUnixMilliseconds) || (!settings.Enabled && settings.TextOnly))
            throw new IOException("Widget diagnostic settings are invalid or require a newer app. They were not changed.");
    }
    public static void Validate(WidgetDiagnosticEvent item)
    {
        if (item.SchemaVersion != 1 || !ValidId(item.Session) || !ValidId(item.Request) || item.Sequence < 1 || !ValidTime(item.TimestampUnixMilliseconds)
            || item.Source is not ("host" or "query" or "snapshot" or "timeline") || !Token(item.Stage, 60) || !Token(item.Detail, 160, allowEmpty: true))
            throw new IOException("A widget diagnostic event is invalid.");
    }
    private static bool ValidTime(long time) => time is > 0 and <= 253402300799999;
    private static bool Token(string? value, int max, bool allowEmpty = false) => value is not null && value.Length <= max
        && (allowEmpty || value.Length > 0) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '=' or ';' or ',');
}
