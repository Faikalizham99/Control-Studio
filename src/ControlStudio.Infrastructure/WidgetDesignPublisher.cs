using System.Text.Json;
using ControlStudio.Core.Widgets;

namespace ControlStudio.Infrastructure;

/// <summary>Installs immutable renders first, then atomically replaces the shared manifest.</summary>
public sealed class WidgetDesignPublisher(WidgetDesignRepository repository, Func<string> sharedDirectory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task PublishAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            var directory = sharedDirectory();
            var manifest = Path.Combine(directory, "catalog.json");
            if (File.Exists(manifest))
            {
                if (new FileInfo(manifest).Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The shared widget catalog is too large.");
                var old = JsonSerializer.Deserialize(await File.ReadAllBytesAsync(manifest, token), WidgetDesignRules.JsonContext.WidgetCatalog)
                    ?? throw new IOException("The shared widget catalog is invalid.");
                WidgetDesignRules.Validate(old);
            }
            var slotManifest = Path.Combine(directory, "slots.json");
            if (File.Exists(slotManifest))
            {
                if (new FileInfo(slotManifest).Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
                var bytes = await File.ReadAllBytesAsync(slotManifest, token);
                if (bytes.Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
                var old = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetSlotCatalog)
                    ?? throw new IOException("The shared slot list is invalid.");
                WidgetSlotRules.Validate(old);
            }
            var library = await repository.ReadLibraryAsync(token);
            // Old layouts remain private and recoverable; never advertise them to
            // the widget extension or reuse their assignments for iPhone XL.
            var designs = library.Designs.Where(x => WidgetDesignRules.IsSupported(x.Size)).ToArray();
            foreach (var design in designs)
            {
                var path = repository.RenderPath(design.RenderKey) ?? throw new IOException("A widget preview is missing. Open its editor and save it again.");
                if (new FileInfo(path).Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("A widget preview is too large.");
                var bytes = await File.ReadAllBytesAsync(path, token); WidgetDesignRepository.ValidatePng(bytes);
                var target = Path.Combine(directory, "renders", design.RenderKey + ".png");
                if (!File.Exists(target)) await WidgetDesignRepository.AtomicWriteAsync(target, bytes, token);
                if (WidgetScenes.IsLive(design))
                {
                    var scenePath = repository.ScenePath(design.RenderKey) ?? throw new IOException("A live widget scene is missing. Open its editor and save again.");
                    if (new FileInfo(scenePath).Length > WidgetScenes.MaximumBytes) throw new IOException("The live widget scene is too large.");
                    var sceneBytes = await File.ReadAllBytesAsync(scenePath, token);
                    if (sceneBytes.Length > WidgetScenes.MaximumBytes) throw new IOException("The live widget scene is too large.");
                    var scene = JsonSerializer.Deserialize(sceneBytes, WidgetDesignRules.JsonContext.WidgetScene) ?? throw new IOException("The live widget scene is invalid.");
                    WidgetScenes.Validate(scene, design);
                    var images = new Dictionary<int, byte[]>();
                    foreach (var part in scene.Parts)
                        if (part.ImageIndex is { } index)
                        {
                            var imagePath = repository.SceneImagePath(design.RenderKey, index) ?? throw new IOException("A live widget photo group is missing.");
                            if (new FileInfo(imagePath).Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("A live widget photo group is too large.");
                            images[index] = await File.ReadAllBytesAsync(imagePath, token);
                        }
                    // Publish the bounded saved bytes unchanged. Reserializing an
                    // old scene can add default fields and falsely exceed its budget.
                    WidgetDesignRepository.ValidateScene(new(scene, images), design, sceneBytes.Length);
                    foreach (var (index, image) in images)
                    {
                        var imageTarget = Path.Combine(directory, "renders", design.RenderKey + $".part-{index}.png");
                        if (!File.Exists(imageTarget)) await WidgetDesignRepository.AtomicWriteAsync(imageTarget, image, token);
                    }
                    var sceneTarget = Path.Combine(directory, "renders", design.RenderKey + ".scene.json");
                    if (!File.Exists(sceneTarget)) await WidgetDesignRepository.AtomicWriteAsync(sceneTarget, sceneBytes, token);
                }
            }
            var catalog = new WidgetCatalog(1, designs); WidgetDesignRules.Validate(catalog);
            await WidgetDesignRepository.AtomicWriteAsync(manifest, JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetCatalog), token);
            var slots = new WidgetSlotCatalog(1, library.Slots.Where(x => WidgetDesignRules.IsSupported(x.Size)).ToArray()); WidgetSlotRules.Validate(slots);
            var slotBytes = JsonSerializer.SerializeToUtf8Bytes(slots, WidgetDesignRules.JsonContext.WidgetSlotCatalog);
            if (slotBytes.Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
            // Catalog first: a new assignment cannot be published before its design and image exist.
            await WidgetDesignRepository.AtomicWriteAsync(slotManifest, slotBytes, token);
            RemoveRetiredTestData(directory);
            // Cached timelines may still refer to the previous generation. Keep recent immutable renders.
            var current = designs.Select(x => x.RenderKey).ToHashSet();
            var folder = Path.Combine(directory, "renders");
            try
            {
                if (Directory.Exists(folder))
                    foreach (var path in Directory.EnumerateFiles(folder))
                        if (!current.Contains(Path.GetFileName(path).Split('.')[0]) && File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-7))
                            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        finally { gate.Release(); }
    }
    private static void RemoveRetiredTestData(string directory)
    {
        // This exact direct child belongs only to the removed loading experiment; never sweep design/image folders.
        var retired = Path.Combine(directory, "diagnostics");
        try { if (Directory.Exists(retired)) Directory.Delete(retired, recursive: true); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
