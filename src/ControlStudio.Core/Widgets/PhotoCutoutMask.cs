using System.IO.Compression;

namespace ControlStudio.Core.Widgets;

/// <summary>Image-space soft mask. A whole brush stroke is one bounded undo step.</summary>
public sealed class PhotoCutoutMask
{
    public const int MaximumSteps = 30;
    private const int HistoryBytes = 16 * 1024 * 1024;
    private readonly List<byte[]> undo = [], redo = [];
    private byte[]? stroke;
    private double? lastX, lastY;
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; private set; }
    public bool IsPainting => stroke is not null;
    public bool CanUndo => !IsPainting && undo.Count > 0;
    public bool CanRedo => !IsPainting && redo.Count > 0;
    public PhotoCutoutMask(int width, int height, byte[]? pixels = null)
    {
        if (width is < 1 or > WidgetDesignRules.MaximumImageEdge || height is < 1 or > WidgetDesignRules.MaximumImageEdge
            || pixels is not null && pixels.Length != width * height) throw new IOException("The photo mask is invalid.");
        Width = width; Height = height; Pixels = pixels?.ToArray() ?? Enumerable.Repeat((byte)255, width * height).ToArray();
    }
    public void BeginStroke() { if (IsPainting) return; stroke = Pack(Pixels); lastX = lastY = null; }
    public void Paint(double x, double y, double radius, double softness, bool restore)
    {
        if (!IsPainting || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(radius) || radius <= 0 || !double.IsFinite(softness)) return;
        radius = Math.Clamp(radius, .5, 256); softness = Math.Clamp(softness, 0, 1);
        if (lastX is { } lx && lastY is { } ly)
        {
            var distance = Math.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly));
            // Ignore pathological input rather than allowing unbounded interpolation.
            var steps = Math.Clamp((int)Math.Ceiling(distance / Math.Max(.5, radius / 4)), 1, 8192);
            for (var i = 1; i <= steps; i++) Stamp(lx + (x - lx) * i / steps, ly + (y - ly) * i / steps, radius, softness, restore);
        }
        else Stamp(x, y, radius, softness, restore);
        lastX = x; lastY = y;
    }
    private void Stamp(double x, double y, double radius, double softness, bool restore)
    {
        var left = (int)Math.Clamp(Math.Floor(x - radius), 0, Width - 1); var right = (int)Math.Clamp(Math.Ceiling(x + radius), 0, Width - 1);
        var top = (int)Math.Clamp(Math.Floor(y - radius), 0, Height - 1); var bottom = (int)Math.Clamp(Math.Ceiling(y + radius), 0, Height - 1);
        var inner = radius * (1 - softness);
        for (var py = top; py <= bottom; py++) for (var px = left; px <= right; px++)
        {
            var d = Math.Sqrt((px + .5 - x) * (px + .5 - x) + (py + .5 - y) * (py + .5 - y));
            if (d > radius) continue;
            var amount = d <= inner ? 1 : Math.Clamp((radius - d) / Math.Max(.001, radius - inner), 0, 1);
            var target = (byte)Math.Round(255 * (restore ? amount : 1 - amount)); var index = py * Width + px;
            Pixels[index] = restore ? Math.Max(Pixels[index], target) : Math.Min(Pixels[index], target);
        }
    }
    public void EndStroke(bool cancel = false)
    {
        if (stroke is not { } before) return;
        stroke = null; lastX = lastY = null;
        if (cancel) { Pixels = Unpack(before); return; }
        if (Pixels.AsSpan().SequenceEqual(Unpack(before))) return;
        redo.Clear(); Push(undo, before);
    }
    public void Replace(byte[] pixels)
    {
        if (IsPainting || pixels.Length != Pixels.Length) throw new IOException("The photo mask is invalid.");
        if (pixels.AsSpan().SequenceEqual(Pixels)) return;
        var before = Pack(Pixels); Pixels = pixels.ToArray(); redo.Clear(); Push(undo, before);
    }
    public void Undo() { if (!CanUndo) return; var next = Pop(undo); Push(redo, Pack(Pixels)); Pixels = next; }
    public void Redo() { if (!CanRedo) return; var next = Pop(redo); Push(undo, Pack(Pixels)); Pixels = next; }
    private void Push(List<byte[]> list, byte[] bytes)
    {
        list.Add(bytes);
        while (list.Count > MaximumSteps) list.RemoveAt(0);
        while (undo.Sum(x => (long)x.Length) + redo.Sum(x => (long)x.Length) > HistoryBytes)
        {
            var other = ReferenceEquals(list, undo) ? redo : undo;
            if (list.Count > 1) list.RemoveAt(0);
            else if (other.Count > 0) other.RemoveAt(0);
            else break;
        }
    }
    private byte[] Pop(List<byte[]> list) { var value = list[^1]; list.RemoveAt(list.Count - 1); return Unpack(value); }
    private static byte[] Pack(byte[] bytes)
    {
        using var result = new MemoryStream();
        using (var zip = new DeflateStream(result, CompressionLevel.Fastest, true)) zip.Write(bytes);
        return result.ToArray();
    }
    private byte[] Unpack(byte[] bytes)
    {
        using var input = new MemoryStream(bytes); using var zip = new DeflateStream(input, CompressionMode.Decompress);
        var result = new byte[Width * Height]; zip.ReadExactly(result); return result;
    }
}

public static class PhotoCutoutPixels
{
    /// <summary>Multiply premultiplied RGB and source alpha together; Restore never invents opaque source pixels.</summary>
    public static byte[] Apply(ReadOnlySpan<byte> rgba, ReadOnlySpan<byte> mask)
    {
        if (mask.Length is < 1 or > WidgetDesignRules.MaximumImageEdge * WidgetDesignRules.MaximumImageEdge || rgba.Length != mask.Length * 4)
            throw new IOException("The photo mask dimensions are invalid.");
        var output = new byte[rgba.Length];
        for (var p = 0; p < mask.Length; p++) for (var c = 0; c < 4; c++)
            output[p * 4 + c] = (byte)((rgba[p * 4 + c] * mask[p] + 127) / 255);
        return output;
    }
}
