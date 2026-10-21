using ControlStudio.Core;

namespace ControlStudio.App;

public sealed class AppearancePreferences : BindableObject
{
    private Application? application;
    public int ThemeIndex { get; private set; } = ReadTheme();
    public string AccentHex { get; private set; } = AppearanceValues.TryNormalizeHex(Preferences.Default.Get("accent", AppearanceValues.DefaultAccent), out var hex) ? hex : AppearanceValues.DefaultAccent;
    public bool CustomColourEnabled { get; private set; } = Preferences.Default.Get("custom_colour_enabled", true);
    public string DisplayName { get; private set; } = AppearanceValues.TryNormalizeName(Preferences.Default.Get("profile_name", "Faikal"), out var name) ? name : "Faikal";
    public string PhotoKey { get; private set; } = Preferences.Default.Get("profile_photo", "");

    public void Attach(Application app)
    {
        application = app;
        Apply();
    }

    public void SetTheme(int index)
    {
        if (index is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(index));
        ThemeIndex = index;
        Preferences.Default.Set("appearance", index);
        Apply(); OnPropertyChanged(nameof(ThemeIndex));
    }

    public bool SetAccent(string? value, bool persist = true)
    {
        if (!AppearanceValues.TryNormalizeHex(value, out var normalized)) return false;
        if (normalized == AccentHex) { if (persist) SaveAccent(); return true; }
        AccentHex = normalized;
        if (persist) SaveAccent();
        Apply(); OnPropertyChanged(nameof(AccentHex));
        return true;
    }

    public void SaveAccent() => Preferences.Default.Set("accent", AccentHex);

    public void SetCustomColourEnabled(bool enabled)
    {
        CustomColourEnabled = enabled;
        Preferences.Default.Set("custom_colour_enabled", enabled);
        OnPropertyChanged(nameof(CustomColourEnabled));
    }

    public bool SaveProfile(string? displayName, string photoKey)
    {
        if (!AppearanceValues.TryNormalizeName(displayName, out var normalized)) return false;
        DisplayName = normalized; PhotoKey = photoKey;
        Preferences.Default.Set("profile_name", normalized);
        Preferences.Default.Set("profile_photo", photoKey);
        OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(PhotoKey));
        return true;
    }

    private void Apply()
    {
        if (application is null) return;
        application.UserAppTheme = ThemeIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
        var accent = Color.FromArgb(AccentHex);
        var button = AppearanceColors.ButtonBackground(accent);
        application.Resources["Accent"] = accent;
        application.Resources["AccentButton"] = button;
        application.Resources["AccentGlow"] = new SolidColorBrush(accent.WithAlpha(.38f));
    }

    private static int ReadTheme()
    {
        var value = Preferences.Default.Get("appearance", 0);
        return value is >= 0 and <= 2 ? value : 0;
    }
}
