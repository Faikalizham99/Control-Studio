using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal sealed class WidgetClockControls : ContentView
{
    private readonly WidgetSegments format, fonts, alignment;
    private readonly Switch seconds = SettingsControls.RowSwitch("ClockShowSeconds");
    private readonly Switch amPm = SettingsControls.RowSwitch("ClockShowAmPm");
    private readonly Grid amPmRow;
    private readonly Slider size = new() { Minimum = .2, Maximum = 1, AutomationId = "ClockTextSize" };
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, AutomationId = "ClockOpacity" };
    private readonly Label sizeLabel = SettingsControls.Text("Text size", 14), opacityLabel = SettingsControls.Text("Opacity", 14);
    private readonly Label notice = SettingsControls.Text("", 12, secondary: true);
    private readonly ColourPickerView colour;
    private WidgetLayer? layer;
    private bool updating;
    public bool IsBusy => colour.IsBusy;
    public event EventHandler? BusyChanged;
    public event Action<WidgetLayer>? Changed;
    public WidgetClockControls(IColourPicker picker)
    {
        AutomationId = "WidgetClockControls"; IsVisible = false;
        format = new(["12-hour", "24-hour"], "ClockFormat", index => Edit(x => x with { TwentyFourHour = index == 1 }));
        fonts = new(["System", "Rounded", "Mono", "Serif"], "ClockFont", index => Edit(x => x with { Font = (WidgetClockFont)index }));
        alignment = new(["Left", "Centre", "Right"], "ClockAlignment", index => Edit(x => x with { Alignment = (WidgetClockAlignment)index }));
        seconds.Toggled += (_, args) => Edit(x => x with { ShowSeconds = args.Value });
        amPm.Toggled += (_, args) => { if (layer?.Clock?.TwentyFourHour == false) Edit(x => x with { ShowAmPm = args.Value }); };
        size.ValueChanged += (_, args) => Edit(x => x with { FontScale = args.NewValue });
        opacity.ValueChanged += (_, args) => { if (!updating && IsEnabled && layer is not null) Publish(layer with { Opacity = args.NewValue }); };
        foreach (var slider in new[] { size, opacity }) { slider.MinimumHeightRequest = 44; slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); }
        SemanticProperties.SetDescription(size, "Clock text size within its frame"); SemanticProperties.SetDescription(opacity, "Clock opacity");
        SemanticProperties.SetDescription(seconds, "Show clock seconds");
        SemanticProperties.SetDescription(amPm, "Show AM/PM");
        colour = new(picker, "ClockText", "Clock text");
        colour.ColourChanged += (_, args) => { if (!updating && layer is not null) Publish(layer with { Clock = layer.Clock! with { ColourHex = args.Hex } }); };
        colour.BusyChanged += (_, _) => BusyChanged?.Invoke(this, EventArgs.Empty);
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var label = SettingsControls.Text("Show seconds", 14); label.VerticalOptions = LayoutOptions.Center; row.Add(label); row.Add(seconds, 1);
        amPmRow = new Grid { ColumnSpacing = 8, IsVisible = false, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var amPmLabel = SettingsControls.Text("Show AM/PM", 14); amPmLabel.VerticalOptions = LayoutOptions.Center; amPmRow.Add(amPmLabel); amPmRow.Add(amPm, 1);
        Content = ClipboardEditorControls.Surface(new VerticalStackLayout { Spacing = 12, Children = {
            SettingsControls.Text("Clock", 21, bold: true),
            SettingsControls.Text("Drag or pinch to move and resize. Top layers appear in front. Time follows this iPhone; iOS may simplify it while the display is dimmed.", 12, secondary: true),
            notice, format, row, amPmRow, SettingsControls.Text("Font", 14, bold: true), fonts, sizeLabel, size,
            SettingsControls.Text("Alignment", 14, bold: true), alignment, opacityLabel, opacity,
            SettingsControls.Text("Text colour", 14, bold: true), colour
        } }, 22, 14);
    }
    private void Edit(Func<WidgetClockOptions, WidgetClockOptions> edit)
    {
        if (!updating && IsEnabled && layer?.Clock is { } options) Publish(layer with { Clock = edit(options) });
    }
    private void Publish(WidgetLayer value) { layer = value; Changed?.Invoke(value); }
    public bool CommitPendingColour() => layer is null || colour.CommitPendingHex();
    public void Update(WidgetLayer? value)
    {
        updating = true;
        try
        {
            layer = value?.Kind == WidgetLayerKind.Clock ? value : null; IsVisible = layer is not null;
            if (layer?.Clock is not { } clock) return;
            notice.Text = !layer.Visible ? "This clock is hidden. Show it in Layers to see it in the preview." : layer.Opacity == 0 ? "This clock has 0% opacity. Increase opacity to see it." : "";
            notice.IsVisible = notice.Text.Length > 0;
            format.Update(clock.TwentyFourHour ? 1 : 0); fonts.Update((int)clock.Font); alignment.Update((int)clock.Alignment);
            seconds.IsToggled = clock.ShowSeconds; size.Value = clock.FontScale; opacity.Value = layer.Opacity;
            amPm.IsToggled = clock.ShowAmPm; amPmRow.IsVisible = !clock.TwentyFourHour;
            sizeLabel.Text = $"Text size · {clock.FontScale:P0}"; opacityLabel.Text = $"Opacity · {layer.Opacity:P0}";
            if (colour.SelectedHex != clock.ColourHex) colour.SetSelection(clock.ColourHex);
        }
        finally { updating = false; }
    }
}
