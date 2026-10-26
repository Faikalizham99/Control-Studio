using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class WidgetClockSceneTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "WidgetClockScenes", Guid.NewGuid().ToString("N"));
    private string Private => Path.Combine(root, "private"); private string Shared => Path.Combine(root, "shared");
    private WidgetDesignRepository Repo() => new(Private);
    private static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZXsAAAAASUVORK5CYII=");
    private static WidgetSceneExport Export(WidgetDesign design) => new(WidgetScenes.Manifest(design), WidgetScenes.Manifest(design).Parts
        .Where(x => x.ImageIndex is not null).ToDictionary(x => x.ImageIndex!.Value, _ => Png));
    private async Task<WidgetDesign> Draft()
    {
        var key = await Repo().ImportPhotoAsync(Png); var photo = new WidgetLayer(Guid.NewGuid(), "Bottom", key, 1, 1, new());
        return WidgetDesignRules.New() with { Layers = [photo, WidgetClocks.New(), photo with { Id = Guid.NewGuid(), Name = "Top" }] };
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CustomTextReopensAndPublishesAsStaticGroupsWithNoSourceAssetsOrNewSceneSchema(bool mixed)
    {
        var repo = Repo(); var text = WidgetTexts.New() with { Text = new("Hello\n世界 🌸", WidgetClockFont.SfProRounded, "#12ABEF", .5, WidgetClockAlignment.Left, WidgetClockWeight.Light) };
        var top = text with { Id = Guid.NewGuid(), Name = "Top", Text = text.Text! with { Content = "Top" }, Locked = true };
        var layers = mixed ? new[] { text, WidgetClocks.New(), top, WidgetDates.New() } : new[] { text, top };
        var draft = WidgetDesignRules.New() with { Layers = layers };
        var saved = await repo.SaveAsync(draft, Png, scene: mixed ? Export(draft) : null); await repo.AssignSlotAsync(saved.Size, 3, saved.Id);
        var reopened = Assert.Single(await Repo().ReadAsync()); Assert.Equal(layers, reopened.Layers);
        await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        var published = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.Equal(layers, Assert.Single(published.Designs).Layers); Assert.False(Directory.Exists(Path.Combine(Private, "photos")));
        Assert.False(Directory.Exists(Path.Combine(Shared, "photos"))); Assert.Equal(saved.Id, Assert.Single((await repo.ReadLibraryAsync()).Slots).DesignId);
        if (mixed)
        {
            var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
            Assert.Equal(2, scene.SchemaVersion); Assert.Equal(new int?[] { 0, null, 2, null }, scene.Parts.Select(x => x.ImageIndex));
            Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".part-0.png"))); WidgetScenes.Validate(scene, saved);
        }
        else Assert.Null(repo.ScenePath(saved.RenderKey));
        var broken = saved with { Layers = [text with { Text = text.Text! with { Content = new string('x', 501) } }] };
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(broken, Png)); Assert.Equal(layers, Assert.Single(await repo.ReadAsync()).Layers);
    }
    [Fact]
    public async Task NudgedPhotoClockAndDateFramesReopenAndPublishAtTheirSavedPositions()
    {
        var repo = Repo(); var key = await repo.ImportPhotoAsync(Png);
        var dimensions = new WidgetCanvasSize(158, 158);
        var layers = new[] { new WidgetLayer(Guid.NewGuid(), "Photo", key, 1, 1, new(0, 0, .3, .4)), WidgetClocks.New(), WidgetDates.New() }
            .Select(layer => WidgetLayerEdits.Nudge(WidgetLayerEdits.Nudge(layer, WidgetMoveDirection.Left, WidgetSize.Small, dimensions),
                WidgetMoveDirection.Down, WidgetSize.Small, dimensions)).ToArray();
        var draft = WidgetDesignRules.New() with { CanvasDimensions = dimensions, Layers = layers };
        var saved = await repo.SaveAsync(draft, Png, scene: Export(draft)); await repo.AssignSlotAsync(saved.Size, 2, saved.Id);
        Assert.Equal(layers, Assert.Single(await Repo().ReadAsync()).Layers);
        await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        var published = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.Equal(layers, Assert.Single(published.Designs).Layers);
        var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.Equal(layers[1].Frame, scene.Parts.Single(x => x.Clock is not null).Clock!.Frame);
        Assert.Equal(layers[2].Frame, scene.Parts.Single(x => x.Date is not null).Date!.Frame);
        Assert.Equal(saved.Id, Assert.Single((await Repo().ReadLibraryAsync()).Slots).DesignId);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DuplicatedLiveLayersPublishDistinctIDsAndKeepIndependentOptions(bool dateLayer)
    {
        var layer = dateLayer ? WidgetDates.New() with { Date = new(ShowWeekday: true, WeekdayStyle: WidgetWeekdayStyle.Full) } : WidgetClocks.New();
        var duplicate = WidgetLayerEdits.Duplicate(WidgetDesignRules.New() with { Layers = [layer] }, layer.Id);
        var repo = Repo(); var saved = await repo.SaveAsync(duplicate.Design, Png, scene: Export(duplicate.Design));
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var ids = scene.Parts.Select(part => (part.Clock ?? part.Date)!.Id).ToArray(); Assert.Equal(new[] { layer.Id, duplicate.Copy.Id }, ids); Assert.Equal(2, ids.Distinct().Count());
        var edited = dateLayer ? duplicate.Copy with { Date = duplicate.Copy.Date! with { ShowWeekday = false } }
            : duplicate.Copy with { Clock = duplicate.Copy.Clock! with { ShowSeconds = false } };
        var changed = saved with { Layers = [layer, edited] }; await repo.SaveAsync(changed, Png, scene: Export(changed));
        Assert.Equal(layer, Assert.Single(await Repo().ReadAsync()).Layers[0]); Assert.Equal(edited, Assert.Single(await Repo().ReadAsync()).Layers[1]);
    }
    [Fact]
    public async Task WeekdayCanBeDisabledAndReenabledWithoutLosingItsStyleOrSlot()
    {
        var repo = Repo(); var date = WidgetDates.New() with { Date = new(Format: WidgetDateFormat.Numeric, ShowWeekday: true, WeekdayStyle: WidgetWeekdayStyle.Full) };
        var draft = WidgetDesignRules.New() with { Layers = [date] }; var saved = await repo.SaveAsync(draft, Png, scene: Export(draft));
        await repo.AssignSlotAsync(saved.Size, 4, saved.Id);
        var off = saved with { Layers = [saved.Layers[0] with { Date = date.Date! with { ShowWeekday = false } }] };
        var updated = await repo.SaveAsync(off, Png, scene: Export(off)); await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        var reopened = Assert.Single(await Repo().ReadAsync()); Assert.False(reopened.Layers[0].Date!.ShowWeekday);
        Assert.Equal(WidgetWeekdayStyle.Full, reopened.Layers[0].Date!.WeekdayStyle);
        var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", updated.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.False(scene.Parts[0].Date!.Date!.ShowWeekday); Assert.Equal(WidgetWeekdayStyle.Full, scene.Parts[0].Date!.Date!.WeekdayStyle);
        var on = reopened with { Layers = [reopened.Layers[0] with { Date = reopened.Layers[0].Date! with { ShowWeekday = true } }] };
        await repo.SaveAsync(on, Png, scene: Export(on)); Assert.True(Assert.Single(await Repo().ReadAsync()).Layers[0].Date!.ShowWeekday);
        Assert.Equal(saved.Id, Assert.Single((await Repo().ReadLibraryAsync()).Slots).DesignId);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DatesReopenAndPublishAsLiveTextWithoutSourceAssets(bool mixed)
    {
        var repo = Repo(); var date = WidgetDates.New() with { Date = new(WidgetDateFormat.ShortDate, WidgetClockFont.SfProRounded, "#12ABEF", .6, WidgetClockAlignment.Left, WidgetClockWeight.Light, true, WidgetWeekdayStyle.Full), Locked = true };
        var draft = WidgetDesignRules.New() with { Layers = mixed ? [WidgetClocks.New(), date] : [date] };
        var saved = await repo.SaveAsync(draft, Png, scene: Export(draft)); await repo.AssignSlotAsync(saved.Size, 2, saved.Id);
        var reopened = Assert.Single(await Repo().ReadAsync()); Assert.Equal(date, reopened.Layers.Last()); Assert.Null(reopened.Layers.Last().Clock);
        await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.Equal(2, scene.SchemaVersion); Assert.Equal(date, scene.Parts.Last().Date); WidgetScenes.Validate(scene, saved);
        Assert.False(Directory.Exists(Path.Combine(Private, "photos"))); Assert.False(Directory.Exists(Path.Combine(Shared, "photos")));
        var hiddenDraft = saved with { Layers = saved.Layers.Select(layer => layer with { Visible = false }).ToArray() };
        var hidden = await repo.SaveAsync(hiddenDraft, Png); Assert.Null(repo.ScenePath(hidden.RenderKey));
        var visibleDraft = hidden with { Layers = hidden.Layers.Select(layer => layer with { Visible = true }).ToArray() };
        await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(visibleDraft, Png));
        var restored = await repo.SaveAsync(visibleDraft, Png, scene: Export(visibleDraft)); Assert.NotNull(repo.ScenePath(restored.RenderKey));
        Assert.Equal(saved.Id, Assert.Single((await Repo().ReadLibraryAsync()).Slots).DesignId);
    }
    [Theory]
    [InlineData(WidgetClockFont.Rounded)] [InlineData(WidgetClockFont.RocknRollOne)]
    [InlineData(WidgetClockFont.DelaGothicOne)] [InlineData(WidgetClockFont.KaiseiDecol)]
    [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Light)] [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Regular)]
    [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Medium)] [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Semibold)]
    [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Bold)] [InlineData(WidgetClockFont.SfProRounded, WidgetClockWeight.Heavy)]
    [InlineData(WidgetClockFont.System, WidgetClockWeight.Light)] [InlineData(WidgetClockFont.Monospaced, WidgetClockWeight.Regular)]
    [InlineData(WidgetClockFont.Serif, WidgetClockWeight.Regular)]
    public async Task MixedSceneSavesReopensPublishesWithoutPublishingSourcePhotosAndKeepsCachedScenes(WidgetClockFont font, WidgetClockWeight? weight = null)
    {
        var repo = Repo(); var original = await Draft();
        var draft = original with { Layers = original.Layers.Select(x => x.Clock is null ? x : x with { Clock = x.Clock with { Font = font, Weight = weight } }).ToArray() };
        var saved = await repo.SaveAsync(draft, Png, scene: Export(draft));
        await repo.AssignSlotAsync(saved.Size, 3, saved.Id);
        Assert.Equal(saved.Layers, Assert.Single(await Repo().ReadAsync()).Layers);
        var scene = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(repo.ScenePath(saved.RenderKey)!), WidgetDesignRules.JsonContext.WidgetScene)!;
        WidgetScenes.Validate(scene, saved); Assert.NotNull(repo.SceneImagePath(saved.RenderKey, 0)); Assert.NotNull(repo.SceneImagePath(saved.RenderKey, 2));
        await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json"))); Assert.False(Directory.Exists(Path.Combine(Shared, "photos")));
        var updatedDraft = saved with { Layers = saved.Layers.Select(x => x.Kind == WidgetLayerKind.Clock ? x with { Locked = true, Clock = x.Clock! with { ShowSeconds = false, TwentyFourHour = false, ShowAmPm = false } } : x).ToArray() };
        var updated = await repo.SaveAsync(updatedDraft, Png, scene: Export(updatedDraft));
        Assert.Null(repo.ScenePath(saved.RenderKey)); Assert.NotNull(repo.ScenePath(updated.RenderKey)); Assert.Null(repo.SceneImagePath(saved.RenderKey, 0));
        await new WidgetDesignPublisher(Repo(), () => Shared).PublishAsync();
        Assert.False(Assert.Single(await Repo().ReadAsync()).Layers[1].Clock!.ShowAmPm);
        var published = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(Path.Combine(Shared, "renders", updated.RenderKey + ".scene.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.False(published.Parts.Single(x => x.Clock is not null).Clock!.Clock!.ShowAmPm);
        Assert.Equal(font, published.Parts.Single(x => x.Clock is not null).Clock!.Clock!.Font);
        Assert.Equal(weight, published.Parts.Single(x => x.Clock is not null).Clock!.Clock!.Weight);
        Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".scene.json"))); // Cached timelines have a seven-day grace period.
        Assert.Equal(saved.Id, Assert.Single((await Repo().ReadLibraryAsync()).Slots).DesignId);
        Assert.Equal(Png, await File.ReadAllBytesAsync(repo.AssetPath(saved.Layers[0].AssetKey)!));
    }
    [Fact]
    public async Task ClockOnlyNeedsNoImportedPhotoAndHiddenClocksNeedNoLiveArtifacts()
    {
        var repo = Repo(); var draft = WidgetDesignRules.New() with { Layers = [WidgetClocks.New()] };
        var saved = await repo.SaveAsync(draft, Png, scene: Export(draft)); Assert.NotNull(repo.ScenePath(saved.RenderKey));
        Assert.False(Directory.Exists(Path.Combine(Private, "photos"))); await new WidgetDesignPublisher(repo, () => Shared).PublishAsync();
        var hidden = await repo.SaveAsync(saved with { Layers = [saved.Layers[0] with { Visible = false }] }, Png);
        Assert.Null(repo.ScenePath(hidden.RenderKey)); Assert.Null(repo.ScenePath(saved.RenderKey));
        await new WidgetDesignPublisher(repo, () => Shared).PublishAsync(); Assert.Single(await repo.ReadAsync());
    }
    [Fact]
    public async Task InvalidOrMissingSceneCannotReplaceTheSavedLibrary()
    {
        var repo = Repo(); var draft = await Draft(); var export = Export(draft); var saved = await repo.SaveAsync(draft, Png, scene: export);
        var before = await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")); var existing = Directory.GetFiles(Path.Combine(Private, "renders")).Order().ToArray();
        foreach (var scene in new WidgetSceneExport?[] { null, export with { Images = new Dictionary<int, byte[]>() },
            export with { Scene = export.Scene with { Width = 0 } }, export with { Scene = export.Scene with { Parts = export.Scene.Parts.Reverse().ToArray() } } })
        {
            await Assert.ThrowsAsync<IOException>(() => repo.SaveAsync(saved, Png, scene: scene));
            Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Private, "catalog.json")));
            Assert.Equal(existing, Directory.GetFiles(Path.Combine(Private, "renders")).Order().ToArray());
        }
    }
    [Fact]
    public async Task BrokenPhotoGroupDoesNotReplaceAPublishedCatalog()
    {
        var repo = Repo(); var draft = await Draft(); var saved = await repo.SaveAsync(draft, Png, scene: Export(draft));
        var publisher = new WidgetDesignPublisher(repo, () => Shared); await publisher.PublishAsync();
        var before = await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json"));
        File.Delete(repo.SceneImagePath(saved.RenderKey, 2)!); await Assert.ThrowsAsync<IOException>(() => publisher.PublishAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(Shared, "catalog.json")));
        Assert.True(File.Exists(Path.Combine(Shared, "renders", saved.RenderKey + ".part-2.png")));
    }
    [Fact]
    public void SceneDecodedPixelBudgetAndPhotoProportionsAreBounded()
    {
        var photos = Enumerable.Range(0, 4).Select(i => new WidgetLayer(Guid.NewGuid(), "Photo " + i, Guid.NewGuid().ToString("N"), 1, 1, new())).ToArray();
        var layers = photos.SelectMany((photo, index) => new[] { photo, WidgetClocks.New("Clock " + index) }).ToArray(); var design = WidgetDesignRules.New() with { Layers = layers };
        var export = Export(design); var large = Png;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(large.AsSpan(16, 4), 1024); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(large.AsSpan(20, 4), 1024);
        Assert.Throws<IOException>(() => WidgetDesignRepository.ValidateScene(export with { Images = export.Images.Keys.ToDictionary(x => x, _ => large) }, design));
        var wrongShape = Png; System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(wrongShape.AsSpan(20, 4), 2);
        Assert.Throws<IOException>(() => WidgetDesignRepository.ValidateScene(export with { Images = export.Images.Keys.ToDictionary(x => x, _ => wrongShape) }, design));
        var padded = new byte[2_000_000]; Png.CopyTo(padded, 0);
        Assert.Throws<IOException>(() => WidgetDesignRepository.ValidateScene(export with { Images = export.Images.Keys.ToDictionary(x => x, _ => padded) }, design));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
