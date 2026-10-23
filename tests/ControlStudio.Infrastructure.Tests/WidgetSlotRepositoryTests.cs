using System.Text.Json;
using ControlStudio.Core.Widgets;
using ControlStudio.Infrastructure;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetSlotRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ControlStudioSlots", Guid.NewGuid().ToString("N"));
    private string Private => Path.Combine(root, "private");
    private string Shared => Path.Combine(root, "shared");
    private string Catalog => Path.Combine(Private, "catalog.json");
    private WidgetDesignRepository Repository => new(Private);
    private WidgetDesignPublisher Publisher => new(Repository, () => Shared);
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=");
    [Fact]
    public async Task ExistingV1LibraryReadsWithoutRewriteAndFirstAssignmentUpgradesWithoutLosingData()
    {
        var source = await Repository.ImportPhotoAsync(Png);
        var draft = WidgetDesignRules.New() with { Layers = [new WidgetLayer(Guid.NewGuid(), "Photo", source, 1, 1, new())] };
        var saved = await Repository.SaveAsync(draft, Png, thumbnail: Png);
        var old = JsonSerializer.SerializeToUtf8Bytes(new WidgetCatalog(1, [saved]), WidgetDesignRules.JsonContext.WidgetCatalog);
        await File.WriteAllBytesAsync(Catalog, old);
        var read = await Repository.ReadLibraryAsync(); Assert.Empty(read.Slots);
        Assert.Equal(old, await File.ReadAllBytesAsync(Catalog));
        await Repository.AssignSlotAsync(saved.Size, 1, saved.Id);
        var reopened = await Repository.ReadLibraryAsync(); Assert.Equal(2, reopened.SchemaVersion);
        Assert.Equal(JsonSerializer.Serialize(saved, WidgetDesignRules.JsonContext.WidgetDesign),
            JsonSerializer.Serialize(Assert.Single(reopened.Designs), WidgetDesignRules.JsonContext.WidgetDesign));
        Assert.Equal(saved.Id, Assert.Single(reopened.Slots).DesignId);
        Assert.Equal(Png, await File.ReadAllBytesAsync(Repository.RenderPath(saved.RenderKey)!));
        Assert.NotNull(Repository.ThumbnailPath(saved.RenderKey));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Repository.AssetPath(source)!));
    }
    [Fact]
    public async Task ConcurrentInstancesKeepIndependentSlotsSizesAndClearOnlyTheChosenAddress()
    {
        var small = await Repository.SaveAsync(WidgetDesignRules.New(), Png);
        var medium = await Repository.SaveAsync(WidgetDesignRules.New() with { Size = WidgetSize.Medium }, Png);
        await Task.WhenAll(Enumerable.Range(1, 10).SelectMany(number => new[]
        {
            Repository.AssignSlotAsync(WidgetSize.Small, number, small.Id),
            Repository.AssignSlotAsync(WidgetSize.Medium, number, medium.Id)
        }));
        Assert.Equal(20, (await Repository.ReadLibraryAsync()).Slots.Count);
        await Repository.AssignSlotAsync(WidgetSize.Small, 1, null);
        var slots = (await Repository.ReadLibraryAsync()).Slots; Assert.Equal(19, slots.Count);
        Assert.DoesNotContain(slots, x => x.Size == WidgetSize.Small && x.Number == 1);
        Assert.Contains(slots, x => x.Size == WidgetSize.Medium && x.Number == 1 && x.DesignId == medium.Id);
    }
    [Fact]
    public async Task RenameAndPhotoEditKeepAssignmentsButResizeAndDeleteClearThemAtomically()
    {
        var saved = await Repository.SaveAsync(WidgetDesignRules.New(), Png);
        await Repository.AssignSlotAsync(saved.Size, 1, saved.Id); await Repository.AssignSlotAsync(saved.Size, 2, saved.Id);
        var edited = await Repository.SaveAsync(saved with { Name = "Updated photo" }, Png);
        Assert.Equal(2, (await Repository.ReadLibraryAsync()).Slots.Count);
        Assert.NotEqual(saved.RenderKey, edited.RenderKey);
        var resized = await Repository.SaveAsync(edited with { Size = WidgetSize.Medium }, Png);
        Assert.Empty((await Repository.ReadLibraryAsync()).Slots);
        var restored = await Repository.SaveAsync(resized with { Size = WidgetSize.Small }, Png);
        Assert.Empty((await Repository.ReadLibraryAsync()).Slots);
        await Repository.AssignSlotAsync(restored.Size, 10, restored.Id); await Repository.DeleteAsync(restored.Id);
        var empty = await Repository.ReadLibraryAsync(); Assert.Empty(empty.Designs); Assert.Empty(empty.Slots);
    }
    [Fact]
    public async Task InvalidDeletedWrongSizeAndCanceledAssignmentsLeaveTheLibraryByteIdentical()
    {
        var saved = await Repository.SaveAsync(WidgetDesignRules.New(), Png);
        await Repository.AssignSlotAsync(saved.Size, 1, saved.Id);
        var bytes = await File.ReadAllBytesAsync(Catalog);
        await Assert.ThrowsAsync<IOException>(() => Repository.AssignSlotAsync(WidgetSize.Medium, 1, saved.Id));
        await Assert.ThrowsAsync<IOException>(() => Repository.AssignSlotAsync(WidgetSize.Small, 11, saved.Id));
        await Assert.ThrowsAsync<IOException>(() => Repository.AssignSlotAsync(saved.Size, 1, Guid.NewGuid()));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository.AssignSlotAsync(saved.Size, 1, null, cancel.Token));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Catalog));
    }
    [Theory]
    [InlineData("{\"schemaVersion\":3,\"designs\":[],\"slots\":[]}")]
    [InlineData("{\"schemaVersion\":2,\"designs\":[],\"slots\":null}")]
    [InlineData("{\"schemaVersion\":\"2\",\"designs\":[],\"slots\":[]}")]
    [InlineData("{broken")]
    public async Task UnsupportedAndCorruptLibrariesArePreserved(string data)
    {
        Directory.CreateDirectory(Private); await File.WriteAllTextAsync(Catalog, data);
        await Assert.ThrowsAsync<IOException>(() => Repository.ReadLibraryAsync());
        await Assert.ThrowsAsync<IOException>(() => Repository.AssignSlotAsync(WidgetSize.Small, 1, null));
        await Assert.ThrowsAsync<IOException>(() => Repository.SaveAsync(WidgetDesignRules.New(), Png));
        Assert.Equal(data, await File.ReadAllTextAsync(Catalog));
    }
    [Fact]
    public async Task PublicationInstallsExactAssignmentsAndFailureKeepsTheLastSharedSlots()
    {
        var first = await Repository.SaveAsync(WidgetDesignRules.New(), Png);
        var second = await Repository.SaveAsync(WidgetDesignRules.New() with { Name = "Second" }, Png);
        await Repository.AssignSlotAsync(WidgetSize.Small, 1, first.Id); await Publisher.PublishAsync();
        var path = Path.Combine(Shared, "slots.json"); var old = await File.ReadAllBytesAsync(path);
        await Repository.AssignSlotAsync(WidgetSize.Small, 1, second.Id);
        File.Delete(Repository.RenderPath(second.RenderKey)!);
        await Assert.ThrowsAsync<IOException>(() => Publisher.PublishAsync());
        Assert.Equal(old, await File.ReadAllBytesAsync(path));
        Assert.Equal(second.Id, Assert.Single((await Repository.ReadLibraryAsync()).Slots).DesignId);
        await Repository.SaveAsync(second, Png); await Publisher.PublishAsync();
        var shared = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(path), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        Assert.Equal(second.Id, Assert.Single(shared.Slots).DesignId);
    }
    [Fact]
    public async Task FutureSharedSlotsAreNotOverwrittenAndDeletedLastDesignPublishesEmpty()
    {
        var saved = await Repository.SaveAsync(WidgetDesignRules.New(), Png);
        await Repository.AssignSlotAsync(saved.Size, 1, saved.Id); await Publisher.PublishAsync();
        var path = Path.Combine(Shared, "slots.json"); var good = await File.ReadAllBytesAsync(path);
        const string future = "{\"schemaVersion\":2,\"slots\":[]}";
        await File.WriteAllTextAsync(path, future);
        await Assert.ThrowsAsync<IOException>(() => Publisher.PublishAsync()); Assert.Equal(future, await File.ReadAllTextAsync(path));
        await File.WriteAllBytesAsync(path, good);
        var oldTestData = Path.Combine(Shared, "diagnostics", "events"); Directory.CreateDirectory(oldTestData);
        await File.WriteAllTextAsync(Path.Combine(oldTestData, "old.json"), "obsolete");
        await Repository.DeleteAsync(saved.Id); await Publisher.PublishAsync();
        Assert.Empty(JsonSerializer.Deserialize(await File.ReadAllBytesAsync(path), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!.Slots);
        Assert.Empty(JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!.Designs);
        Assert.False(Directory.Exists(Path.Combine(Shared, "diagnostics")));
        Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".png")));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
