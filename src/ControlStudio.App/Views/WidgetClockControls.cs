using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal enum WidgetClockTool { Time, Font, Colour, Size, Position, Opacity }

internal sealed class WidgetClockControls : ContentView
{
    private readonly WidgetSegments format, alignment;
    private readonly WidgetClockFontPicker fonts;
    private readonly Switch seconds = SettingsControls.RowSwitch("ClockShowSeconds");
    private readonly Switch amPm = SettingsControls.RowSwitch("ClockShowAmPm");
    private readonly Grid amPmRow;
    private readonly Slider size = new() { Minimum = .2, Maximum = 1, AutomationId = "ClockTextSize" };
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, AutomationId = "ClockOpacity" };
    private readonly Label sizeLabel = SettingsControls.Text("Text size", 14), opacityLabel = SettingsControls.Text("Opacity", 14);
    private readonly Label notice = SettingsControls.Text("", 12, secondary: true);
    private readonly ColourPickerView colour;
    private readonly ClipboardEditorActionButton centre = new("Centre horizontally", ClipboardEditorControls.Icon(StudioSymbol.CenterHorizontal, size: 18), "CentreClockHorizontally", accentCaption: true);
    private readonly Dictionary<WidgetClockTool, (WidgetToolButton Button, View Panel)> tools = [];
    private readonly WidgetToolButton delete;
    private WidgetLayer? layer;
    private bool updating, available = true;
    public WidgetHorizontalStrip Toolbar { get; }
    public WidgetClockTool? ActiveTool { get; private set; }
    public Func<bool>? CanInteract { get; set; }
    public bool IsBusy => colour.IsBusy;
    public event EventHandler? BusyChanged;
    public event Action<WidgetLayer>? Changed;
    public event EventHandler? CentreHorizontallyRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? PanelChanged;
    public event EventHandler? InvalidColour;
    private bool CanUse => available && IsEnabled && !IsBusy && layer is not null && (CanInteract?.Invoke() ?? true);
    public WidgetClockControls(IColourPicker picker, ILogger? logger = null)
    {
        AutomationId = "WidgetClockControls"; IsVisible = false;
        format = new(["12-hour", "24-hour"], "ClockFormat", index => Edit(WidgetClockTool.Time, x => x with { TwentyFourHour = index == 1 }));
        fonts = new(font => Edit(WidgetClockTool.Font, x => WidgetClockWeights.ChangeFont(x, font)),
            weight => { if (layer?.Clock is { } clock && WidgetClockWeights.Available(clock.Font).Contains(weight)) Edit(WidgetClockTool.Font, x => x with { Weight = weight }); });
        alignment = new(["Left", "Centre", "Right"], "ClockAlignment", index => Edit(WidgetClockTool.Position, x => x with { Alignment = (WidgetClockAlignment)index }));
        seconds.Toggled += (_, args) => Edit(WidgetClockTool.Time, x => x with { ShowSeconds = args.Value });
        amPm.Toggled += (_, args) => { if (layer?.Clock?.TwentyFourHour == false) Edit(WidgetClockTool.Time, x => x with { ShowAmPm = args.Value }); };
        size.ValueChanged += (_, args) => Edit(WidgetClockTool.Size, x => x with { FontScale = args.NewValue });
        opacity.ValueChanged += (_, args) => { if (!updating && CanUse && ActiveTool == WidgetClockTool.Opacity) Publish(layer! with { Opacity = args.NewValue }); };
        foreach (var slider in new[] { size, opacity }) { slider.MinimumHeightRequest = 44; slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); }
        SemanticProperties.SetDescription(size, "Clock text size within its frame"); SemanticProperties.SetDescription(opacity, "Clock opacity");
        SemanticProperties.SetDescription(seconds, "Show clock seconds");
        SemanticProperties.SetDescription(amPm, "Show AM/PM");
        centre.Describe("Centre clock horizontally in the widget, keeping its vertical position and size");
        centre.Clicked += (_, _) => { if (!updating && CanUse && ActiveTool == WidgetClockTool.Position && layer is { Locked: false }) CentreHorizontallyRequested?.Invoke(this, EventArgs.Empty); };
        colour = new(picker, "ClockText", "Clock text");
        colour.ColourChanged += (_, args) => { if (!updating && layer is not null) Publish(layer with { Clock = layer.Clock! with { ColourHex = args.Hex } }); };
        colour.BusyChanged += (_, _) => BusyChanged?.Invoke(this, EventArgs.Empty);
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var label = SettingsControls.Text("Show seconds", 14); label.VerticalOptions = LayoutOptions.Center; row.Add(label); row.Add(seconds, 1);
        amPmRow = new Grid { ColumnSpacing = 8, IsVisible = false, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var amPmLabel = SettingsControls.Text("Show AM/PM", 14); amPmLabel.VerticalOptions = LayoutOptions.Center; amPmRow.Add(amPmLabel); amPmRow.Add(amPm, 1);
        var panels = new VerticalStackLayout { Spacing = 10, Children = { notice } };
        void Add(WidgetClockTool tool, StudioSymbol symbol, string title, params View[] controls)
        {
            var body = new VerticalStackLayout { Spacing = 12 };
            var heading = SettingsControls.Text(title, 19, bold: true); SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
            body.Children.Add(heading);
            foreach (var control in controls) body.Children.Add(control);
            var panel = ClipboardEditorControls.Surface(body, 22, 14);
            panel.AutomationId = "ClockPanel" + tool; panel.IsVisible = false; panels.Children.Add(panel);
            var button = new WidgetToolButton(symbol, "Clock " + title.ToLowerInvariant(), automationId: "ClockTool" + tool);
            button.Clicked += (_, _) => Toggle(tool); tools.Add(tool, (button, panel));
        }
        Add(WidgetClockTool.Time, StudioSymbol.Clock, "Time", format, row, amPmRow,
            SettingsControls.Text("Time follows this iPhone. iOS may simplify it while the display is dimmed.", 12, secondary: true));
        Add(WidgetClockTool.Font, StudioSymbol.Font, "Font", fonts);
        Add(WidgetClockTool.Colour, StudioSymbol.Palette, "Colour", colour);
        Add(WidgetClockTool.Size, StudioSymbol.Expand, "Size", sizeLabel, size,
            SettingsControls.Text("Text size within the layer. Pinch the preview to resize the whole clock around its centre.", 12, secondary: true));
        Add(WidgetClockTool.Position, StudioSymbol.CenterHorizontal, "Position", centre,
            SettingsControls.Text("Drag the clock to move it. The layer menu also offers precise size and position controls.", 12, secondary: true),
            SettingsControls.Text("Text alignment", 14, bold: true), alignment);
        Add(WidgetClockTool.Opacity, StudioSymbol.Opacity, "Opacity", opacityLabel, opacity);
        delete = new WidgetToolButton(StudioSymbol.Delete, "Delete clock layer", destructive: true, automationId: "ClockToolDelete");
        delete.Clicked += (_, _) => { if (CanUse && ValidateColour()) DeleteRequested?.Invoke(this, EventArgs.Empty); };
        Toolbar = new WidgetHorizontalStrip(tools.Values.Select(x => (View)x.Button).Append(delete), "ClockEditorToolbar", logger) { IsVisible = false };
        Content = panels;
    }
    private bool ValidateColour()
    {
        if (CommitPendingColour()) return true;
        InvalidColour?.Invoke(this, EventArgs.Empty); return false;
    }
    private void Toggle(WidgetClockTool tool)
    {
        if (!CanUse || !ValidateColour()) return;
        SetPanel(ActiveTool == tool ? null : tool);
    }
    private void SetPanel(WidgetClockTool? tool)
    {
        var changed = ActiveTool != tool; ActiveTool = tool;
        foreach (var (key, item) in tools) { item.Panel.IsVisible = key == tool; item.Button.IsSelected = key == tool; }
        IsVisible = layer is not null && tool is not null;
        if (changed)
        {
            if (tool is { } selected) Toolbar.Reveal(tools[selected].Button);
            PanelChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public void ClosePanel() { if (!IsBusy) SetPanel(null); }
    public void SetAvailable(bool value)
    {
        available = value; IsEnabled = value; Toolbar.IsEnabled = value;
        centre.IsEnabled = value && layer is { Locked: false };
    }
    private void Edit(WidgetClockTool tool, Func<WidgetClockOptions, WidgetClockOptions> edit)
    {
        if (!updating && CanUse && ActiveTool == tool && layer?.Clock is { } options) Publish(layer with { Clock = edit(options) });
    }
    private void Publish(WidgetLayer value) { layer = value; Changed?.Invoke(value); }
    public bool CommitPendingColour()
    {
        if (layer is null || colour.CommitPendingHex()) return true;
        SetPanel(WidgetClockTool.Colour); return false;
    }
    public void Update(WidgetLayer? value)
    {
        updating = true;
        try
        {
            var next = value?.Kind == WidgetLayerKind.Clock ? value : null;
            var changedLayer = next?.Id != layer?.Id; layer = next;
            Toolbar.IsVisible = layer is not null;
            if (changedLayer) SetPanel(null);
            IsVisible = layer is not null && ActiveTool is not null;
            centre.IsEnabled = available && layer is { Locked: false };
            if (layer?.Clock is not { } clock) return;
            notice.Text = !layer.Visible ? "This clock is hidden. Show it in Layers to see it in the preview." : layer.Opacity == 0 ? "This clock has 0% opacity. Increase opacity to see it." : "";
            notice.IsVisible = notice.Text.Length > 0;
            format.Update(clock.TwentyFourHour ? 1 : 0); fonts.Update(clock.Font, clock.Weight); alignment.Update((int)clock.Alignment);
            seconds.IsToggled = clock.ShowSeconds; size.Value = clock.FontScale; opacity.Value = layer.Opacity;
            amPm.IsToggled = clock.ShowAmPm; amPmRow.IsVisible = !clock.TwentyFourHour;
            sizeLabel.Text = $"Text size · {clock.FontScale:P0}"; opacityLabel.Text = $"Opacity · {layer.Opacity:P0}";
            if (colour.SelectedHex != clock.ColourHex) colour.SetSelection(clock.ColourHex);
            foreach (var (key, item) in tools) item.Button.Describe("Clock " + key.ToString().ToLowerInvariant(), layer.Name);
            delete.Describe("Delete clock layer", layer.Name);
        }
        finally { updating = false; }
    }
}
