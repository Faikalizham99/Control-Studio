using System.Collections.Concurrent;
using System.Text.Json;
using ControlStudio.Core.Widgets;

namespace ControlStudio.Infrastructure;

/// <summary>Private source of truth. Immutable images are installed before an atomic catalog commit.</summary>
public sealed class WidgetDesignRepository
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string directory;
    private readonly SemaphoreSlim gate;
    public WidgetDesignRepository(string directory)
    {
        this.directory = Path.GetFullPath(directory); gate = Locks.GetOrAdd(this.directory, _ => new(1, 1));
    }
    private string CatalogPath => Path.Combine(directory, "catalog.json");
    public bool HasSavedLibrary => File.Exists(CatalogPath);
    public string? AssetPath(string key) => ImagePath("photos", key);
    public string? RenderPath(string key) => ImagePath("renders", key);
    public string? ThumbnailPath(string key)
    {
        if (!WidgetDesignRules.ValidKey(key)) return null;
        var path = Path.Combine(directory, "renders", key + ".thumb.png"); return File.Exists(path) ? path : null;
    }
    private string? ImagePath(string folder, string key)
    {
        if (!WidgetDesignRules.ValidKey(key)) return null;
        var path = Path.Combine(directory, folder, key + ".png"); return File.Exists(path) ? path : null;
    }
    public async Task<IReadOnlyList<WidgetDesign>> ReadAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try { return (await ReadCoreAsync(token)).Designs; }
        finally { gate.Release(); }
    }
    public async Task<WidgetLibraryCatalog> ReadLibraryAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try { return await ReadCoreAsync(token); }
        finally { gate.Release(); }
    }
    private async Task<WidgetLibraryCatalog> ReadCoreAsync(CancellationToken token)
    {
        if (!File.Exists(CatalogPath)) return new(2, [], []);
        if (new FileInfo(CatalogPath).Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The widget catalog is too large. Data was not changed.");
        try
        {
            var bytes = await File.ReadAllBytesAsync(CatalogPath, token);
            if (bytes.Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The widget catalog is too large.");
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("schemaVersion", out var schema)
                || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version)) throw new IOException("The widget library is invalid. Data was not changed.");
            if (version == 1)
            {
                var original = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetCatalog)
                    ?? throw new IOException("The widget catalog is empty. Data was not changed.");
                WidgetDesignRules.Validate(original);
                // Existing IDs, revisions, layers and images are retained. The first mutation upgrades atomically.
                return new(2, original.Designs, []);
            }
            if (version != 2) throw new IOException("This widget library requires a newer app. Data was not changed.");
            var library = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetLibraryCatalog)
                ?? throw new IOException("The widget library is empty. Data was not changed.");
            WidgetSlotRules.Validate(library); return library;
        }
        catch (JsonException ex) { throw new IOException("The widget catalog could not be read. Data was not changed.", ex); }
    }
    public async Task<string> ImportPhotoAsync(byte[] png, CancellationToken token = default)
    {
        ValidatePng(png);
        var key = Guid.NewGuid().ToString("N");
        await WriteImageAsync("photos", key, png, token); return key;
    }
    public async Task<WidgetDesign> SaveAsync(WidgetDesign draft, byte[] render, CancellationToken token = default, byte[]? thumbnail = null)
    {
        WidgetDesignRules.Validate(draft); ValidatePng(render);
        if (thumbnail is not null) ValidatePng(thumbnail);
        await gate.WaitAsync(token);
        try
        {
            var library = await ReadCoreAsync(token);
            var designs = library.Designs.ToList();
            var index = designs.FindIndex(x => x.Id == draft.Id);
            var oldAssets = index >= 0 ? designs[index].Layers.Select(x => x.AssetKey).ToArray() : [];
            if (index < 0 && draft.Revision != 0 || index >= 0 && designs[index].Revision != draft.Revision)
                throw new IOException("This widget changed in another editor. Reopen it before saving.");
            if (index < 0 && designs.Count >= WidgetDesignRules.MaximumDesigns) throw new IOException("You can save up to 50 widgets. Delete one before adding another.");
            if (designs.Any(x => x.Id != draft.Id && x.Size == draft.Size && string.Equals(x.Name.Trim(), draft.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new IOException($"A {WidgetDesignRules.SizeName(draft.Size)} widget already uses this name. Choose another name.");
            foreach (var layer in draft.Layers)
                if (AssetPath(layer.AssetKey) is null) throw new IOException("A photo is missing. Remove that layer or choose the photo again.");
            var saved = draft with { Name = draft.Name.Trim(), Layers = draft.Layers.ToArray(), RenderKey = Guid.NewGuid().ToString("N"), Revision = checked(draft.Revision + 1) };
            if (index < 0) designs.Add(saved); else designs[index] = saved;
            try
            {
                await WriteImageAsync("renders", saved.RenderKey, render, token);
                if (thumbnail is not null) await WriteImageAsync("renders", saved.RenderKey + ".thumb", thumbnail, token);
                var slots = library.Slots.Where(x => x.DesignId != saved.Id || x.Size == saved.Size).ToArray();
                await WriteCatalogAsync(new(2, designs, slots), token);
            }
            catch { TryRemove(RenderPath(saved.RenderKey)); TryRemove(ThumbnailPath(saved.RenderKey)); throw; }
            // Keep sources/renders referenced by other designs. Cleanup is best effort after commit.
            RemoveUnusedSources(oldAssets, designs);
            CollectUnreferenced(designs); return saved;
        }
        finally { gate.Release(); }
    }
    public async Task DeleteAsync(Guid id, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            var library = await ReadCoreAsync(token);
            var before = library.Designs;
            var removedAssets = before.Where(x => x.Id == id).SelectMany(x => x.Layers).Select(x => x.AssetKey).ToArray();
            var designs = before.Where(x => x.Id != id).ToArray();
            await WriteCatalogAsync(new(2, designs, library.Slots.Where(x => x.DesignId != id).ToArray()), token);
            RemoveUnusedSources(removedAssets, designs); CollectUnreferenced(designs);
        }
        finally { gate.Release(); }
    }
    public async Task AssignSlotAsync(WidgetSize size, int number, Guid? designId, CancellationToken token = default)
    {
        WidgetSlotRules.ValidateAddress(size, number);
        await gate.WaitAsync(token);
        try
        {
            var library = await ReadCoreAsync(token);
            if (designId is { } id && !library.Designs.Any(x => x.Id == id && x.Size == size))
                throw new IOException("This design is unavailable or has a different size. Choose a matching saved design.");
            var slots = library.Slots.Where(x => x.Size != size || x.Number != number).ToList();
            if (designId is { } selected) slots.Add(new(size, number, selected));
            await WriteCatalogAsync(library with { Slots = slots.OrderBy(x => x.Size).ThenBy(x => x.Number).ToArray() }, token);
        }
        finally { gate.Release(); }
    }
    public async Task RemoveDraftPhotosAsync(IEnumerable<string> keys)
    {
        await gate.WaitAsync();
        try
        {
            var referenced = (await ReadCoreAsync(default)).Designs.SelectMany(x => x.Layers).Select(x => x.AssetKey).ToHashSet();
            foreach (var key in keys.Where(x => !referenced.Contains(x))) TryRemove(AssetPath(key));
        }
        finally { gate.Release(); }
    }
    private void CollectUnreferenced(IEnumerable<WidgetDesign> designs)
    {
        // Draft photos may belong to a still-open editor. Only explicit draft cleanup removes sources.
        var renders = designs.Select(x => x.RenderKey).ToHashSet();
        var folder = Path.Combine(directory, "renders");
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder, "*.png"))
            {
                var key = Path.GetFileNameWithoutExtension(path); if (key.EndsWith(".thumb", StringComparison.Ordinal)) key = key[..^6];
                if (!renders.Contains(key)) TryRemove(path);
            }
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    private void RemoveUnusedSources(IEnumerable<string> candidates, IEnumerable<WidgetDesign> designs)
    {
        var referenced = designs.SelectMany(x => x.Layers).Select(x => x.AssetKey).ToHashSet();
        foreach (var key in candidates.Where(x => !referenced.Contains(x))) TryRemove(AssetPath(key));
    }
    private async Task WriteCatalogAsync(WidgetLibraryCatalog catalog, CancellationToken token)
    {
        WidgetSlotRules.Validate(catalog);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetLibraryCatalog);
        if (bytes.Length > WidgetDesignRules.MaximumCatalogBytes) throw new IOException("The widget catalog is too large.");
        await AtomicWriteAsync(CatalogPath, bytes, token);
    }
    private Task WriteImageAsync(string folder, string key, byte[] bytes, CancellationToken token) => AtomicWriteAsync(Path.Combine(directory, folder, key + ".png"), bytes, token);
    internal static async Task AtomicWriteAsync(string path, byte[] bytes, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, token); token.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally { TryRemove(temporary); }
    }
    public static void ValidatePng(ReadOnlySpan<byte> png)
    {
        if (png.Length < 24 || png.Length > WidgetDesignRules.MaximumImageBytes || !png[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new IOException("This photo could not be prepared for a widget.");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png[16..20]);
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png[20..24]);
        if (width is < 1 or > WidgetDesignRules.MaximumImageEdge || height is < 1 or > WidgetDesignRules.MaximumImageEdge)
            throw new IOException("This widget image exceeds the supported dimensions.");
    }
    private static void TryRemove(string? path)
    {
        if (path is null) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
