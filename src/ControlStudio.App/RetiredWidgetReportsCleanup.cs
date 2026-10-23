using Microsoft.Maui.Storage;

namespace ControlStudio.App;

/// <summary>One-time retirement of the old widget report files, scoped to their App Group.</summary>
internal static class RetiredWidgetReportsCleanup
{
    internal static bool TryRun(string? containerPath, string appGroup, IPreferences preferences)
    {
        try
        {
            var key = "retired-widget-reports-cleaned-v1:" + appGroup;
            if (preferences.Get(key, false)) return true;
            if (string.IsNullOrWhiteSpace(containerPath) || !Directory.Exists(containerPath)) return false;
            var directory = Path.GetFullPath(containerPath);
            if (Linked(directory)) return false;
            foreach (var part in new[] { "Library", "Application Support", "ControlStudio", "WidgetBackgroundDiagnostics" })
            {
                directory = Path.Combine(directory, part);
                if (File.Exists(directory) || Linked(directory)) return false;
                if (!Directory.Exists(directory)) { preferences.Set(key, true); return true; }
            }
            // Never recurse or follow directory links. Only the recorder's UUID.json files are owned.
            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
                    && Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out _))
                    File.Delete(path);
            }
            if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory, recursive: false);
            preferences.Set(key, true); return true;
        }
        catch (Exception) { return false; } // A failed cleanup must not prevent launch; retry on activation.
    }

    private static bool Linked(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
