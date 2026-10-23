using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetDesignRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ControlStudioWidgets", Guid.NewGuid().ToString("N"));
    private string Private => Path.Combine(root, "private");
    private string Shared => Path.Combine(root, "shared");
    private WidgetDesignRepository Repository() => new(Private);
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=");
    private async Task<WidgetDesign> PhotoDraft()
    {
        var key = await Repository().ImportPhotoAsync(Png);
        return WidgetDesignRules.New() with { Layers = [new WidgetLayer(Guid.NewGuid(), "Photo 1", key, 1, 1, new(.1, .2, .5, .5), 2, .3, .7)] };
    }
    [Fact]
    public async Task PortraitAndLandscapePersistAndPublishAsDistinctDesigns()
    {
        var repo = Repository();
        var portrait = await repo.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.ExtraLargePortrait }, Png);
        var landscape = await repo.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.ExtraLarge }, Png);
        var reopened = await Repository().ReadAsync();
        Assert.Equal(WidgetSize.ExtraLargePortrait, reopened.Single(x => x.Id == portrait.Id).Size);
        Assert.Equal(WidgetSize.ExtraLarge, reopened.Single(x => x.Id == landscape.Id).Size);
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var catalog = JsonSerializer.Deserialize<WidgetCatalog>(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.Json)!;
        Assert.Equal(2, catalog.Designs.Count);
        Assert.Equal(WidgetSize.ExtraLargePortrait, catalog.Designs.Single(x => x.Id == portrait.Id).Size);
        Assert.Equal(WidgetSize.ExtraLarge, catalog.Designs.Single(x => x.Id == landscape.Id).Size);
    }
    [Fact]
    public async Task CreateEditAndDeleteSurviveReopeningAndRetainLayerIdentity()
    {
        var repo = Repository(); var draft = await PhotoDraft(); var original = Assert.Single(draft.Layers);
        var saved = await repo.SaveAsync(draft, Png, thumbnail: Png); Assert.NotNull(repo.ThumbnailPath(saved.RenderKey));
        var edited = await Repository().SaveAsync(saved with { Name = "Edited", Size = WidgetSize.ExtraLarge, Layers = [original with { Visible = false }] }, Png);
        var reopened = Assert.Single(await Repository().ReadAsync());
        Assert.Equal(saved.Id, reopened.Id); Assert.Equal(2, reopened.Revision);
        Assert.Equal(WidgetSize.ExtraLarge, reopened.Size); Assert.Equal(original with { Visible = false }, Assert.Single(reopened.Layers));
        Assert.NotEqual(saved.RenderKey, edited.RenderKey); Assert.NotNull(repo.RenderPath(edited.RenderKey));
        Assert.Null(repo.ThumbnailPath(saved.RenderKey));
        await Repository().DeleteAsync(saved.Id); Assert.Empty(await Repository().ReadAsync()); Assert.Null(repo.AssetPath(original.AssetKey));
    }
    [Fact]
    public async Task CancelAndFailedValidationDoNotMutateSavedDesigns()
    {
        var repo = Repository(); var saved = await repo.SaveAsync(await PhotoDraft(), Png);
        var before = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.SaveAsync(saved with { Name = "Canceled" }, Png, cancellation.Token));
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(saved with { BackgroundHex = "invalid" }, Png));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        var unused = await repo.ImportPhotoAsync(Png);
        await repo.RemoveDraftPhotosAsync([unused, saved.Layers[0].AssetKey]);
        Assert.Null(repo.AssetPath(unused)); Assert.NotNull(repo.AssetPath(saved.Layers[0].AssetKey));
    }
    [Fact]
    public async Task DraftCleanupAndDeletionKeepSourcesReferencedByOtherDesigns()
    {
        var repo = Repository(); var first = await repo.SaveAsync(await PhotoDraft(), Png);
        var second = await repo.SaveAsync(first with { Id = Guid.NewGuid(), Revision = 0, Name = "Other design" }, Png);
        await repo.DeleteAsync(first.Id); Assert.NotNull(repo.AssetPath(second.Layers[0].AssetKey));
        await repo.SaveAsync(second with { Layers = [] }, Png); Assert.Null(repo.AssetPath(second.Layers[0].AssetKey));
    }
    [Fact]
    public async Task ConcurrentInstancesDoNotLoseNewDesignsAndStaleEditorsCannotOverwriteOrResurrect()
    {
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Repository().SaveAsync(WidgetDesignRules.New() with { Name = "Widget " + i }, Png)));
        var all = await Repository().ReadAsync(); Assert.Equal(12, all.Count); Assert.Equal(12, all.Select(x => x.Id).Distinct().Count());
        var original = all[0]; await Repository().SaveAsync(original with { Name = "Latest" }, Png);
        await Assert.ThrowsAsync<IOException>(() => Repository().SaveAsync(original with { Name = "Stale" }, Png));
        await Repository().DeleteAsync(original.Id);
        await Assert.ThrowsAsync<IOException>(() => Repository().SaveAsync(original, Png));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":3,\"designs\":[]}")]
    [InlineData("{broken")]
    [InlineData("{\"schemaVersion\":1,\"designs\":null}")]
    public async Task UnsupportedOrCorruptCatalogIsNotResetOrOverwritten(string contents)
    {
        Directory.CreateDirectory(Private); var path = Path.Combine(Private, "catalog.json"); await File.WriteAllTextAsync(path, contents);
        await Assert.ThrowsAsync<IOException>(() => Repository().ReadAsync());
        await Assert.ThrowsAsync<IOException>(() => Repository().SaveAsync(WidgetDesignRules.New(), Png));
        Assert.Equal(contents, await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task PublisherKeepsPreviousSnapshotOnFailureAndCanRecover()
    {
        var repo = Repository(); var publisher = new WidgetDesignPublisher(repo, () => Shared);
        var first = await repo.SaveAsync(await PhotoDraft(), Png); await publisher.PublishAsync();
        var manifest = Path.Combine(Shared, "catalog.json"); var before = await File.ReadAllBytesAsync(manifest);
        var second = await repo.SaveAsync(first with { Name = "New" }, Png);
        File.Delete(repo.RenderPath(second.RenderKey)!);
        await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(manifest));
        Assert.True(File.Exists(Path.Combine(Shared, "renders", first.RenderKey + ".png")));
        var recovered = await repo.SaveAsync(second, Png); await publisher.PublishAsync();
        var shared = JsonSerializer.Deserialize<WidgetCatalog>(await File.ReadAllBytesAsync(manifest), WidgetDesignRules.Json)!;
        Assert.Equal(recovered.RenderKey, Assert.Single(shared.Designs).RenderKey);
        await repo.DeleteAsync(first.Id); await publisher.PublishAsync();
        Assert.Empty(JsonSerializer.Deserialize<WidgetCatalog>(await File.ReadAllBytesAsync(manifest), WidgetDesignRules.Json)!.Designs);
        Assert.True(File.Exists(Path.Combine(Shared, "renders", recovered.RenderKey + ".png")));
    }
    [Fact]
    public async Task PublicationFailureLeavesPrivateSaveIntactAndFutureSharedCatalogIsPreserved()
    {
        var repo = Repository(); var saved = await repo.SaveAsync(WidgetDesignRules.New(), Png);
        var unavailable = new WidgetDesignPublisher(repo, () => throw new IOException("Unavailable"));
        await Assert.ThrowsAsync<IOException>(() => unavailable.PublishAsync());
        Assert.Equal(saved.Id, Assert.Single(await Repository().ReadAsync()).Id);
        Directory.CreateDirectory(Shared); var path = Path.Combine(Shared, "catalog.json");
        const string future = "{\"schemaVersion\":2,\"designs\":[]}"; await File.WriteAllTextAsync(path, future);
        await Assert.ThrowsAsync<IOException>(() => new WidgetDesignPublisher(repo, () => Shared).PublishAsync());
        Assert.Equal(future, await File.ReadAllTextAsync(path));
    }
    [Fact]
    public async Task SameSizeNamesAreDistinctInTheDesignLibrary()
    {
        var repo = Repository(); var first = await repo.SaveAsync(WidgetDesignRules.New(), Png);
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(WidgetDesignRules.New() with { Name = " my WIDGET " }, Png));
        await repo.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.Medium }, Png);
        Assert.Equal(2, (await repo.ReadAsync()).Count);
        await repo.SaveAsync(first with { Name = "Updated name" }, Png);
    }
    [Fact]
    public async Task ImageLimitsAndPathTraversalAreRejectedWithoutTouchingOtherFiles()
    {
        var repo = Repository(); Assert.Null(repo.AssetPath("../secret")); Assert.Null(repo.RenderPath("../secret"));
        await Assert.ThrowsAsync<IOException>(() => repo.ImportPhotoAsync(new byte[24]));
        var huge = Png; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(huge.AsSpan(16, 4), 4096);
        await Assert.ThrowsAsync<IOException>(() => repo.ImportPhotoAsync(huge));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
