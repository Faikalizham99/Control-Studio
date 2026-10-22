namespace ControlStudio.App;

/// <summary>Bound temporary blur textures even for an expanded 10,000-character card.</summary>
internal sealed record ClipboardBlurPlan(int PixelWidth, int PixelHeight, double Radius)
{
    public const int MaximumPixels = 196_608;
    public const int MaximumEdge = 1_536;
    public const int AnimationFrames = 16;
    public const double Duration = .26;

    public static ClipboardBlurPlan? Create(double width, double height)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width < 1 || height < 1) return null;
        var scale = Math.Min(1, Math.Min(MaximumEdge / Math.Max(width, height), Math.Sqrt(MaximumPixels / width / height)));
        var pixelsWide = Math.Max(1, (int)Math.Floor(width * scale));
        var pixelsHigh = Math.Max(1, (int)Math.Floor(height * scale));
        // Extreme aspect ratios can round the short edge up to one pixel.
        pixelsHigh = Math.Min(pixelsHigh, MaximumPixels / pixelsWide);
        return new(pixelsWide, pixelsHigh, Math.Max(.5, 14 * Math.Min(pixelsWide / width, pixelsHigh / height)));
    }

    public static double Amount(double from, double to, double elapsed)
    {
        var duration = Duration * Math.Abs(to - from);
        if (duration < .001) return to;
        var progress = Math.Clamp(elapsed / duration, 0, 1);
        if (progress >= 1) return to;
        return Math.Clamp(from + (to - from) * progress * progress * (3 - 2 * progress), Math.Min(from, to), Math.Max(from, to));
    }

    public static int FrameIndex(double amount) => (int)Math.Round(Math.Clamp(amount, 0, 1) * (AnimationFrames - 1));
}
