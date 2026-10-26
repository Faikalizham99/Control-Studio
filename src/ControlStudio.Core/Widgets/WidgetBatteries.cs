namespace ControlStudio.Core.Widgets;

public enum WidgetBatteryShape { Battery, Bar, Ring, Arc, Percentage }
public enum WidgetBatteryColourMode { Ranges, Smooth }
public sealed record WidgetBatteryColourStop(int FromPercent, string ColourHex);
public sealed record WidgetBatteryOptions(WidgetBatteryShape Shape, WidgetBatteryColourMode ColourMode,
    IReadOnlyList<WidgetBatteryColourStop> Stops, bool ShowPercentage = true, bool ShowCharging = true)
{
    // A decoded manifest and catalog must compare by value, including their independently decoded stops.
    public bool Equals(WidgetBatteryOptions? other) => other is not null && Shape == other.Shape && ColourMode == other.ColourMode
        && ShowPercentage == other.ShowPercentage && ShowCharging == other.ShowCharging
        && (ReferenceEquals(Stops, other.Stops) || Stops is not null && other.Stops is not null && Stops.SequenceEqual(other.Stops));
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(Shape); hash.Add(ColourMode); hash.Add(ShowPercentage); hash.Add(ShowCharging);
        if (Stops is not null) foreach (var stop in Stops) hash.Add(stop);
        return hash.ToHashCode();
    }
}
public readonly record struct WidgetBatteryReading(int? Percent = null, bool Charging = false, bool Full = false)
{
    public bool Known => Percent is >= 0 and <= 100;
    public string Text => Known ? Percent + "%" : "—";
}

public static class WidgetBatteries
{
    public const int MaximumStops = 8;
    public static WidgetBatteryOptions Defaults() => new(WidgetBatteryShape.Battery, WidgetBatteryColourMode.Ranges,
        Array.AsReadOnly(new[] { new WidgetBatteryColourStop(0, "#FF453A"), new(21, "#FF9F0A"), new(51, "#0A84FF"), new(81, "#30D158") }));
    public static WidgetLayer New(string name = "Battery 1") => new(Guid.NewGuid(), name, "", 1, 1,
        new(.15, .3, .7, .4), Kind: WidgetLayerKind.Battery, Battery: Defaults());
    public static void Validate(WidgetBatteryOptions options)
    {
        if (!Enum.IsDefined(options.Shape) || !Enum.IsDefined(options.ColourMode) || options.Stops is null
            || options.Stops.Count is < 2 or > MaximumStops || options.Stops[0]?.FromPercent != 0)
            throw new IOException("Choose between 2 and 8 battery colour stops, starting at 0%.");
        var previous = -1;
        foreach (var stop in options.Stops)
        {
            if (stop is null || stop.FromPercent <= previous || stop.FromPercent > 100
                || !AppearanceValues.TryNormalizeHex(stop.ColourHex, out var hex) || hex != stop.ColourHex)
                throw new IOException("Battery thresholds must increase from 0 to 100, with a valid colour at each stop.");
            previous = stop.FromPercent;
        }
    }
    public static string Colour(WidgetBatteryOptions options, WidgetBatteryReading reading)
    {
        if (!reading.Known) return "#8E8E93";
        var percent = reading.Percent!.Value;
        var left = options.Stops[0];
        foreach (var right in options.Stops.Skip(1))
        {
            if (percent < right.FromPercent)
            {
                if (options.ColourMode == WidgetBatteryColourMode.Ranges) return left.ColourHex;
                var t = (double)(percent - left.FromPercent) / (right.FromPercent - left.FromPercent);
                int Channel(int offset) => (int)Math.Round(Convert.ToInt32(left.ColourHex.Substring(offset, 2), 16) * (1 - t)
                    + Convert.ToInt32(right.ColourHex.Substring(offset, 2), 16) * t, MidpointRounding.AwayFromZero);
                return $"#{Channel(1):X2}{Channel(3):X2}{Channel(5):X2}";
            }
            left = right;
        }
        return left.ColourHex;
    }
}
