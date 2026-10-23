using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ControlStudio.App;

public sealed record WidgetBackgroundReport(string Status, string Text);

/// <summary>Reads bounded, local extension snapshots without exposing user content or raw errors.</summary>
internal static class WidgetBackgroundReportReader
{
    internal const string RelativeDirectory = "Library/Application Support/ControlStudio/WidgetBackgroundDiagnostics";
    private sealed record Event(long Sequence, string Utc, string Stage, string Reason, string Detail);
    private sealed record Session(string Id, string Version, string Build, string Os, DateTimeOffset Started,
        List<Event> Events, List<Event> Latest);

    internal static WidgetBackgroundReport Read(string? directory, string version, string build, string os)
    {
        var report = new StringBuilder($"Control Studio {version} ({build})\niOS {os}\nWidget background diagnostics\n");
        report.AppendLine("Changed descriptors are a request, not confirmation of transparent display by iOS.");
        if (directory is null) return Result("App Group access is unavailable; widget diagnostics cannot be read.");
        var sessions = new List<Session>(); var unreadable = 0;
        try
        {
            if (Directory.Exists(directory))
            {
                // Own directory contains at most 16 recent sessions. Read a bounded window if pruning failed.
                foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                    .Take(512)
                    .Where(path => Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out _))
                    .OrderByDescending(File.GetLastWriteTimeUtc).Take(32))
                {
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        var bytes = new byte[65537]; var count = 0;
                        while (count < bytes.Length)
                        {
                            var read = stream.Read(bytes, count, bytes.Length - count); if (read == 0) break; count += read;
                        }
                        if (count > 65536) { unreadable++; continue; }
                        using var json = JsonDocument.Parse(bytes.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 12 });
                        var root = json.RootElement;
                        if (root.GetProperty("schema").GetInt32() != 1) { unreadable++; continue; }
                        var id = Text(root, "session");
                        if (!Guid.TryParseExact(id, "D", out _) || !string.Equals(Path.GetFileNameWithoutExtension(path), id, StringComparison.OrdinalIgnoreCase))
                        { unreadable++; continue; }
                        if (!DateTimeOffset.TryParse(Text(root, "startedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var started))
                        { unreadable++; continue; }
                        var events = root.GetProperty("events").EnumerateArray().TakeLast(64).Select(ParseEvent).ToList();
                        var latest = root.TryGetProperty("latest", out var summary)
                            ? summary.EnumerateObject().Take(12).Select(property => ParseEvent(property.Value)).ToList() : [];
                        sessions.Add(new(id, Text(root, "version"), Text(root, "build"), Text(root, "os"), started, events, latest));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
                    { unreadable++; }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Result("Widget diagnostic storage could not be read. Unlock this iPhone and try Refresh."); }
        var current = sessions.Where(s => s.Version == version && s.Build == build).OrderByDescending(s => s.Started).ToList();
        string status;
        if (current.Count == 0) status = "No report from this build yet. Open the Home Screen widget picker, then return here and tap Refresh.";
        else
        {
            var latest = current[0].Latest.Concat(current[0].Events).OrderBy(e => e.Sequence).ToList();
            var install = latest.LastOrDefault(e => e.Stage == "install");
            var rewrite = latest.LastOrDefault(e => e.Stage == "rewrite");
            status = rewrite is not null ? (rewrite.Reason == "changed-result-returned"
                ? "The last captured rewrite returned changed slot descriptors. Transparent display by iOS is not confirmed."
                : $"The last captured descriptor rewrite fell back: {rewrite.Reason}.")
                : install?.Reason == "installed" ? (latest.Any(e => e.Stage == "callback")
                    ? "A descriptor callback was captured, but no rewrite result was saved yet."
                    : "The adapter installed; no descriptor callback has been captured in the latest session.")
                : install is not null ? $"The adapter did not install: {install.Reason}."
                : "The extension started; no installation result was saved yet.";
        }
        report.AppendLine($"Unreadable/unsupported session files: {unreadable}");
        report.AppendLine($"Current-build sessions found: {current.Count}"); report.AppendLine(status);
        var selected = current.Count > 0 ? current.Take(3) : sessions.OrderByDescending(s => s.Started).Take(1);
        foreach (var session in selected)
        {
            report.AppendLine();
            report.AppendLine($"{(session.Version == version && session.Build == build ? "CURRENT BUILD" : "OLDER/DIFFERENT BUILD — not evidence for this installation")}: {session.Version} ({session.Build})");
            report.AppendLine($"Session: {session.Id}; started UTC: {session.Started:O}; OS: {session.Os}");
            report.AppendLine("Latest recorded stages:");
            foreach (var e in session.Latest.OrderBy(e => e.Sequence)) Append(e);
            report.AppendLine("Recent events (maximum 64; sequence gaps mean older events were trimmed):");
            foreach (var e in session.Events.OrderBy(e => e.Sequence)) Append(e);
        }
        report.AppendLine("Reports are local, bounded snapshots. A terminated extension or unavailable shared storage can prevent the final event from being saved.");
        return new(status, report.ToString());

        void Append(Event e) => report.AppendLine($"{e.Utc} #{e.Sequence} {e.Stage}: {e.Reason} {e.Detail}".TrimEnd());
        WidgetBackgroundReport Result(string message) => new(message, report.AppendLine(message).ToString());
    }
    private static Event ParseEvent(JsonElement element) => new(element.GetProperty("sequence").GetInt64(), Text(element, "utc"),
        Text(element, "stage"), Text(element, "reason"), Text(element, "detail"));
    private static string Text(JsonElement element, string key)
    {
        var text = element.GetProperty(key).GetString() ?? "";
        return new string(text.Take(256).Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    }
}
