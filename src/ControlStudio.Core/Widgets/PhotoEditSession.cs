using System.IO.Compression;

namespace ControlStudio.Core.Widgets;

/// <summary>One bounded history for colour repair, transparency and object selections.</summary>
public sealed class PhotoEditSession
{
    private sealed record State(byte[] Image, byte[] Mask, byte[] Selection);
    private const int HistoryBytes = 32 * 1024 * 1024;
    private readonly List<State> undo = [], redo = [];
    private readonly byte[] original;
    private readonly byte[] initialPixels, initialMask;
    private State? stroke;
    private PhotoCutoutMask? brush;
    public int Width { get; }
    public int Height { get; }
    // Image buffers are immutable once installed, and shared between snapshots.
    public byte[] Pixels { get; private set; }
    public PhotoCutoutMask Mask { get; private set; }
    public PhotoCutoutMask Selection { get; private set; }
    public bool IsPainting => stroke is not null;
    public bool HasSelection => Selection.Pixels.Any(x => x != 0);
    public bool HasRepairs => !Pixels.AsSpan().SequenceEqual(original);
    public bool HasChanges => !Pixels.AsSpan().SequenceEqual(initialPixels) || !Mask.Pixels.AsSpan().SequenceEqual(initialMask);
    public bool CanUndo => !IsPainting && undo.Count > 0;
    public bool CanRedo => !IsPainting && redo.Count > 0;
    public PhotoEditSession(int width, int height, byte[] original, byte[]? mask = null, byte[]? working = null)
    {
        Mask = new(width, height, mask, recordHistory: false);
        if (original.Length != width * height * 4 || working is not null && working.Length != original.Length)
            throw new IOException("The editable photo dimensions are invalid.");
        Width = width; Height = height; this.original = original.ToArray(); Pixels = (working ?? original).ToArray();
        initialPixels = Pixels; initialMask = Mask.Pixels.ToArray();
        Selection = new(width, height, new byte[width * height], recordHistory: false);
    }
    public void BeginStroke(bool selecting)
    {
        if (IsPainting) return;
        stroke = Capture(); brush = selecting ? Selection : Mask; brush.BeginStroke();
    }
    public void Paint(double x, double y, double radius, double softness, bool restore)
        => brush?.Paint(x, y, radius, softness, restore);
    public void EndStroke(bool cancel = false)
    {
        if (stroke is not { } before) return;
        brush!.EndStroke(cancel); stroke = null; brush = null;
        if (!cancel && !Matches(before)) Commit(before);
    }
    public void ReplaceMask(byte[] pixels)
    {
        if (IsPainting) throw new IOException("Finish the brush stroke first.");
        var before = Capture(); Mask.Replace(pixels); if (!Matches(before)) Commit(before);
    }
    public void ClearSelection()
    {
        if (IsPainting || !HasSelection) return;
        var before = Capture(); Selection.Replace(new byte[Width * Height]); Commit(before);
    }
    public void AcceptRepair(byte[] generated)
    {
        if (IsPainting || generated.Length != Pixels.Length || !HasSelection)
            throw new IOException("Select an object before removing it.");
        // The repair engine may only replace RGB, never invent opaque pixels.
        for (var p = 0; p < Width * Height; p++)
        {
            var at = p * 4;
            if (generated[at + 3] != Pixels[at + 3]
                || Selection.Pixels[p] == 0 && !generated.AsSpan(at, 4).SequenceEqual(Pixels.AsSpan(at, 4)))
                throw new IOException("The removal changed pixels outside the selection.");
        }
        var before = Capture(); Pixels = generated.ToArray(); Selection.Replace(new byte[Width * Height]); Commit(before);
    }
    public void Reset()
    {
        if (IsPainting) return;
        var before = Capture(); Pixels = original;
        Mask.Replace(Enumerable.Repeat((byte)255, Width * Height).ToArray()); Selection.Replace(new byte[Width * Height]);
        if (!Matches(before)) Commit(before);
    }
    public void Undo() { if (!CanUndo) return; var next = undo[^1]; undo.RemoveAt(undo.Count - 1); redo.Add(Capture()); Restore(next); Trim(); }
    public void Redo() { if (!CanRedo) return; var next = redo[^1]; redo.RemoveAt(redo.Count - 1); undo.Add(Capture()); Restore(next); Trim(); }
    private State Capture() => new(Pixels, Pack(Mask.Pixels), Pack(Selection.Pixels));
    private bool Matches(State state) => (ReferenceEquals(Pixels, state.Image) || Pixels.AsSpan().SequenceEqual(state.Image))
        && Mask.Pixels.AsSpan().SequenceEqual(Unpack(state.Mask)) && Selection.Pixels.AsSpan().SequenceEqual(Unpack(state.Selection));
    private void Restore(State state)
    {
        Pixels = state.Image; Mask = new(Width, Height, Unpack(state.Mask), false);
        Selection = new(Width, Height, Unpack(state.Selection), false);
    }
    private void Commit(State before) { redo.Clear(); undo.Add(before); Trim(); }
    private void Trim()
    {
        while (undo.Count > PhotoCutoutMask.MaximumSteps) undo.RemoveAt(0);
        while (redo.Count > PhotoCutoutMask.MaximumSteps) redo.RemoveAt(0);
        while (Bytes() > HistoryBytes && undo.Count + redo.Count > 1)
        { if (undo.Count > 0) undo.RemoveAt(0); else redo.RemoveAt(0); }
    }
    private long Bytes()
    {
        var states = undo.Concat(redo).ToArray();
        return states.Sum(x => (long)x.Mask.Length + x.Selection.Length)
            + states.Select(x => x.Image).Distinct<byte[]>(ReferenceEqualityComparer.Instance).Sum(x => (long)x.Length);
    }
    private static byte[] Pack(byte[] data)
    {
        using var stream = new MemoryStream();
        using (var zip = new DeflateStream(stream, CompressionLevel.Fastest, true)) zip.Write(data);
        return stream.ToArray();
    }
    private byte[] Unpack(byte[] data)
    { using var input = new MemoryStream(data); using var zip = new DeflateStream(input, CompressionMode.Decompress); var result = new byte[Width * Height]; zip.ReadExactly(result); return result; }
}
