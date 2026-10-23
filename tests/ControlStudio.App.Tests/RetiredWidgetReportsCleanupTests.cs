using Xunit;

namespace ControlStudio.App.Tests;

public sealed class RetiredWidgetReportsCleanupTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cs-retired-reports-" + Guid.NewGuid());
    private readonly TestPreferences preferences = new();
    private string Reports => Path.Combine(root, "Library", "Application Support", "ControlStudio", "WidgetBackgroundDiagnostics");
    public RetiredWidgetReportsCleanupTests() => Directory.CreateDirectory(root);
    private string Report()
    {
        Directory.CreateDirectory(Reports);
        var path = Path.Combine(Reports, Guid.NewGuid() + ".json"); File.WriteAllText(path, "retired report"); return path;
    }
    private bool Run(string group = "group.test") => RetiredWidgetReportsCleanup.TryRun(root, group, preferences);

    [Fact] public void RemovesOwnedReportsAndTheirEmptyFolderWithoutTouchingOtherAppData()
    {
        var report = Report();
        var sources = new[] { "counter.sqlite3", "WidgetDesigns/design.json", "Clipboard/photo.png" };
        foreach (var name in sources) { var path = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, "keep"); }
        Assert.True(Run()); Assert.False(File.Exists(report)); Assert.False(Directory.Exists(Reports));
        foreach (var name in sources) Assert.Equal("keep", File.ReadAllText(Path.Combine(root, name)));
    }
    [Fact] public void UnknownFilesAndNestedFoldersArePreserved()
    {
        var report = Report(); var unknown = Path.Combine(Reports, "notes.json"); File.WriteAllText(unknown, "keep");
        var nested = Path.Combine(Reports, "nested", Guid.NewGuid() + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(nested)!); File.WriteAllText(nested, "keep");
        Assert.True(Run()); Assert.False(File.Exists(report)); Assert.Equal("keep", File.ReadAllText(unknown)); Assert.Equal("keep", File.ReadAllText(nested));
    }
    [Fact] public void UnavailableContainerDoesNotMarkCompletionAndCanRetry()
    {
        var report = Report();
        Assert.False(RetiredWidgetReportsCleanup.TryRun(null, "group.test", preferences)); Assert.Empty(preferences.Values);
        Assert.True(Run()); Assert.False(File.Exists(report));
    }
    [Fact] public void CompletedMigrationPersistsAcrossRestartAndIsScopedToTheAppGroup()
    {
        Report(); Assert.True(Run()); var later = Report();
        Assert.True(RetiredWidgetReportsCleanup.TryRun(root, "group.test", new TestPreferences(preferences.Values)));
        Assert.True(File.Exists(later)); Assert.True(Run("group.other")); Assert.False(File.Exists(later));
    }
    [Fact] public void MissingReportFolderCompletesWithoutCreatingDirectories()
    {
        Assert.True(Run()); Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }
    [Fact] public void BlockedReportDirectoryRetriesAfterTheConflictIsRemoved()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Reports)!); File.WriteAllText(Reports, "keep");
        Assert.False(Run()); Assert.Empty(preferences.Values); Assert.Equal("keep", File.ReadAllText(Reports));
        File.Delete(Reports); var report = Report(); Assert.True(Run()); Assert.False(File.Exists(report));
    }
    [Fact] public void PreferenceFailuresAreContainedAndDoNotMarkSuccess()
    {
        var report = Report(); preferences.FailReads = true;
        Assert.False(Run()); Assert.True(File.Exists(report));
        preferences.FailReads = false; preferences.FailWrites = true;
        Assert.False(Run()); Assert.Empty(preferences.Values);
        preferences.FailWrites = false; Assert.True(Run());
    }
    public void Dispose() => Directory.Delete(root, recursive: true);
}
