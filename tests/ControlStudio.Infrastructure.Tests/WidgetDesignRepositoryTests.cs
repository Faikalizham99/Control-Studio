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
    [Fact]
    public async Task LayerBlendModesSurviveReopenPublicationAndRejectedSave()
    {
        var repo = Repository(); var draft = await PhotoDraft();
        var bottom = draft.Layers[0] with { BlendMode = WidgetBlendMode.Multiply, Opacity = .6 };
        var top = bottom with { Id = Guid.NewGuid(), Name = "Top", BlendMode = WidgetBlendMode.LighterColor, Visible = false };
        var saved = await repo.SaveAsync(draft with { Layers = [bottom, top] }, Png);
        await repo.AssignSlotAsync(saved.Size, 2, saved.Id);
        Assert.Equal(saved.Layers, Assert.Single(await Repository().ReadAsync()).Layers);
        var before = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(saved with { Layers = [bottom with { BlendMode = (WidgetBlendMode)999 }] }, Png));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        await new WidgetDesignPublisher(Repository(), () => Shared).PublishAsync();
        var published = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.Equal(saved.Layers, Assert.Single(published.Designs).Layers);
        Assert.Equal(saved.Id, Assert.Single((await Repository().ReadLibraryAsync()).Slots).DesignId);
    }
    [Fact]
    public async Task OffCanvasPhotoAndFitChoiceSurviveSaveReopenAndSlotPublication()
    {
        var repo = Repository(); var draft = await PhotoDraft();
        var photo = draft.Layers[0] with { Frame = new(-.4, 1.2, 2.4, 1.6), FitPhoto = true, QuarterTurns = 1, Opacity = .6 };
        var saved = await repo.SaveAsync(draft with { Layers = [photo] }, Png);
        await repo.AssignSlotAsync(saved.Size, 3, saved.Id);
        var reopened = Assert.Single(await Repository().ReadAsync()); Assert.Equal(photo, Assert.Single(reopened.Layers));
        var privateBytes = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        await new WidgetDesignPublisher(Repository(), () => Shared).PublishAsync();
        var published = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.Equal(photo, Assert.Single(Assert.Single(published.Designs).Layers));
        Assert.True(Assert.Single(published.Designs).PreserveCanvas);
        Assert.Equal(saved.Id, Assert.Single((await Repository().ReadLibraryAsync()).Slots).DesignId);
        Assert.Equal(privateBytes, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        Assert.Equal(Png, await File.ReadAllBytesAsync(repo.AssetPath(photo.AssetKey)!));
    }
    [Fact]
    public async Task DisplayTimestampReadsExistingRenderWithoutChangingLibraryOrPhotos()
    {
        var repo = Repository(); var design = await repo.SaveAsync(WidgetDesignRules.New(), Png);
        var path = repo.RenderPath(design.RenderKey)!;
        var stamp = new DateTime(2026, 10, 23, 4, 0, 0, DateTimeKind.Utc); File.SetLastWriteTimeUtc(path, stamp);
        var catalog = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        Assert.Equal(new DateTimeOffset(stamp), repo.RenderUpdatedAt(design.RenderKey));
        Assert.Equal(catalog, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        Assert.Equal(Png, await File.ReadAllBytesAsync(path));
        Assert.Null(repo.RenderUpdatedAt("../invalid"));
        Assert.Null(repo.RenderUpdatedAt(Guid.NewGuid().ToString("N")));
    }
    private async Task<WidgetDesign> PhotoDraft()
    {
        var key = await Repository().ImportPhotoAsync(Png);
        return WidgetDesignRules.New() with { Layers = [new WidgetLayer(Guid.NewGuid(), "Photo 1", key, 1, 1, new(.1, .2, .5, .5), 2, .3, .7)] };
    }
    [Fact]
    public async Task TransparentTransformsSurviveSavePublishReopenAndFailedEdits()
    {
        var repo = Repository(); var draft = await PhotoDraft();
        draft = draft with { Layers = [draft.Layers[0] with { QuarterTurns = 3, FlipHorizontal = true, FlipVertical = true, Opacity = .45 }] };
        var saved = await repo.SaveAsync(draft, Png, thumbnail: Png); await repo.AssignSlotAsync(saved.Size, 2, saved.Id);
        var reopened = Assert.Single(await Repository().ReadAsync());
        Assert.True(reopened.TransparentBackground); Assert.Equal(draft.Layers, reopened.Layers);
        var before = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(reopened with { Layers = [reopened.Layers[0] with { Opacity = 2 }] }, Png));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var shared = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        var published = Assert.Single(shared.Designs); Assert.True(published.TransparentBackground); Assert.Equal(draft.Layers, published.Layers);
        Assert.Equal(saved.Id, Assert.Single((await Repository().ReadLibraryAsync()).Slots).DesignId);
        var solid = await repo.SaveAsync(reopened with { TransparentBackground = false, BackgroundHex = "#FFFFFF" }, Png);
        Assert.False(Assert.Single(await Repository().ReadAsync()).TransparentBackground);
        Assert.Equal(saved.Id, solid.Id); Assert.Equal(saved.Revision + 1, solid.Revision);
    }
    [Fact]
    public async Task RetiredDesignsStayPrivateUntilExplicitResizeAndCannotStealExistingPhoneSlots()
    {
        var repo = Repository();
        var portrait = await repo.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.ExtraLargePortrait }, Png);
        var landscape = await repo.SaveAsync((await PhotoDraft()) with { Name = "Retired design", Size = WidgetSize.Large }, Png);
        // Simulate a library written by the previous version. New saves reject this size.
        landscape = landscape with { Size = WidgetSize.ExtraLarge };
        var library = new WidgetLibraryCatalog(2, [portrait, landscape], [new(portrait.Size, 1, portrait.Id), new(landscape.Size, 1, landscape.Id)]);
        var path = Path.Combine(Private, "catalog.json");
        var before = JsonSerializer.SerializeToUtf8Bytes(library, WidgetDesignRules.JsonContext.WidgetLibraryCatalog);
        await File.WriteAllBytesAsync(path, before);
        var reopened = await Repository().ReadAsync();
        Assert.Equal(WidgetSize.ExtraLargePortrait, reopened.Single(x => x.Id == portrait.Id).Size);
        Assert.Equal(WidgetSize.ExtraLarge, reopened.Single(x => x.Id == landscape.Id).Size);
        Directory.CreateDirectory(Shared);
        await File.WriteAllBytesAsync(Path.Combine(Shared, "catalog.json"), JsonSerializer.SerializeToUtf8Bytes(new WidgetCatalog(1, library.Designs), WidgetDesignRules.JsonContext.WidgetCatalog));
        await File.WriteAllBytesAsync(Path.Combine(Shared, "slots.json"), JsonSerializer.SerializeToUtf8Bytes(new WidgetSlotCatalog(1, library.Slots), WidgetDesignRules.JsonContext.WidgetSlotCatalog));
        File.Delete(repo.RenderPath(landscape.RenderKey)!); // A retired render must not block valid iPhone widgets.
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var catalog = JsonSerializer.Deserialize<WidgetCatalog>(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.Json)!;
        Assert.Single(catalog.Designs);
        Assert.Equal(WidgetSize.ExtraLargePortrait, catalog.Designs.Single(x => x.Id == portrait.Id).Size);
        var slots = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "slots.json")), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        Assert.Equal(portrait.Id, Assert.Single(slots.Slots).DesignId);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(landscape, Png));
        await Assert.ThrowsAsync<IOException>(() => repo.AssignSlotAsync(WidgetSize.ExtraLarge, 2, landscape.Id));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        var resized = await repo.SaveAsync(WidgetDesignRules.ForEditing(landscape), Png);
        Assert.Equal(landscape.Id, resized.Id); Assert.Equal(landscape.Revision + 1, resized.Revision);
        Assert.Equal(landscape.Layers, resized.Layers); Assert.NotNull(repo.AssetPath(landscape.Layers[0].AssetKey));
        Assert.Equal(portrait.Id, Assert.Single((await repo.ReadLibraryAsync()).Slots).DesignId);
        await repo.AssignSlotAsync(resized.Size, 2, resized.Id);
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var after = await repo.ReadLibraryAsync(); Assert.Equal(2, after.Slots.Count);
        Assert.Contains(after.Slots, x => x.Number == 1 && x.DesignId == portrait.Id);
        Assert.Contains(after.Slots, x => x.Number == 2 && x.DesignId == resized.Id);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task OldLibrariesAndPhotosRemainUnchangedWhenResizeIsCanceledOrConflicts(int schema)
    {
        var repo = Repository();
        var phone = await repo.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.ExtraLargePortrait }, Png);
        var old = (await repo.SaveAsync((await PhotoDraft()) with { Size = WidgetSize.Large }, Png)) with { Size = WidgetSize.ExtraLarge };
        var bytes = schema == 1
            ? JsonSerializer.SerializeToUtf8Bytes(new WidgetCatalog(1, [phone, old]), WidgetDesignRules.JsonContext.WidgetCatalog)
            : JsonSerializer.SerializeToUtf8Bytes(new WidgetLibraryCatalog(2, [phone, old], [new(phone.Size, 1, phone.Id), new(old.Size, 1, old.Id)]), WidgetDesignRules.JsonContext.WidgetLibraryCatalog);
        var path = Path.Combine(Private, "catalog.json"); await File.WriteAllBytesAsync(path, bytes);
        var library = await repo.ReadLibraryAsync(); Assert.Equal(2, library.Designs.Count);
        var draft = WidgetDesignRules.ForEditing(library.Designs.Single(x => x.Id == old.Id));
        // Opening/canceling is read-only. A duplicate-name resize cannot overwrite either design.
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(draft, Png));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Equal(Png, await File.ReadAllBytesAsync(repo.AssetPath(old.Layers[0].AssetKey)!));
        Assert.Equal(Png, await File.ReadAllBytesAsync(repo.RenderPath(old.RenderKey)!));
    }
    [Fact]
    public async Task CreateEditAndDeleteSurviveReopeningAndRetainLayerIdentity()
    {
        var repo = Repository(); var draft = await PhotoDraft(); var original = Assert.Single(draft.Layers);
        var saved = await repo.SaveAsync(draft, Png, thumbnail: Png); Assert.NotNull(repo.ThumbnailPath(saved.RenderKey));
        var edited = await Repository().SaveAsync(saved with { Name = "Edited", Size = WidgetSize.ExtraLargePortrait, Layers = [original with { Visible = false }] }, Png);
        var reopened = Assert.Single(await Repository().ReadAsync());
        Assert.Equal(saved.Id, reopened.Id); Assert.Equal(2, reopened.Revision);
        Assert.Equal(WidgetSize.ExtraLargePortrait, reopened.Size); Assert.Equal(original with { Visible = false }, Assert.Single(reopened.Layers));
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
