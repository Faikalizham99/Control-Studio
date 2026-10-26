namespace ControlStudio.App.Rendering;

/// <summary>
/// Owns a bounded chunk of an image recipe. Native operations may return an existing
/// wrapper, including a borrowed raster; each owned wrapper is released only once.
/// A checkpoint must be an independent snapshot, never a borrowed input.
/// </summary>
internal sealed class ImageRenderBatch<T> : IDisposable where T : class, IDisposable
{
    private readonly HashSet<T> owned = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<T> borrowed = new(ReferenceEqualityComparer.Instance);
    private bool disposed;
    public T Borrow(T image)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        owned.Remove(image); borrowed.Add(image); return image;
    }
    public T Own(T image)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!borrowed.Contains(image)) owned.Add(image);
        return image;
    }
    public T Checkpoint(T snapshot)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (borrowed.Contains(snapshot)) throw new InvalidOperationException("An image checkpoint must be independent of cached inputs.");
        foreach (var image in owned) if (!ReferenceEquals(image, snapshot)) image.Dispose();
        owned.Clear(); borrowed.Clear(); owned.Add(snapshot); return snapshot;
    }
    public T Detach(T image)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!owned.Remove(image)) throw new InvalidOperationException("Only an owned image can leave its render batch.");
        return image;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var image in owned) image.Dispose();
        owned.Clear(); borrowed.Clear();
    }
}
