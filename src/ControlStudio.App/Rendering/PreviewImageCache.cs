namespace ControlStudio.App.Rendering;

/// <summary>Byte-bounded admission cache. A large sequential scan cannot evict
/// every useful entry each frame; overflow images belong to that frame only.</summary>
internal sealed class PreviewImageCache<TKey, TValue>(long budget, int maximumEntries = int.MaxValue) : IDisposable
    where TKey : notnull where TValue : class, IDisposable
{
    private readonly Dictionary<TKey, (TValue Value, long Bytes)> entries = [];
    public long Bytes { get; private set; }
    public IEnumerable<TKey> Keys => entries.Keys;
    public bool TryGet(TKey key, out TValue? value)
    {
        if (entries.TryGetValue(key, out var item)) { value = item.Value; return true; }
        value = null; return false;
    }
    // Ownership transfers only on success. Replace/remove before allocating.
    public bool TryStore(TKey key, TValue value, long bytes)
    {
        if (bytes <= 0 || bytes > budget - Bytes || entries.Count >= maximumEntries || entries.ContainsKey(key)) return false;
        entries.Add(key, (value, bytes)); Bytes += bytes; return true;
    }
    public void Remove(TKey key)
    {
        if (!entries.Remove(key, out var item)) return;
        Bytes -= item.Bytes; item.Value.Dispose();
    }
    public void Dispose() { foreach (var key in entries.Keys.ToArray()) Remove(key); }
}
