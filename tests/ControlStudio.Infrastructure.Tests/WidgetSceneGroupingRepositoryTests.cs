using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetSceneGroupingRepositoryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "WidgetGrouping", Guid.NewGuid().ToString("N"));
    private string Private => Path.Combine(root, "private");
    private string Shared => Path.Combine(root, "shared");
    private WidgetDesignRepository Repository() => new(Private);
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=");
    private static WidgetLayer Live(WidgetLayerKind kind) => kind switch
    { WidgetLayerKind.Date => WidgetDates.New(), WidgetLayerKind.Battery => WidgetBatteries.New(), _ => WidgetClocks.New() };
    private static WidgetSceneExport Export(WidgetDesign design)
    {
        var scene = WidgetScenes.Manifest(design);
        return new(scene, scene.Parts.Where(part => part.ImageIndex is not null).ToDictionary(part => part.ImageIndex!.Value, _ => Png));
    }
    private Dictionary<string, byte[]> PrivateFiles() => Directory.GetFiles(Private, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(Private, path), File.ReadAllBytes);

    [Theory]
    [InlineData(WidgetLayerKind.Clock, false)] [InlineData(WidgetLayerKind.Clock, true)]
    [InlineData(WidgetLayerKind.Date, false)] [InlineData(WidgetLayerKind.Date, true)]
    [InlineData(WidgetLayerKind.Battery, false)] [InlineData(WidgetLayerKind.Battery, true)]
    public async Task RefreshPreservesOldSceneAndSavingUpgradesOnlyThatDesignWithoutLosingItsSlot(WidgetLayerKind kind, bool zeroOpacity)
    {
        var repository = Repository(); var key = await repository.ImportPhotoAsync(Png);
        var bottom = new WidgetLayer(Guid.NewGuid(), "Bottom", key, 1, 1, new());
        var top = bottom with { Id = Guid.NewGuid(), Name = "Multiply", BlendMode = WidgetBlendMode.Multiply, Locked = true };
        var hidden = zeroOpacity ? Live(kind) with { Opacity = 0 } : Live(kind) with { Visible = false };
        var visible = Live(kind) with { Id = Guid.NewGuid(), Name = "Visible live layer" };
        var draft = WidgetDesignRules.New() with { Layers = [bottom, hidden, top, visible] };
        var saved = await repository.SaveAsync(draft, Png, thumbnail: Png, scene: Export(draft));
        await repository.AssignSlotAsync(saved.Size, 3, saved.Id);
        // Seed an installed pre-fix export. Old binaries omitted groupingVersion
        // and split the photos into two image parts at the invisible live layer.
        var part = kind switch { WidgetLayerKind.Date => new WidgetScenePart(Date: visible), WidgetLayerKind.Battery => new WidgetScenePart(Battery: visible), _ => new WidgetScenePart(Clock: visible) };
        var old = new WidgetScene(WidgetScenes.Manifest(saved).SchemaVersion, 170, 170, [new(0), new(1), part]);
        var fields = JsonSerializer.SerializeToElement(old, WidgetDesignRules.JsonContext.WidgetScene).EnumerateObject()
            .Where(property => property.Name != "groupingVersion").ToDictionary(property => property.Name, property => property.Value.Clone());
        var originalScene = JsonSerializer.SerializeToUtf8Bytes(fields);
        await File.WriteAllBytesAsync(repository.ScenePath(saved.RenderKey)!, originalScene);
        await File.WriteAllBytesAsync(Path.Combine(Private, "renders", saved.RenderKey + ".part-1.png"), Png);
        var before = PrivateFiles();
        var publisher = new WidgetDesignPublisher(Repository(), () => Shared);
        await publisher.PublishAsync(); await publisher.PublishAsync(); // Refresh/retry never migrates source data.
        var after = PrivateFiles(); Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        var publishedScenePath = Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json");
        Assert.Equal(originalScene, await File.ReadAllBytesAsync(publishedScenePath));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".part-1.png")));
        var reopened = Assert.Single(await Repository().ReadAsync()); Assert.Equal(saved.Layers, reopened.Layers);

        // A failed/stale export cannot replace the working source of truth.
        var catalogBefore = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        await Assert.ThrowsAsync<IOException>(() => repository.SaveAsync(reopened, Png, scene: new(old, new Dictionary<int, byte[]> { [0] = Png, [1] = Png })));
        await Assert.ThrowsAsync<IOException>(() => repository.SaveAsync(reopened, Png, scene: Export(reopened) with { Scene = old with { GroupingVersion = 2 } }));
        Assert.Equal(catalogBefore, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
        var updated = await repository.SaveAsync(reopened, Png, thumbnail: Png, scene: Export(reopened));
        Assert.Equal(saved.Id, updated.Id); Assert.Equal(saved.Revision + 1, updated.Revision);
        Assert.NotEqual(saved.RenderKey, updated.RenderKey); Assert.Equal(saved.Layers, updated.Layers);
        Assert.Equal(saved.Id, Assert.Single((await Repository().ReadLibraryAsync()).Slots).DesignId);
        Assert.Equal(Png, await File.ReadAllBytesAsync(repository.AssetPath(key)!));
        await publisher.PublishAsync();
        var current = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", updated.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.Equal(2, current.GroupingVersion); Assert.Equal(new int?[] { 0, null }, current.Parts.Select(item => item.ImageIndex));
        WidgetScenes.Validate(current, updated);
        Assert.False(File.Exists(Path.Combine(Shared, "renders", updated.RenderKey + ".part-1.png")));
        Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".part-1.png"))); // Cached timeline retention.
        Assert.Equal(originalScene, await File.ReadAllBytesAsync(publishedScenePath));
    }

    [Fact]
    public async Task UnknownGroupingCannotOverwriteTheLastPublishedCatalogOrPrivateLibrary()
    {
        var repository = Repository(); var draft = WidgetDesignRules.New() with { Layers = [WidgetClocks.New()] };
        var saved = await repository.SaveAsync(draft, Png, scene: Export(draft)); await repository.AssignSlotAsync(saved.Size, 5, saved.Id);
        var publisher = new WidgetDesignPublisher(repository, () => Shared); await publisher.PublishAsync();
        var beforeCatalog = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        var beforeSlots = await File.ReadAllBytesAsync(Path.Combine(Shared, "slots.json"));
        var original = await File.ReadAllBytesAsync(repository.ScenePath(saved.RenderKey)!);
        foreach (var version in new[] { 0, 3 })
        {
            var scene = WidgetScenes.Manifest(saved) with { GroupingVersion = version };
            var invalid = JsonSerializer.SerializeToUtf8Bytes(scene, WidgetDesignRules.JsonContext.WidgetScene);
            await File.WriteAllBytesAsync(repository.ScenePath(saved.RenderKey)!, invalid);
            var privateBefore = PrivateFiles();
            await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync());
            foreach (var (path, bytes) in privateBefore) Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(Private, path)));
            Assert.Equal(beforeCatalog, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")));
            Assert.Equal(beforeSlots, await File.ReadAllBytesAsync(Path.Combine(Shared, "slots.json")));
            Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json")));
        }
    }

    [Fact]
    public async Task LegacySceneNearTheByteLimitPublishesUsingItsActualBytesWithoutAddingNewMetadata()
    {
        byte[] LegacyBytes(WidgetDesign design)
        {
            var scene = WidgetScenes.Manifest(design) with { GroupingVersion = 1 };
            var fields = JsonSerializer.SerializeToElement(scene, WidgetDesignRules.JsonContext.WidgetScene).EnumerateObject()
                .Where(property => property.Name != "groupingVersion").ToDictionary(property => property.Name, property => property.Value.Clone());
            return JsonSerializer.SerializeToUtf8Bytes(fields);
        }
        var stops = new[] { 0, 14, 28, 42, 56, 70, 84, 100 }.Select(value => new WidgetBatteryColourStop(value, "#123456")).ToArray();
        var layers = Enumerable.Range(0, 30).Select(_ => WidgetBatteries.New(new string('A', 40)) with
            { Battery = WidgetBatteries.Defaults() with { Stops = stops }, Frame = new(.1234567890123456, .2345678901234567, 1.2345678901234567, .9234567890123456) }).ToArray();
        var design = WidgetDesignRules.New() with { Revision = 1, Layers = layers };
        // Each escaped CJK character replaces one ASCII byte with six bytes.
        var count = (WidgetScenes.MaximumBytes - LegacyBytes(design).Length) / 5;
        Assert.InRange(count, 1, 1200);
        design = design with { Layers = layers.Select((layer, index) =>
        {
            var chars = Math.Clamp(count - index * 40, 0, 40);
            return layer with { Name = new string('\u754c', chars) + new string('A', 40 - chars) };
        }).ToArray() };
        var bytes = LegacyBytes(design); Assert.InRange(bytes.Length, WidgetScenes.MaximumBytes - 4, WidgetScenes.MaximumBytes);
        var legacy = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(legacy, WidgetDesignRules.JsonContext.WidgetScene).Length > WidgetScenes.MaximumBytes);
        Directory.CreateDirectory(Path.Combine(Private, "renders"));
        await File.WriteAllBytesAsync(Path.Combine(Private, "catalog.json"), JsonSerializer.SerializeToUtf8Bytes(new WidgetLibraryCatalog(2, [design], [new(design.Size, 2, design.Id)]), WidgetDesignRules.JsonContext.WidgetLibraryCatalog));
        await File.WriteAllBytesAsync(Path.Combine(Private, "renders", design.RenderKey + ".png"), Png);
        await File.WriteAllBytesAsync(Path.Combine(Private, "renders", design.RenderKey + ".scene.json"), bytes);
        await new WidgetDesignPublisher(Repository(), () => Shared).PublishAsync();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", design.RenderKey + ".scene.json")));
        var before = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json"));
        await Assert.ThrowsAsync<IOException>(() => Repository().SaveAsync(design, Png, scene: Export(design)));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
    }

    [Fact]
    public async Task StaticAndFullyHiddenDesignsKeepTheirSavedImageUntilExplicitSave()
    {
        var repository = Repository(); var key = await repository.ImportPhotoAsync(Png);
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", key, 1, 1, new());
        var draft = WidgetDesignRules.New() with { Layers = [photo, WidgetClocks.New() with { Visible = false }, photo with { Id = Guid.NewGuid(), BlendMode = WidgetBlendMode.Multiply }] };
        var saved = await repository.SaveAsync(draft, Png);
        await repository.AssignSlotAsync(saved.Size, 1, saved.Id);
        var before = PrivateFiles(); await new WidgetDesignPublisher(repository, () => Shared).PublishAsync();
        foreach (var (path, bytes) in before) Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(Private, path)));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".png")));
        Assert.Null(repository.ScenePath(saved.RenderKey)); Assert.Single(WidgetScenes.Steps(saved));
        var empty = saved with { Layers = saved.Layers.Select(layer => layer with { Visible = false }).ToArray() };
        var updated = await repository.SaveAsync(empty, Png); Assert.Empty(WidgetScenes.Steps(updated));
        Assert.Null(repository.ScenePath(updated.RenderKey)); Assert.Equal(saved.Id, Assert.Single((await repository.ReadLibraryAsync()).Slots).DesignId);
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
