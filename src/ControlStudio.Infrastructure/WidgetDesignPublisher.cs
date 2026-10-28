using System.Buffers;
using System.Text.Json;
using ControlStudio.Core.Widgets;

namespace ControlStudio.Infrastructure;

/// <summary>Installs immutable renders first, then atomically replaces the shared manifest.</summary>
public sealed class WidgetDesignPublisher(WidgetDesignRepository repository, Func<string> sharedDirectory)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    // No image bytes are retained. Entries only describe artifacts validated by
    // this publisher, and are replaced after a complete successful publication.
    private Dictionary<Guid, ValidatedDesign> validated = [];
    public Task PublishAsync(CancellationToken token = default) => PublishCoreAsync(force: true, token);
    public Task PublishIfChangedAsync(CancellationToken token = default) => PublishCoreAsync(force: false, token);
    private async Task PublishCoreAsync(bool force, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            var directory = sharedDirectory();
            var manifest = Path.Combine(directory, "catalog.json");
            byte[]? previousCatalog = null, previousSlots = null;
            if (File.Exists(manifest))
            {
                if (new FileInfo(manifest).Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The shared widget catalog is too large.");
                previousCatalog = await File.ReadAllBytesAsync(manifest, token);
                if (previousCatalog.Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The shared widget catalog is too large.");
                var old = JsonSerializer.Deserialize(previousCatalog, WidgetDesignRules.JsonContext.WidgetCatalog)
                    ?? throw new IOException("The shared widget catalog is invalid.");
                WidgetDesignRules.Validate(old);
            }
            var slotManifest = Path.Combine(directory, "slots.json");
            if (File.Exists(slotManifest))
            {
                if (new FileInfo(slotManifest).Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
                var bytes = await File.ReadAllBytesAsync(slotManifest, token);
                previousSlots = bytes;
                if (bytes.Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
                var old = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetSlotCatalog)
                    ?? throw new IOException("The shared slot list is invalid.");
                WidgetSlotRules.Validate(old);
            }
            var library = await repository.ReadLibraryAsync(token);
            // Old layouts remain private and recoverable; never advertise them to
            // the widget extension or reuse their assignments for iPhone XL.
            var designs = library.Designs.Where(x => WidgetDesignRules.IsSupported(x.Size)).ToArray();
            var nextValidated = new Dictionary<Guid, ValidatedDesign>();
            foreach (var design in designs)
            {
                token.ThrowIfCancellationRequested();
                if (!force && validated.TryGetValue(design.Id, out var cached) && cached.Matches(design, directory))
                {
                    nextValidated.Add(design.Id, cached); continue;
                }
                var stamps = new List<FileStamp>();
                var path = repository.RenderPath(design.RenderKey) ?? throw new IOException("A widget preview is missing. Open its editor and save it again.");
                stamps.Add(FileStamp.Read(path));
                if (new FileInfo(path).Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("A widget preview is too large.");
                var bytes = await File.ReadAllBytesAsync(path, token); WidgetDesignRepository.ValidatePng(bytes);
                var target = Path.Combine(directory, "renders", design.RenderKey + ".png");
                await InstallAsync(target, bytes, token); stamps.Add(FileStamp.Read(target));
                if (WidgetScenes.IsLive(design))
                {
                    var scenePath = repository.ScenePath(design.RenderKey) ?? throw new IOException("A live widget scene is missing. Open its editor and save again.");
                    stamps.Add(FileStamp.Read(scenePath));
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
                            stamps.Add(FileStamp.Read(imagePath));
                            if (new FileInfo(imagePath).Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("A live widget photo group is too large.");
                            images[index] = await File.ReadAllBytesAsync(imagePath, token);
                        }
                    // Publish the bounded saved bytes unchanged. Reserializing an
                    // old scene can add default fields and falsely exceed its budget.
                    WidgetDesignRepository.ValidateScene(new(scene, images), design, sceneBytes.Length);
                    foreach (var (index, image) in images)
                    {
                        var imageTarget = Path.Combine(directory, "renders", design.RenderKey + $".part-{index}.png");
                        await InstallAsync(imageTarget, image, token); stamps.Add(FileStamp.Read(imageTarget));
                    }
                    var sceneTarget = Path.Combine(directory, "renders", design.RenderKey + ".scene.json");
                    await InstallAsync(sceneTarget, sceneBytes, token); stamps.Add(FileStamp.Read(sceneTarget));
                }
                if (stamps.Any(x => !x.Unchanged())) throw new IOException("A widget image changed while publishing. Try Refresh again.");
                nextValidated.Add(design.Id, new(design, directory, stamps.ToArray()));
            }
            var catalog = new WidgetCatalog(1, designs); WidgetDesignRules.Validate(catalog);
            var catalogBytes = JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetCatalog);
            if (catalogBytes.Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The shared widget catalog is too large.");
            if (previousCatalog is null || !catalogBytes.AsSpan().SequenceEqual(previousCatalog))
                await WidgetDesignRepository.AtomicWriteAsync(manifest, catalogBytes, token);
            var slots = new WidgetSlotCatalog(1, library.Slots.Where(x => WidgetDesignRules.IsSupported(x.Size)).ToArray()); WidgetSlotRules.Validate(slots);
            var slotBytes = JsonSerializer.SerializeToUtf8Bytes(slots, WidgetDesignRules.JsonContext.WidgetSlotCatalog);
            if (slotBytes.Length > WidgetSlotRules.MaximumCatalogBytes) throw new IOException("The shared slot list is too large.");
            // Catalog first: a new assignment cannot be published before its design and image exist.
            if (previousSlots is null || !slotBytes.AsSpan().SequenceEqual(previousSlots))
                await WidgetDesignRepository.AtomicWriteAsync(slotManifest, slotBytes, token);
            token.ThrowIfCancellationRequested();
            validated = nextValidated;
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
        catch { validated.Clear(); throw; }
        finally { gate.Release(); }
    }
    private static async Task InstallAsync(string path, byte[] bytes, CancellationToken token)
    {
        if (File.Exists(path) && new FileInfo(path).Length == bytes.Length && await SameBytesAsync(path, bytes, token)) return;
        await WidgetDesignRepository.AtomicWriteAsync(path, bytes, token);
    }
    private static async Task<bool> SameBytesAsync(string path, byte[] expected, CancellationToken token)
    {
        // Full refresh also detects damaged installed artifacts, without allocating
        // a second multi-megabyte copy of every render.
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var offset = 0;
            while (offset < expected.Length)
            {
                var read = await file.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, expected.Length - offset)), token);
                if (read == 0 || !buffer.AsSpan(0, read).SequenceEqual(expected.AsSpan(offset, read))) return false;
                offset += read;
            }
            return await file.ReadAsync(buffer.AsMemory(0, 1), token) == 0;
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
    private sealed record ValidatedDesign(WidgetDesign Design, string Directory, FileStamp[] Stamps)
    {
        public bool Matches(WidgetDesign design, string directory) => Directory == directory
            && Design == (design with { Layers = Design.Layers }) && Design.Layers.SequenceEqual(design.Layers)
            && Stamps.All(x => x.Unchanged());
    }
    private sealed record FileStamp(string Path, long Length, DateTime Written, DateTime Created)
    {
        public static FileStamp Read(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new IOException("A widget image is missing. Open its editor and save again.");
            return new(path, file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc);
        }
        public bool Unchanged()
        {
            try { return this == Read(Path); }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
    private static void RemoveRetiredTestData(string directory)
    {
        // This exact direct child belongs only to the removed loading experiment; never sweep design/image folders.
        var retired = Path.Combine(directory, "diagnostics");
        try { if (Directory.Exists(retired)) Directory.Delete(retired, recursive: true); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
