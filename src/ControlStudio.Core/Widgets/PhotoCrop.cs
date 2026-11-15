using System.Text.Json.Serialization;

namespace ControlStudio.Core.Widgets;

public enum PhotoCropAspect { Free, Original, Square, FourThree, SixteenNine }
public enum PhotoCropHandle { Move, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }
public sealed record PhotoCropRect(int X, int Y, int Width, int Height)
{
    public static PhotoCropRect Full(int width, int height) => new(0, 0, width, height);
    public void Validate(int width, int height)
    {
        if (width is < 1 or > WidgetDesignRules.MaximumImageEdge || height is < 1 or > WidgetDesignRules.MaximumImageEdge
            || X < 0 || Y < 0 || Width < 1 || Height < 1 || (long)X + Width > width || (long)Y + Height > height)
            throw new IOException("The saved photo crop is invalid. Your photo is unchanged.");
    }
    public byte[] CopyRgba(byte[] rgba, int width, int height)
    {
        Validate(width, height);
        if (rgba.Length != width * height * 4) throw new IOException("The crop pixels are invalid.");
        var output = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++) Buffer.BlockCopy(rgba, ((Y + y) * width + X) * 4, output, y * Width * 4, Width * 4);
        return output;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 4)]
[JsonSerializable(typeof(PhotoCropRect))]
public partial class PhotoCropJsonContext : JsonSerializerContext;

/// <summary>Pixel-aligned crop edits. View zoom never modifies the crop or history.</summary>
public sealed class PhotoCropSession
{
    private sealed record State(PhotoCropRect Rect, PhotoCropAspect Aspect);
    private readonly List<State> undo = [], redo = [];
    private State? origin;
    private readonly PhotoCropRect initial;
    private State current;
    public int Width { get; }
    public int Height { get; }
    public PhotoCropRect Rect => current.Rect;
    public PhotoCropAspect Aspect => current.Aspect;
    public bool IsInteracting => origin is not null;
    public bool HasChanges => Rect != initial;
    public bool CanUndo => !IsInteracting && undo.Count > 0;
    public bool CanRedo => !IsInteracting && redo.Count > 0;
    public PhotoCropSession(int width, int height, PhotoCropRect? crop = null)
    {
        (crop ?? PhotoCropRect.Full(width, height)).Validate(width, height);
        Width = width; Height = height; initial = crop ?? PhotoCropRect.Full(width, height); current = new(initial, PhotoCropAspect.Free);
    }
    private double Ratio => Aspect switch { PhotoCropAspect.Original => (double)Width / Height, PhotoCropAspect.Square => 1,
        PhotoCropAspect.FourThree => 4d / 3, PhotoCropAspect.SixteenNine => 16d / 9, _ => 0 };
    public void Begin() { origin ??= current; }
    public void End(bool cancel = false)
    {
        if (origin is not { } previous) return; origin = null;
        if (cancel) current = previous; else Commit(previous);
    }
    public void Drag(PhotoCropHandle handle, double dx, double dy)
    {
        if (origin is not { } start || !double.IsFinite(dx) || !double.IsFinite(dy) || !Enum.IsDefined(handle)) return;
        if (dx == 0 && dy == 0) { current = start; return; }
        var r = start.Rect;
        if (handle == PhotoCropHandle.Move)
        {
            current = current with { Rect = new((int)Math.Round(Math.Clamp(r.X + dx, 0, Width - r.Width)),
                (int)Math.Round(Math.Clamp(r.Y + dy, 0, Height - r.Height)), r.Width, r.Height) }; return;
        }
        var left = handle is PhotoCropHandle.TopLeft or PhotoCropHandle.Left or PhotoCropHandle.BottomLeft;
        var right = handle is PhotoCropHandle.TopRight or PhotoCropHandle.Right or PhotoCropHandle.BottomRight;
        var top = handle is PhotoCropHandle.TopLeft or PhotoCropHandle.Top or PhotoCropHandle.TopRight;
        var bottom = handle is PhotoCropHandle.BottomLeft or PhotoCropHandle.Bottom or PhotoCropHandle.BottomRight;
        double x = left ? Math.Clamp(r.X + dx, 0, r.X + r.Width - 1) : r.X;
        double y = top ? Math.Clamp(r.Y + dy, 0, r.Y + r.Height - 1) : r.Y;
        double w = left ? r.X + r.Width - x : right ? Math.Clamp(r.Width + dx, 1, Width - r.X) : r.Width;
        double h = top ? r.Y + r.Height - y : bottom ? Math.Clamp(r.Height + dy, 1, Height - r.Y) : r.Height;
        if (Ratio > 0)
        {
            var ratio = Ratio; var cx = r.X + r.Width / 2d; var cy = r.Y + r.Height / 2d;
            if ((left || right) && (top || bottom))
            {
                // Project onto the aspect diagonal instead of switching the
                // dominant axis (which jumps when dx/dy oppose each other).
                w = r.Width + ((left ? -dx : dx) + (top ? -dy : dy) / ratio) / (1 + 1 / (ratio * ratio));
                var maxW = left ? r.X + r.Width : Width - r.X; var maxH = top ? r.Y + r.Height : Height - r.Y;
                w = Math.Clamp(w, Math.Min(maxW, Math.Min(maxH * ratio, Math.Max(1, ratio))), Math.Min(maxW, maxH * ratio)); h = w / ratio;
                x = left ? r.X + r.Width - w : r.X; y = top ? r.Y + r.Height - h : r.Y;
            }
            else if (left || right)
            {
                var maxW = Math.Min(left ? r.X + r.Width : Width - r.X, 2 * Math.Min(cy, Height - cy) * ratio);
                w = Math.Clamp(w, Math.Min(maxW, Math.Max(1, ratio)), maxW); h = w / ratio;
                x = left ? r.X + r.Width - w : r.X; y = cy - h / 2;
            }
            else
            {
                var maxH = Math.Min(top ? r.Y + r.Height : Height - r.Y, 2 * Math.Min(cx, Width - cx) / ratio);
                h = Math.Clamp(h, Math.Min(maxH, Math.Max(1, 1 / ratio)), maxH); w = h * ratio;
                y = top ? r.Y + r.Height - h : r.Y; x = cx - w / 2;
            }
        }
        current = current with { Rect = Rounded(x, y, w, h) };
    }
    public void SetAspect(PhotoCropAspect aspect)
    {
        if (IsInteracting || !Enum.IsDefined(aspect) || aspect == Aspect) return;
        var previous = current; current = current with { Aspect = aspect };
        if (Ratio > 0)
        {
            var r = Rect; var w = Math.Min(r.Width, r.Height * Ratio); var h = w / Ratio;
            current = current with { Rect = Rounded(r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h) };
        }
        Commit(previous);
    }
    public void Reset() { if (IsInteracting) return; var previous = current; current = new(PhotoCropRect.Full(Width, Height), PhotoCropAspect.Free); Commit(previous); }
    public void Undo() { if (!CanUndo) return; redo.Add(current); current = undo[^1]; undo.RemoveAt(undo.Count - 1); }
    public void Redo() { if (!CanRedo) return; undo.Add(current); current = redo[^1]; redo.RemoveAt(redo.Count - 1); }
    private void Commit(State previous)
    {
        if (current == previous) return; redo.Clear(); undo.Add(previous); if (undo.Count > 30) undo.RemoveAt(0);
    }
    private PhotoCropRect Rounded(double x, double y, double w, double h)
    {
        var ix = (int)Math.Round(Math.Clamp(x, 0, Width - 1)); var iy = (int)Math.Round(Math.Clamp(y, 0, Height - 1));
        // Round absolute edges together. Rounding an origin and width separately
        // can move the fixed opposite edge when both fall on half pixels.
        var right = Math.Clamp((int)Math.Round(x + w), ix + 1, Width);
        var bottom = Math.Clamp((int)Math.Round(y + h), iy + 1, Height);
        return new(ix, iy, right - ix, bottom - iy);
    }
}
