using ControlStudio.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;

namespace ControlStudio.App;

public sealed class AppearancePreferences : BindableObject
{
    private Application? application;
    private readonly IPreferences storage;
    private readonly ICounterAppearancePublisher publisher;
    private readonly ILogger<AppearancePreferences> logger;
    public int ThemeIndex { get; private set; }
    public string AccentHex { get; private set; }
    public bool CustomColourEnabled { get; private set; }
    public string DisplayName { get; private set; }
    public string PhotoKey { get; private set; }

    public AppearancePreferences(ICounterAppearancePublisher publisher, ILogger<AppearancePreferences> logger)
        : this(publisher, logger, Preferences.Default) { }

    internal AppearancePreferences(ICounterAppearancePublisher publisher, ILogger<AppearancePreferences> logger, IPreferences storage)
    {
        this.storage = storage; this.publisher = publisher; this.logger = logger;
        var theme = storage.Get("appearance", 0); ThemeIndex = theme is >= 0 and <= 2 ? theme : 0;
        AccentHex = AppearanceValues.TryNormalizeHex(storage.Get("accent", AppearanceValues.DefaultAccent), out var hex) ? hex : AppearanceValues.DefaultAccent;
        CustomColourEnabled = storage.Get("custom_colour_enabled", true);
        DisplayName = AppearanceValues.TryNormalizeName(storage.Get("profile_name", "Faikal"), out var name) ? name : "Faikal";
        PhotoKey = storage.Get("profile_photo", "");
    }

    public void Attach(Application app)
    {
        if (application is not null) application.RequestedThemeChanged -= ThemeChanged;
        application = app;
        application.RequestedThemeChanged += ThemeChanged;
        Apply(); SynchronizeCounterAppearance();
    }

    public void SetTheme(int index)
    {
        if (index is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(index));
        ThemeIndex = index;
        storage.Set("appearance", index);
        Apply(); SynchronizeCounterAppearance(); OnPropertyChanged(nameof(ThemeIndex));
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

    public void SaveAccent()
    {
        storage.Set("accent", AccentHex);
        SynchronizeCounterAppearance();
    }

    public void SynchronizeCounterAppearance()
    {
        // Only committed colour values are published. Dragging the Settings hue
        // slider previews locally without exhausting WidgetKit refresh budgets.
        try
        {
            var committed = AppearanceValues.TryNormalizeHex(storage.Get("accent", AppearanceValues.DefaultAccent), out var hex) ? hex : AppearanceValues.DefaultAccent;
            publisher.Publish(ThemeIndex, committed);
        }
        catch (Exception ex) { logger.LogWarning("Counter appearance publication unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }

    public void SetCustomColourEnabled(bool enabled)
    {
        CustomColourEnabled = enabled;
        storage.Set("custom_colour_enabled", enabled);
        OnPropertyChanged(nameof(CustomColourEnabled));
    }

    public bool SaveProfile(string? displayName, string photoKey)
    {
        if (!AppearanceValues.TryNormalizeName(displayName, out var normalized)) return false;
        DisplayName = normalized; PhotoKey = photoKey;
        storage.Set("profile_name", normalized);
        storage.Set("profile_photo", photoKey);
        OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(PhotoKey));
        return true;
    }

    private void Apply()
    {
        if (application is null) return;
        application.UserAppTheme = ThemeIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
        ApplyAccentResources();
    }

    private void ThemeChanged(object? sender, AppThemeChangedEventArgs args) => ApplyAccentResources();

    private void ApplyAccentResources()
    {
        if (application is null) return;
        var accent = Color.FromArgb(AccentHex);
        application.Resources["Accent"] = accent;
        application.Resources["AccentButton"] = accent;
        application.Resources["AccentButtonBrush"] = new SolidColorBrush(accent);
        application.Resources["AccentButtonText"] = AppearanceColors.ButtonForeground(accent);
        application.Resources["AccentActionText"] = AppearanceColors.ButtonForeground(accent, 19, bold: true);
        application.Resources["AccentOnLight"] = AppearanceColors.TextAccent(accent, false);
        var textAccent = AppearanceColors.TextAccent(accent, application.RequestedTheme == AppTheme.Dark);
        application.Resources["AccentText"] = textAccent;
        application.Resources["AccentSelectionBrush"] = new SolidColorBrush(accent.WithAlpha(.12f));
        application.Resources["AccentSelectionStroke"] = new SolidColorBrush(textAccent.WithAlpha(.6f));
        application.Resources["AccentGlow"] = new SolidColorBrush(accent.WithAlpha(.38f));
        application.Resources["CounterIconBackground"] = AppearanceColors.ButtonForeground(accent);
    }

}
