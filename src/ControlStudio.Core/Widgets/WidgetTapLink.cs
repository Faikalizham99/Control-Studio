using System.Globalization;

namespace ControlStudio.Core.Widgets;

/// <summary>A bounded address, never a file path, photo payload or write command.</summary>
public sealed record WidgetTapLink(WidgetSize? Size = null, int Number = 0)
{
    public const string Scheme = "controlstudio";
    public bool IsCounter => Size is null && Number == 0;

    public static bool TryParse(string? value, out WidgetTapLink? link)
    {
        link = null;
        if (value is null || value.Length > 160 || value.Contains('%') || value.Contains('\\') || value.Contains("/.") || value.Any(char.IsWhiteSpace)
            || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Scheme
            || uri.UserInfo.Length != 0 || uri.Port != -1 || uri.Query.Length != 0 || uri.Fragment.Length != 0) return false;
        if (uri.Host == "counter" && uri.AbsolutePath is "" or "/") { link = new(); return true; }
        if (uri.Host != "widget") return false;
        var parts = uri.AbsolutePath.Split('/');
        if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || parts[2] != number.ToString(CultureInfo.InvariantCulture) || number < 1 || number > WidgetSlotRules.SlotsPerSize) return false;
        WidgetSize? size = parts[1] switch
        {
            "small" => WidgetSize.Small, "medium" => WidgetSize.Medium,
            "large" => WidgetSize.Large, "extraLargePortrait" => WidgetSize.ExtraLargePortrait, _ => null
        };
        if (size is null) return false;
        link = new(size, number); return true;
    }

    public WidgetDesign? Resolve(WidgetLibraryCatalog library)
    {
        WidgetSlotRules.Validate(library);
        var assignment = library.Slots.FirstOrDefault(x => x.Size == Size && x.Number == Number);
        return assignment is null ? null : library.Designs.FirstOrDefault(x => x.Id == assignment.DesignId && x.Size == Size);
    }
}
