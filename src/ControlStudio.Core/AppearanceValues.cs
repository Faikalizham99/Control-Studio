namespace ControlStudio.Core;

public static class AppearanceValues
{
    public const string DefaultAccent = "#007AFF";
    public static IReadOnlyList<string> Presets { get; } = Array.AsReadOnly(new[]
        { DefaultAccent, "#AF52DE", "#FF2D85", "#34C759", "#FF9500" });

    public static bool TryNormalizeHex(string? value, out string hex)
    {
        hex = DefaultAccent;
        if (value is null) return false;
        var input = value.Trim();
        if (input.StartsWith('#')) input = input[1..];
        if (input.Length != 6 || input.Any(c => !Uri.IsHexDigit(c))) return false;
        hex = "#" + input.ToUpperInvariant();
        return true;
    }

    public static bool TryNormalizeName(string? value, out string name)
    {
        name = value?.Trim() ?? "";
        return name.Length is >= 1 and <= 40 && !name.Any(char.IsControl);
    }

    public static double Luminance(string hex)
    {
        if (!TryNormalizeHex(hex, out var normalized)) throw new ArgumentException("Use a six-digit RGB color.", nameof(hex));
        static double Channel(string component)
        {
            var value = Convert.ToByte(component, 16) / 255d;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        return .2126 * Channel(normalized[1..3]) + .7152 * Channel(normalized[3..5]) + .0722 * Channel(normalized[5..7]);
    }
}
