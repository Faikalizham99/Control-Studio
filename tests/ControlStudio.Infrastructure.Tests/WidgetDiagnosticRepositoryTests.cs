using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetDiagnosticRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ControlStudioDiagnosticTests", Guid.NewGuid().ToString("N"));
    private WidgetDiagnosticRepository Repository => new(root);
    private string Settings => Path.Combine(root, "diagnostics", "settings.json");
    private string Events => Path.Combine(root, "diagnostics", "events");
    [Fact]
    public async Task TestsAreOptInPersistAcrossReopenAndStopRestoresPhotoPath()
    {
        Assert.Null((await Repository.ReadAsync()).Settings);
        var text = await Repository.StartAsync(true);
        var reopened = await Repository.ReadAsync(); Assert.Equal(text, reopened.Settings);
        Assert.True(text.Enabled); Assert.True(text.TextOnly);
        await Repository.RecordHostAsync(text, "session.text-started");
        Assert.Single((await Repository.ReadAsync()).Events);
        await Repository.StopAsync();
        var stopped = await Repository.ReadAsync(); Assert.False(stopped.Settings!.Enabled); Assert.False(stopped.Settings.TextOnly);
        Assert.Single(stopped.Events);
        var photo = await Repository.StartAsync(false); Assert.NotEqual(text.Session, photo.Session);
        Assert.False(photo.TextOnly); Assert.Empty((await Repository.ReadAsync()).Events);
        await Repository.RecordHostAsync(text, "late.old-request");
        Assert.Empty((await Repository.ReadAsync()).Events);
    }
    [Fact]
    public async Task UnsupportedSettingsArePreservedInsteadOfReset()
    {
        await Repository.StartAsync(true);
        var future = (await File.ReadAllTextAsync(Settings)).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");
        await File.WriteAllTextAsync(Settings, future);
        await Assert.ThrowsAsync<IOException>(() => Repository.StartAsync(false));
        await Assert.ThrowsAsync<IOException>(() => Repository.StopAsync());
        Assert.Equal(future, await File.ReadAllTextAsync(Settings));
    }
    [Fact]
    public async Task NativeEventsAreValidatedAndBrokenFilesDoNotHideGoodResults()
    {
        var settings = await Repository.StartAsync(false); Directory.CreateDirectory(Events);
        var item = new WidgetDiagnosticEvent(1, settings.Session, Guid.NewGuid().ToString("N"), 2, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "timeline", "image.created", "width=340;height=340");
        await File.WriteAllBytesAsync(EventPath(settings), JsonSerializer.SerializeToUtf8Bytes(item, WidgetDesignRules.JsonContext.WidgetDiagnosticEvent));
        await File.WriteAllTextAsync(EventPath(settings), "{broken");
        await File.WriteAllTextAsync(EventPath(settings), new string('x', 3000));
        var invalid = item with { Detail = "/private/user/photo.png" };
        await File.WriteAllBytesAsync(EventPath(settings), JsonSerializer.SerializeToUtf8Bytes(invalid, WidgetDesignRules.JsonContext.WidgetDiagnosticEvent));
        var value = await Repository.ReadAsync(); Assert.Equal(item, Assert.Single(value.Events)); Assert.Equal(3, value.UnreadableEvents);
    }
    [Fact]
    public async Task ConcurrentBreadcrumbsStayBoundedAndNeverTouchWidgetData()
    {
        Directory.CreateDirectory(root); var catalog = Path.Combine(root, "catalog.json");
        await File.WriteAllTextAsync(catalog, "saved designs must remain intact");
        var settings = await Repository.StartAsync(true);
        // Multiple writers own unique files; sequential pruning is also exercised after the burst.
        await Task.WhenAll(Enumerable.Range(0, 150).Select(_ => Repository.RecordHostAsync(settings, "provider.begin")));
        await Repository.RecordHostAsync(settings, "session.last");
        var result = await Repository.ReadAsync();
        Assert.InRange(result.Events.Count, 1, WidgetDiagnosticRules.MaximumEvents);
        Assert.Equal(result.Events.Count, result.Events.Select(x => x.Request).Distinct().Count());
        Assert.Contains(result.Events, x => x.Stage == "session.last");
        Assert.Equal("saved designs must remain intact", await File.ReadAllTextAsync(catalog));
    }
    private string EventPath(WidgetDiagnosticSettings settings) => Path.Combine(Events, settings.Session + "-" + Guid.NewGuid().ToString("N") + ".json");
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
