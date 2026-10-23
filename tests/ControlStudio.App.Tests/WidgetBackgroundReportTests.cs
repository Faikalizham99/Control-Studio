using System.Text.Json;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetBackgroundReportTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cs-widget-report-" + Guid.NewGuid());
    public WidgetBackgroundReportTests() => Directory.CreateDirectory(directory);
    private string Session(string version = "0.2.23", string build = "25", string stage = "install", string reason = "installed",
        DateTimeOffset? started = null, int schema = 1, object? latest = null)
    {
        var id = Guid.NewGuid(); var path = Path.Combine(directory, id + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            schema, session = id.ToString(), version, build, os = "27.0", startedUtc = (started ?? DateTimeOffset.UtcNow).ToString("O"),
            events = new[] { new { sequence = 1, utc = "2026-10-24T01:00:00Z", stage, reason, detail = "" } }, latest = latest ?? new { }
        }));
        return path;
    }
    private WidgetBackgroundReport Read() => WidgetBackgroundReportReader.Read(directory, "0.2.23", "25", "27.0");
    [Fact] public void MissingGroupAndNoFilesCannotClaimSuccessfulInstallation()
    {
        Assert.Contains("App Group access is unavailable", WidgetBackgroundReportReader.Read(null, "1", "1", "27").Status);
        Assert.Contains("No report from this build", Read().Status);
        Assert.False(Directory.Exists(Path.Combine(directory, "new")));
        Assert.Contains("No report from this build", WidgetBackgroundReportReader.Read(Path.Combine(directory, "new"), "1", "1", "27").Status);
        Assert.False(Directory.Exists(Path.Combine(directory, "new")));
    }
    [Fact] public void InstalledWithNoCallbackDoesNotClaimTheRewriteRan()
    {
        Session(); var report = Read(); Assert.Contains("no descriptor callback", report.Status);
        Assert.Contains("not confirmation", report.Text); Assert.Contains("CURRENT BUILD", report.Text);
    }
    [Fact] public void RewriteFailureIncludesItsSpecificReason()
    {
        Session(stage: "rewrite", reason: "setter-unavailable-or-incompatible:setTransparent:");
        Assert.Contains("fell back: setter-unavailable-or-incompatible:setTransparent:", Read().Status);
    }
    [Fact] public void ReturnedDescriptorsAreNotPresentedAsConfirmedTransparency()
    {
        Session(stage: "rewrite", reason: "changed-result-returned");
        Assert.Contains("not confirmed", Read().Status);
    }
    [Fact] public void OlderBuildAndDifferentBuildNumberAreClearlyMarkedStale()
    {
        Session(build: "24", stage: "rewrite", reason: "changed-result-returned");
        var report = Read(); Assert.Contains("No report from this build", report.Status);
        Assert.Contains("OLDER/DIFFERENT BUILD", report.Text); Assert.DoesNotContain("CURRENT BUILD:", report.Text);
    }
    [Fact] public void NewestCurrentSessionWinsEvenWhenAnOlderBuildWasModifiedLater()
    {
        Session(started: DateTimeOffset.UtcNow.AddHours(-1), stage: "rewrite", reason: "changed-result-returned");
        Session(stage: "install", reason: "exported-object-class-missing");
        Session(build: "24", stage: "rewrite", reason: "changed-result-returned");
        Assert.Contains("did not install: exported-object-class-missing", Read().Status);
    }
    [Fact] public void StageSummarySurvivesTrimmedEventHistory()
    {
        Session(stage: "fetch", reason: "requested", latest: new {
            install = new { sequence = 1, utc = "2026-10-24T01:00:00Z", stage = "install", reason = "installed", detail = "" },
            callback = new { sequence = 2, utc = "2026-10-24T01:00:00Z", stage = "callback", reason = "received", detail = "" }
        });
        Assert.Contains("callback was captured", Read().Status);
    }
    [Fact] public void CorruptFutureOversizedAndInvalidSessionFilesAreIgnoredWithoutLeakingRawContent()
    {
        Session(schema: 2);
        File.WriteAllText(Path.Combine(directory, Guid.NewGuid() + ".json"), "secret raw malformed value");
        File.WriteAllText(Path.Combine(directory, Guid.NewGuid() + ".json"), new string('x', 65537));
        File.WriteAllText(Path.Combine(directory, "other-app.json"), "must not be read");
        var report = Read(); Assert.Contains("Unreadable/unsupported session files: 3", report.Text);
        Assert.DoesNotContain("secret", report.Text); Assert.DoesNotContain("must not", report.Text);
    }
    [Fact] public void MalformedFileDoesNotHideAValidCurrentSession()
    {
        Session(); File.WriteAllText(Path.Combine(directory, Guid.NewGuid() + ".json"), "{}");
        var report = Read(); Assert.Contains("no descriptor callback", report.Status);
        Assert.Contains("Unreadable/unsupported session files: 1", report.Text);
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
