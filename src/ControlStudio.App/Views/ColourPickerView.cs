using ControlStudio.Core;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal sealed class ColourChangedEventArgs(string hex, bool persist) : EventArgs
{
    public string Hex { get; } = hex;
    public bool Persist { get; } = persist;
}

/// <summary>Shared palette, hue, HEX and native picker. Its host owns persistence.</summary>
internal sealed class ColourPickerView : ContentView
{
    private readonly IColourPicker picker;
    private readonly List<ColourChoice> choices = [];
    private readonly Switch custom = new() { HorizontalOptions = LayoutOptions.End };
    private readonly Slider hue = new() { Minimum = 0, Maximum = 360, MinimumTrackColor = Colors.Transparent, MaximumTrackColor = Colors.Transparent, ThumbColor = Colors.White, HeightRequest = 30 };
    private readonly HexColorEntry hex = new()
    {
        FontSize = 16, FontFamily = "Menlo", MaxLength = 7, HorizontalTextAlignment = TextAlignment.Start,
        VerticalTextAlignment = TextAlignment.Center, ReturnType = ReturnType.Done, TextTransform = TextTransform.Uppercase,
        IsTextPredictionEnabled = false, IsSpellCheckEnabled = false, BackgroundColor = Colors.Transparent, HeightRequest = 36,
        MinimumWidthRequest = 0, ClearButtonVisibility = ClearButtonVisibility.WhileEditing
    };
    private readonly Border well, field;
    private readonly VerticalStackLayout customEditor = new() { Spacing = 8 };
    private readonly Label error = SettingsControls.Text("", 12, secondary: true);
    private bool updating, dragging, invalidHex, dirtyHex;
    public string SelectedHex { get; private set; } = AppearanceValues.DefaultAccent;
    public bool CustomEditingEnabled { get; private set; } = true;
    public bool IsBusy { get; private set; }
    public event EventHandler<ColourChangedEventArgs>? ColourChanged;
    public event EventHandler<ToggledEventArgs>? CustomEditingChanged;
    public event EventHandler? BusyChanged;

    public ColourPickerView(IColourPicker picker, string prefix = "Accent", string colourName = "Accent")
    {
        this.picker = picker;
        string Id(string original, string suffix) => prefix == "Accent" ? original : prefix + suffix;
        AutomationId = prefix + "ColourPicker";
        well = SettingsControls.Rounded(new Grid(), 10);
        well.WidthRequest = well.HeightRequest = 40; well.VerticalOptions = LayoutOptions.Center;
        field = SettingsControls.Rounded(new Grid(), 16, new Thickness(12, 8));
        var palette = new Grid { ColumnSpacing = 2 };
        var names = new[] { "Blue", "Purple", "Pink", "Green", "Orange" };
        for (var i = 0; i < AppearanceValues.Presets.Count; i++)
        {
            palette.ColumnDefinitions.Add(new(GridLength.Star));
            var value = AppearanceValues.Presets[i];
            var choice = new ColourChoice(names[i], SettingsControls.Swatch(Color.FromArgb(value)), prefix + names[i], () => Select(value));
            choices.Add(choice); palette.Add(choice, i);
        }
        palette.ColumnDefinitions.Add(new(GridLength.Star));
        var spectrum = new LinearGradientBrush(new GradientStopCollection { new(Colors.Magenta, 0), new(Colors.Cyan, .33f), new(Colors.Lime, .5f), new(Colors.Yellow, .67f), new(Colors.Orange, .83f), new(Colors.Magenta, 1) }, new Point(0, 0), new Point(1, 1));
        var customChoice = new ColourChoice("Custom colour", spectrum, prefix + "Custom", async () => await PickAsync(), true);
        choices.Add(customChoice); palette.Add(customChoice, 5);
        custom.SetDynamicResource(Switch.OnColorProperty, "Accent");
        custom.AutomationId = Id("CustomColourEnabled", "CustomEnabled"); SemanticProperties.SetDescription(custom, $"Enable custom {colourName.ToLowerInvariant()} colour editing");
        custom.Toggled += (_, args) =>
        {
            if (updating) return;
            CustomEditingEnabled = args.Value;
            if (!args.Value) { dirtyHex = invalidHex = false; ClearError(); }
            Refresh(forceHex: !args.Value); CustomEditingChanged?.Invoke(this, args);
        };
        var heading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var title = SettingsControls.Text("Custom Colour", 13, bold: true); title.VerticalOptions = LayoutOptions.Center;
        heading.Add(title); heading.Add(custom, 1);
        var hueBrush = new LinearGradientBrush(new GradientStopCollection {
            new(Colors.Magenta, 0), new(Colors.Blue, 1f/6), new(Colors.Cyan, 2f/6), new(Colors.Lime, 3f/6), new(Colors.Yellow, 4f/6), new(Colors.Red, 5f/6), new(Colors.Magenta, 1)
        }, new Point(0, 0), new Point(1, 0));
        var bar = new Border { Background = hueBrush, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 }, HeightRequest = 15, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(5, 0) };
        hue.AutomationId = prefix + "Hue"; SemanticProperties.SetDescription(hue, colourName + " hue");
        hue.DragStarted += (_, _) => dragging = true;
        hue.DragCompleted += (_, _) => { dragging = false; Publish(SelectedHex, true); Refresh(); };
        hue.ValueChanged += (_, args) =>
        {
            if (!updating) Select(AppearanceColors.ToRgbHex(Color.FromHsv((float)((300 - args.NewValue + 360) % 360) / 360, 1, 1)), !dragging);
        };
        customEditor.Children.Add(new Grid { Children = { bar, hue } });
        hex.AutomationId = Id("CustomAccentHex", "Hex");
        hex.Completed += (_, _) => CommitPendingHex(); hex.Unfocused += (_, _) => CommitPendingHex();
        hex.Focused += (_, _) => UpdateHexBorder();
        hex.TextChanged += (_, _) =>
        {
            if (updating) return;
            dirtyHex = true; invalidHex = false; ClearError(); UpdateHexBorder();
        };
        SemanticProperties.SetDescription(hex, colourName + " colour in six-digit hexadecimal");
        hex.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
        var hexLabel = SettingsControls.Text("HEX", 10, secondary: true, bold: true); hexLabel.CharacterSpacing = 1.2;
        var hexField = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { hexLabel, hex } };
        var eyedropper = new ImageButton { WidthRequest = 44, HeightRequest = 44, Padding = 11, CornerRadius = 10, BackgroundColor = Colors.Transparent, VerticalOptions = LayoutOptions.Center, AutomationId = Id("OpenColourPicker", "OpenSystemPicker") };
        eyedropper.SetAppTheme(ImageButton.SourceProperty, ImageSource.FromFile("color_picker.png"), ImageSource.FromFile("color_picker_dark.png"));
        SemanticProperties.SetDescription(eyedropper, "Open system colour picker"); eyedropper.Clicked += async (_, _) => await PickAsync();
        var colourTap = new TapGestureRecognizer(); colourTap.Tapped += async (_, _) => await PickAsync(); well.GestureRecognizers.Add(colourTap);
        var controls = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        controls.Add(well); controls.Add(hexField, 1); controls.Add(eyedropper, 2); field.Content = controls;
        customEditor.Children.Add(field); customEditor.Children.Add(error);
        var divider = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 3) };
        divider.SetAppThemeColor(BoxView.ColorProperty, Color.FromArgb("#D9DDE6"), Colors.White.WithAlpha(.14f));
        Content = new VerticalStackLayout { Spacing = 6, Children = { palette, divider, heading, customEditor } };
        Refresh(forceHex: true);
    }

    public void SetSelection(string value, bool customEditingEnabled = true)
    {
        if (!AppearanceValues.TryNormalizeHex(value, out var normalized)) throw new ArgumentException("Use a six-digit RGB colour.", nameof(value));
        var changed = SelectedHex != normalized;
        SelectedHex = normalized; CustomEditingEnabled = customEditingEnabled;
        if (changed) { dirtyHex = invalidHex = false; ClearError(); }
        Refresh(forceHex: changed);
    }

    public bool CommitPendingHex()
    {
        if (updating || (!dirtyHex && !invalidHex)) { UpdateHexBorder(); return true; }
        if (!AppearanceValues.TryNormalizeHex(hex.Text, out var normalized))
        {
            invalidHex = true; error.Text = "Use a six-digit colour, for example #007AFF."; error.IsVisible = true; UpdateHexBorder(); return false;
        }
        Select(normalized); return true;
    }

    private void Select(string value, bool persist = true)
    {
        if (!AppearanceValues.TryNormalizeHex(value, out var normalized)) return;
        SelectedHex = normalized; dirtyHex = invalidHex = false; ClearError(); Refresh(forceHex: true); Publish(normalized, persist);
    }
    private void Publish(string value, bool persist) => ColourChanged?.Invoke(this, new ColourChangedEventArgs(value, persist));
    private void ClearError() { error.Text = ""; error.IsVisible = false; }

    internal async Task PickAsync()
    {
        if (IsBusy || !IsEnabled) return;
        IsBusy = true;
        try
        {
            BusyChanged?.Invoke(this, EventArgs.Empty);
            hex.Unfocus(); dirtyHex = invalidHex = false; ClearError();
            CustomEditingEnabled = true; Refresh(forceHex: true); CustomEditingChanged?.Invoke(this, new ToggledEventArgs(true));
            var value = await picker.PickAsync(SelectedHex, preview => Select(preview, false));
            Select(value);
        }
        catch { error.Text = "The colour picker is unavailable. Enter a hex colour instead."; error.IsVisible = true; }
        finally
        {
            try { Publish(SelectedHex, true); }
            catch { error.Text = "The colour could not be saved. Try again."; error.IsVisible = true; }
            finally { IsBusy = false; BusyChanged?.Invoke(this, EventArgs.Empty); }
        }
    }

    private void Refresh(bool forceHex = false)
    {
        updating = true;
        try
        {
            var colour = Color.FromArgb(SelectedHex);
            for (var i = 0; i < choices.Count; i++) choices[i].Update(i < AppearanceValues.Presets.Count ? AppearanceValues.Presets[i] == SelectedHex : !AppearanceValues.Presets.Contains(SelectedHex), colour);
            custom.IsToggled = CustomEditingEnabled; customEditor.IsEnabled = CustomEditingEnabled;
            if (forceHex || (!hex.IsFocused && !dirtyHex && !invalidHex)) hex.Text = SelectedHex;
            if (!dragging) hue.Value = (300 - Hue(colour) + 360) % 360;
            well.Background = new SolidColorBrush(colour); UpdateHexBorder();
        }
        finally { updating = false; }
    }
    private void UpdateHexBorder()
    {
        field.StrokeThickness = hex.IsFocused || invalidHex ? 1.5 : 1;
        if (invalidHex) field.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#B42336")), new SolidColorBrush(Color.FromArgb("#FF8798")));
        else if (hex.IsFocused)
        {
            var colour = Color.FromArgb(SelectedHex);
            field.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(AppearanceColors.TextAccent(colour, false)), new SolidColorBrush(AppearanceColors.TextAccent(colour, true)));
        }
        else SettingsControls.SurfaceStroke(field);
    }
    private static double Hue(Color colour)
    {
        var max = Math.Max(colour.Red, Math.Max(colour.Green, colour.Blue)); var min = Math.Min(colour.Red, Math.Min(colour.Green, colour.Blue)); var delta = max - min;
        if (delta < .00001) return 0;
        var value = max == colour.Red ? (colour.Green - colour.Blue) / delta : max == colour.Green ? 2 + (colour.Blue - colour.Red) / delta : 4 + (colour.Red - colour.Green) / delta;
        return (value * 60 + 360) % 360;
    }
}
