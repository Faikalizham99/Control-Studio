namespace ControlStudio.Core.Widgets;

/// <summary>A square context crop preserves proportions; only selected pixels are composited back.</summary>
public sealed record PhotoObjectRemoval(int Width, int Height, int Left, int Top, int Edge)
{
    public const int ModelEdge = 512;
    public static PhotoObjectRemoval Create(int width, int height, byte[] selection)
    {
        if (width is < 1 or > WidgetDesignRules.MaximumImageEdge || height is < 1 or > WidgetDesignRules.MaximumImageEdge || selection.Length != width * height)
            throw new IOException("The object selection is invalid.");
        var left = width; var right = -1; var top = height; var bottom = -1;
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) if (selection[y * width + x] != 0)
        { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        if (right < 0) throw new IOException("Brush over the object you want to remove first.");
        if (selection.All(x => x != 0)) throw new IOException("Leave some surrounding background unselected so the area can be filled.");
        var edge = Math.Min(WidgetDesignRules.MaximumImageEdge, Math.Max(128, Math.Max(right - left + 1, bottom - top + 1) * 2));
        var cropX = width >= edge ? Math.Clamp((left + right + 1 - edge) / 2, 0, width - edge) : -(edge - width) / 2;
        var cropY = height >= edge ? Math.Clamp((top + bottom + 1 - edge) / 2, 0, height - edge) : -(edge - height) / 2;
        return new(width, height, cropX, cropY, edge);
    }
    public float[] Input(byte[] rgba, byte[] selection, CancellationToken token = default)
    {
        Validate(rgba, selection); var plane = ModelEdge * ModelEdge; var input = new float[plane * 4];
        // First conservatively rasterize the mask so thin brush marks survive reduction.
        for (var y = 0; y < ModelEdge; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < ModelEdge; x++)
            {
                var px = Left + (x + .5) * Edge / ModelEdge; var py = Top + (y + .5) * Edge / ModelEdge;
                var masked = false; var reach = Math.Max(1, (int)Math.Ceiling((double)Edge / ModelEdge));
                for (var sy = (int)Math.Floor(py) - reach; sy <= (int)Math.Floor(py) + reach && !masked; sy++)
                    for (var sx = (int)Math.Floor(px) - reach; sx <= (int)Math.Floor(px) + reach; sx++)
                        if (selection[Math.Clamp(sy, 0, Height - 1) * Width + Math.Clamp(sx, 0, Width - 1)] != 0) { masked = true; break; }
                var index = y * ModelEdge + x; input[plane * 3 + index] = masked ? 1 : 0;
                var at = (Math.Clamp((int)py, 0, Height - 1) * Width + Math.Clamp((int)px, 0, Width - 1)) * 4;
                var alpha = rgba[at + 3];
                for (var c = 0; c < 3; c++)
                    input[c * plane + index] = masked || alpha == 0 ? 1 : Math.Clamp(rgba[at + c] * 255f / alpha, 0, 255) / 127.5f - 1;
            }
        }
        return input;
    }
    public byte[] Composite(byte[] rgba, byte[] selection, float[] prediction, CancellationToken token = default)
    {
        Validate(rgba, selection); var plane = ModelEdge * ModelEdge;
        if (prediction.Length != plane * 3 || prediction.Any(x => !float.IsFinite(x))) throw new IOException("The object removal returned an invalid image.");
        var result = rgba.ToArray();
        for (var y = 0; y < Height; y++)
        {
            token.ThrowIfCancellationRequested();
            for (var x = 0; x < Width; x++)
            {
                var pixel = y * Width + x; var amount = selection[pixel] / 255f; var at = pixel * 4;
                if (amount == 0 || rgba[at + 3] == 0) continue;
                var mx = Math.Clamp((x + .5 - Left) * ModelEdge / Edge - .5, 0, ModelEdge - 1);
                var my = Math.Clamp((y + .5 - Top) * ModelEdge / Edge - .5, 0, ModelEdge - 1);
                var ix = (int)mx; var iy = (int)my; var fx = (float)(mx - ix); var fy = (float)(my - iy);
                for (var c = 0; c < 3; c++)
                {
                    float Sample(int xx, int yy) => prediction[c * plane + Math.Min(yy, ModelEdge - 1) * ModelEdge + Math.Min(xx, ModelEdge - 1)];
                    var generated = (Sample(ix, iy) * (1 - fx) + Sample(ix + 1, iy) * fx) * (1 - fy)
                        + (Sample(ix, iy + 1) * (1 - fx) + Sample(ix + 1, iy + 1) * fx) * fy;
                    var premultiplied = Math.Clamp((generated + 1) * .5f, 0, 1) * rgba[at + 3];
                    result[at + c] = (byte)Math.Clamp(Math.Round(rgba[at + c] * (1 - amount) + premultiplied * amount), 0, rgba[at + 3]);
                }
            }
        }
        return result;
    }
    private void Validate(byte[] rgba, byte[] selection)
    { if (selection.Length != Width * Height || rgba.Length != selection.Length * 4) throw new IOException("The object removal dimensions are invalid."); }
}
