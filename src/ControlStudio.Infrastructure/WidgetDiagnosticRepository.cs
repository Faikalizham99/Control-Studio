using System.Text.Json;
using ControlStudio.Core.Widgets;

namespace ControlStudio.Infrastructure;

/// <summary>Opt-in, bounded diagnostic sessions shared with the native widget. No design content is recorded.</summary>
public sealed class WidgetDiagnosticRepository(string widgetDirectory)
{
    private string DirectoryPath => Path.Combine(widgetDirectory, "diagnostics");
    private string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    private string EventsPath => Path.Combine(DirectoryPath, "events");
    private async Task<WidgetDiagnosticSettings?> ReadSettingsAsync()
    {
        if (!File.Exists(SettingsPath)) return null;
        var bytes = await ReadBoundedAsync(SettingsPath);
        var settings = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetDiagnosticSettings)
            ?? throw new IOException("Widget diagnostic settings are unavailable.");
        WidgetDiagnosticRules.Validate(settings); return settings;
    }
    public async Task<WidgetDiagnosticSettings> StartAsync(bool textOnly)
    {
        // Preserve unsupported/corrupt settings rather than silently overwriting a newer format.
        _ = await ReadSettingsAsync();
        var settings = new WidgetDiagnosticSettings(1, Guid.NewGuid().ToString("N"), true, textOnly, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await WriteSettingsAsync(settings); return settings;
    }
    public async Task StopAsync()
    {
        var settings = await ReadSettingsAsync(); if (settings is null) return;
        await WriteSettingsAsync(settings with { Enabled = false, TextOnly = false });
    }
    private async Task WriteSettingsAsync(WidgetDiagnosticSettings settings)
    {
        WidgetDiagnosticRules.Validate(settings);
        await WidgetDesignRepository.AtomicWriteAsync(SettingsPath, JsonSerializer.SerializeToUtf8Bytes(settings, WidgetDesignRules.JsonContext.WidgetDiagnosticSettings), default);
    }
    public async Task RecordHostAsync(WidgetDiagnosticSettings settings, string stage)
    {
        if (!settings.Enabled) return;
        var item = new WidgetDiagnosticEvent(1, settings.Session, Guid.NewGuid().ToString("N"), 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "host", stage, "");
        WidgetDiagnosticRules.Validate(item);
        await WidgetDesignRepository.AtomicWriteAsync(Path.Combine(EventsPath, settings.Session + "-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.SerializeToUtf8Bytes(item, WidgetDesignRules.JsonContext.WidgetDiagnosticEvent), default);
        Prune();
    }
    public async Task<WidgetDiagnosticSnapshot> ReadAsync()
    {
        var settings = await ReadSettingsAsync(); var items = new List<WidgetDiagnosticEvent>(); var unreadable = 0;
        if (settings is null || !Directory.Exists(EventsPath)) return new(settings, items);
        foreach (var file in EventFiles().Where(x => Path.GetFileName(x).StartsWith(settings.Session + "-", StringComparison.Ordinal)).TakeLast(WidgetDiagnosticRules.MaximumEvents))
        {
            try
            {
                var item = JsonSerializer.Deserialize(await ReadBoundedAsync(file), WidgetDesignRules.JsonContext.WidgetDiagnosticEvent);
                if (item is null) throw new IOException("Empty diagnostic event.");
                WidgetDiagnosticRules.Validate(item); if (item.Session == settings.Session) items.Add(item);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { unreadable++; }
        }
        return new(settings, items.OrderBy(x => x.TimestampUnixMilliseconds).ThenBy(x => x.Request, StringComparer.Ordinal).ThenBy(x => x.Sequence).ToArray(), unreadable);
    }
    private IEnumerable<string> EventFiles() => Directory.EnumerateFiles(EventsPath, "*.json")
        .Where(x => Path.GetFileNameWithoutExtension(x).Split('-') is var parts && parts.Length == 2 && parts.All(WidgetDiagnosticRules.ValidId))
        .OrderBy(File.GetLastWriteTimeUtc);
    private void Prune()
    {
        try { foreach (var file in EventFiles().SkipLast(WidgetDiagnosticRules.MaximumEvents).ToArray()) File.Delete(file); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private static async Task<byte[]> ReadBoundedAsync(string path)
    {
        if (new FileInfo(path).Length > WidgetDiagnosticRules.MaximumFileBytes) throw new IOException("Widget diagnostic file is too large.");
        var bytes = await File.ReadAllBytesAsync(path);
        if (bytes.Length > WidgetDiagnosticRules.MaximumFileBytes) throw new IOException("Widget diagnostic file is too large.");
        return bytes;
    }
}
