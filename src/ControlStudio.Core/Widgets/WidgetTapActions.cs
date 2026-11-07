namespace ControlStudio.Core.Widgets;

public enum WidgetTapAction { EditWidget, RunShortcut }

/// <summary>Named Shortcuts only; no arbitrary scheme, input or callback execution.</summary>
public static class WidgetTapActions
{
    public const int MaximumShortcutNameLength = 200;
    public static bool ValidName(string? name) => name is not null && name.Length <= MaximumShortcutNameLength
        && !string.IsNullOrWhiteSpace(name) && ValidCharacters(name);
    public static void Validate(WidgetTapAction action, string? name)
    {
        if (!Enum.IsDefined(action) || name is null || name.Length > MaximumShortcutNameLength || !ValidCharacters(name))
            throw new IOException("Choose a valid widget tap action and Shortcut name.");
        if (action == WidgetTapAction.RunShortcut && !ValidName(name))
            throw new IOException("Enter the exact name of a Shortcut saved in the Shortcuts app.");
    }
    public static Uri ShortcutUri(string name)
    {
        Validate(WidgetTapAction.RunShortcut, name);
        // MAUI 10.0.110's iOS launcher passes OriginalString to NSUrl. Keep
        // Apple's command format there, with one fully escaped name value.
        return new("shortcuts://run-shortcut?name=" + Uri.EscapeDataString(name.Trim()), UriKind.Absolute);
    }
    private static bool ValidCharacters(string name)
    {
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsControl(name[i])) return false;
            if (char.IsHighSurrogate(name[i]))
            {
                if (++i >= name.Length || !char.IsLowSurrogate(name[i])) return false;
            }
            else if (char.IsLowSurrogate(name[i])) return false;
        }
        return true;
    }
}
