using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetPublicationCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "WidgetPublicationCache", Guid.NewGuid().ToString("N"));
    private string Private => Path.Combine(root, "private");
    private string Shared => Path.Combine(root, "shared");
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=");
    private WidgetDesignRepository Repository => new(Private);
    private async Task<WidgetDesign> Save(bool live = false)
    {
        var key = await Repository.ImportPhotoAsync(Png);
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", key, 1, 1, new());
        var draft = WidgetDesignRules.New() with { Layers = live ? [photo, WidgetClocks.New(), photo with { Id = Guid.NewGuid() }] : [photo] };
        var scene = WidgetScenes.Manifest(draft);
        return await Repository.SaveAsync(draft, Png, scene: live ? new(scene, scene.Parts.Where(x => x.ImageIndex is not null)
            .ToDictionary(x => x.ImageIndex!.Value, _ => Png)) : null);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task UnchangedPublicationDoesNotReopenValidatedPayloadsOrRewriteManifests(bool live)
    {
        await Save(live); var publisher = new WidgetDesignPublisher(Repository, () => Shared);
        await publisher.PublishIfChangedAsync();
        var manifests = new[] { Path.Combine(Shared, "catalog.json"), Path.Combine(Shared, "slots.json") };
        var timestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var path in manifests) File.SetLastWriteTimeUtc(path, timestamp);
        var payloads = Directory.GetFiles(Path.Combine(Private, "renders")).Concat(Directory.GetFiles(Path.Combine(Shared, "renders"))).ToArray();
        var locks = payloads.Select(x => File.Open(x, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try { await publisher.PublishIfChangedAsync(); }
        finally { foreach (var file in locks) file.Dispose(); }
        foreach (var path in manifests) Assert.Equal(timestamp, File.GetLastWriteTimeUtc(path));
    }
    [Fact]
    public async Task ExplicitRefreshBypassesCacheAndFailureRemainsRetryable()
    {
        var saved = await Save(); var publisher = new WidgetDesignPublisher(Repository, () => Shared);
        await publisher.PublishIfChangedAsync(); var before = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        using (File.Open(Repository.RenderPath(saved.RenderKey)!, FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")));
        await publisher.PublishIfChangedAsync(); await publisher.PublishAsync();
    }
    [Theory]
    [InlineData("render")] [InlineData("scene")] [InlineData("part")]
    public async Task ChangedSourceIsRevalidatedAndDoesNotReplacePublishedCatalog(string artifact)
    {
        var saved = await Save(live: true); var publisher = new WidgetDesignPublisher(Repository, () => Shared);
        await publisher.PublishIfChangedAsync(); var before = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        var path = artifact switch { "scene" => Repository.ScenePath(saved.RenderKey)!, "part" => Repository.SceneImagePath(saved.RenderKey, 0)!, _ => Repository.RenderPath(saved.RenderKey)! };
        var bytes = await File.ReadAllBytesAsync(path); var invalid = new byte[bytes.Length];
        await File.WriteAllBytesAsync(path, invalid); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        var error = await Record.ExceptionAsync(() => publisher.PublishIfChangedAsync());
        Assert.True(error is IOException or JsonException, error?.GetType().Name ?? "Missing validation failure");
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")));
        await File.WriteAllBytesAsync(path, bytes); await publisher.PublishIfChangedAsync();
    }
    [Theory]
    [InlineData(".png")] [InlineData(".scene.json")] [InlineData(".part-0.png")]
    public async Task MissingOrChangedInstalledArtifactsAreReinstalled(string suffix)
    {
        var saved = await Save(live: true); var publisher = new WidgetDesignPublisher(Repository, () => Shared);
        await publisher.PublishIfChangedAsync(); var target = Path.Combine(Shared, "renders", saved.RenderKey + suffix);
        var original = await File.ReadAllBytesAsync(target);
        File.Delete(target); await publisher.PublishIfChangedAsync(); Assert.Equal(original, await File.ReadAllBytesAsync(target));
        await File.WriteAllBytesAsync(target, new byte[original.Length]); File.SetLastWriteTimeUtc(target, DateTime.UtcNow.AddMinutes(1));
        await publisher.PublishIfChangedAsync(); Assert.Equal(original, await File.ReadAllBytesAsync(target));
    }
    [Fact]
    public async Task ChangedDesignWithSameArtifactIdentityCannotReuseValidationOfOldScene()
    {
        var saved = await Save(live: true); var publisher = new WidgetDesignPublisher(Repository, () => Shared);
        await publisher.PublishIfChangedAsync(); var before = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        var library = await Repository.ReadLibraryAsync();
        var changed = saved with { Layers = saved.Layers.Select(x => x.Kind == WidgetLayerKind.Clock ? x with { Frame = x.Frame with { Y = .2 } } : x).ToArray() };
        await File.WriteAllBytesAsync(Path.Combine(Private, "catalog.json"), JsonSerializer.SerializeToUtf8Bytes(library with { Designs = [changed] }, WidgetDesignRules.JsonContext.WidgetLibraryCatalog));
        await Assert.ThrowsAsync<IOException>(() => publisher.PublishIfChangedAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")));
    }
    [Theory]
    [InlineData("catalog.json")] [InlineData("slots.json")]
    public async Task CachedImagesNeverBypassFutureSharedMetadata(string file)
    {
        await Save(); var publisher = new WidgetDesignPublisher(Repository, () => Shared); await publisher.PublishIfChangedAsync();
        var future = "{\"schemaVersion\":999,\"" + (file == "slots.json" ? "slots" : "designs") + "\":[]}";
        var path = Path.Combine(Shared, file); await File.WriteAllTextAsync(path, future);
        await Assert.ThrowsAsync<IOException>(() => publisher.PublishIfChangedAsync()); Assert.Equal(future, await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task NewContainerNewGenerationAssignmentsAndDeletionPublishWithoutRestartingPublisher()
    {
        var saved = await Save(live: true); var directory = Shared; var publisher = new WidgetDesignPublisher(Repository, () => directory);
        await publisher.PublishIfChangedAsync(); directory = Path.Combine(root, "remapped"); await publisher.PublishIfChangedAsync();
        Assert.True(File.Exists(Path.Combine(directory, "renders", saved.RenderKey + ".png")));
        await Repository.AssignSlotAsync(saved.Size, 7, saved.Id); await publisher.PublishIfChangedAsync();
        var slots = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(directory, "slots.json")), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        Assert.Equal(saved.Id, Assert.Single(slots.Slots).DesignId);
        var scene = WidgetScenes.Manifest(saved);
        var next = await Repository.SaveAsync(saved with { Name = "Changed" }, Png, scene: new(scene, scene.Parts.Where(x => x.ImageIndex is not null).ToDictionary(x => x.ImageIndex!.Value, _ => Png)));
        await publisher.PublishIfChangedAsync(); Assert.NotEqual(saved.RenderKey, next.RenderKey);
        Assert.True(File.Exists(Path.Combine(directory, "renders", next.RenderKey + ".png")));
        // Cached timelines keep the old generation until the existing retention cutoff.
        Assert.True(File.Exists(Path.Combine(directory, "renders", saved.RenderKey + ".png")));
        await Repository.DeleteAsync(saved.Id); await publisher.PublishIfChangedAsync();
        var catalog = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(directory, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.Empty(catalog.Designs);
        slots = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(directory, "slots.json")), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        Assert.Empty(slots.Slots);
    }
    [Fact]
    public async Task CanceledCachedPublicationDoesNotReplaceSharedCatalogAndCanRetry()
    {
        await Save(); var publisher = new WidgetDesignPublisher(Repository, () => Shared); await publisher.PublishIfChangedAsync();
        var before = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishIfChangedAsync(cancel.Token));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"))); await publisher.PublishIfChangedAsync();
    }
    [Fact]
    public async Task ExplicitRefreshRepairsInstalledBytesEvenWhenFileMetadataIsUnchanged()
    {
        var saved = await Save(); var publisher = new WidgetDesignPublisher(Repository, () => Shared); await publisher.PublishIfChangedAsync();
        var target = Path.Combine(Shared, "renders", saved.RenderKey + ".png"); var timestamp = File.GetLastWriteTimeUtc(target);
        await File.WriteAllBytesAsync(target, new byte[Png.Length]); File.SetLastWriteTimeUtc(target, timestamp);
        await publisher.PublishAsync(); Assert.Equal(Png, await File.ReadAllBytesAsync(target));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
