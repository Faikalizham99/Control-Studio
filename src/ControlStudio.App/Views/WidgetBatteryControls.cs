using System.Globalization;
using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal enum WidgetBatteryTool { Shape, Colour, Position, Opacity }
internal sealed class WidgetBatteryControls : ContentView
{
    private readonly ColourPickerView colour;
    private readonly WidgetSegments mode;
    private readonly Entry threshold = new() { Keyboard = Keyboard.Numeric, MaxLength = 3, HeightRequest = 44,
        BackgroundColor = Colors.Transparent, AutomationId = "BatteryThreshold", ReturnType = ReturnType.Done };
    private readonly Label error = SettingsControls.Text("", 13, secondary: true);
    private readonly Label ruleHelp = SettingsControls.Text("", 12, secondary: true);
    private readonly Label editing = SettingsControls.Text("", 14, bold: true);
    private readonly Switch percentage = SettingsControls.RowSwitch("BatteryShowPercentage"), charging = SettingsControls.RowSwitch("BatteryShowCharging");
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, MinimumHeightRequest = 44, AutomationId = "BatteryOpacity" };
    private readonly Label opacityLabel = SettingsControls.Text("Opacity", 14);
    private readonly ClipboardEditorActionButton add, remove, centre;
    private readonly WidgetLayerNudgeControls nudge = new("BatteryPositionNudge");
    private readonly WidgetRotationControls rotation = new("BatteryRotation");
    private readonly Dictionary<WidgetBatteryTool, (WidgetToolButton Button, View Panel)> tools = [];
    private readonly List<(Border Outline, Border Swatch, Label Label, Button Tap)> stopViews = [];
    private readonly List<(WidgetBatteryShape Shape, Border Outline, ClipboardEditorActionButton Button)> shapeViews = [];
    private readonly WidgetToolButton delete;
    private WidgetLayer? layer;
    private int selectedStop;
    private bool updating, available = true, dirtyThreshold, committing;
    public WidgetHorizontalStrip Toolbar { get; }
    public WidgetBatteryTool? ActiveTool { get; private set; }
    public Func<bool>? CanInteract { get; set; }
    public bool IsBusy => colour.IsBusy;
    public event Action<WidgetLayer>? Changed;
    public event EventHandler? BusyChanged, PanelChanged, LayoutChanging, InvalidSettings, DeleteRequested, CentreRequested;
    public event Action<View>? PositionChanging;
    public event Action<WidgetMoveDirection>? NudgeRequested;
    public event Action<double>? RotationRequested;
    private bool CanUse => !updating && available && IsEnabled && !IsBusy && layer?.Battery is not null && (CanInteract?.Invoke() ?? true);
    public WidgetBatteryControls(IColourPicker picker)
    {
        AutomationId = "WidgetBatteryControls"; IsVisible = false;
        colour = new(picker, "BatteryStop", "Battery stop");
        colour.BusyChanged += (_, _) => BusyChanged?.Invoke(this, EventArgs.Empty);
        colour.ColourChanged += (_, args) =>
        {
            if (updating || layer?.Battery is not { } options || (!(CanUse && ActiveTool == WidgetBatteryTool.Colour) && !IsBusy && !committing)) return;
            var stops = options.Stops.ToArray(); stops[selectedStop] = stops[selectedStop] with { ColourHex = args.Hex };
            Publish(layer with { Battery = options with { Stops = Array.AsReadOnly(stops) } });
        };
        mode = new(["Ranges", "Smooth"], "BatteryColourMode", index =>
        {
            if (!CanUse || ActiveTool != WidgetBatteryTool.Colour || !ValidatePending()) { Refresh(); return; }
            PositionChanging?.Invoke(mode!); Edit(x => x with { ColourMode = (WidgetBatteryColourMode)index });
        });
        threshold.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(threshold, "Battery colour stop percentage");
        SemanticProperties.SetHint(threshold, "An integer from 0 to 100, greater than the preceding stop and less than the next. The first stop stays at zero.");
        threshold.TextChanged += (_, _) => { if (!updating) dirtyThreshold = true; };
        threshold.Completed += (_, _) => ValidatePending(); threshold.Unfocused += (_, _) => ValidatePending();
        error.IsVisible = false;
        add = new("Add stop", ClipboardEditorControls.Icon(StudioSymbol.Plus), "AddBatteryStop", accentCaption: true);
        remove = new("Remove stop", ClipboardEditorControls.Icon(StudioSymbol.Delete), "RemoveBatteryStop", accentCaption: true);
        add.Clicked += (_, _) =>
        {
            if (!CanUse || ActiveTool != WidgetBatteryTool.Colour || !ValidatePending() || layer?.Battery is not { } options || options.Stops.Count >= WidgetBatteries.MaximumStops) return;
            PositionChanging?.Invoke(add); var stops = options.Stops.ToList();
            var gap = Enumerable.Range(0, stops.Count).Select(i => (Index: i, End: i + 1 < stops.Count ? stops[i + 1].FromPercent : 101))
                .OrderByDescending(x => x.End - stops[x.Index].FromPercent).First();
            var point = (stops[gap.Index].FromPercent + gap.End) / 2;
            var hex = WidgetBatteries.Colour(options, new(point));
            selectedStop = gap.Index + 1; stops.Insert(selectedStop, new(point, hex)); dirtyThreshold = false;
            Edit(x => x with { Stops = Array.AsReadOnly(stops.ToArray()) });
        };
        remove.Clicked += (_, _) =>
        {
            if (!CanUse || ActiveTool != WidgetBatteryTool.Colour || !ValidatePending() || layer?.Battery is not { } options || selectedStop == 0 || options.Stops.Count <= 2) return;
            PositionChanging?.Invoke(remove); var stops = options.Stops.ToList(); stops.RemoveAt(selectedStop); selectedStop--;
            Edit(x => x with { Stops = Array.AsReadOnly(stops.ToArray()) });
        };
        for (var i = 0; i < WidgetBatteries.MaximumStops; i++)
        {
            var index = i; var swatch = SettingsControls.Rounded(new Grid(), 6); swatch.WidthRequest = swatch.HeightRequest = 24; swatch.VerticalOptions = LayoutOptions.Center;
            var label = SettingsControls.Text("", 14); label.VerticalTextAlignment = TextAlignment.Center; label.InputTransparent = true;
            var tap = SettingsControls.TapTarget("Battery colour stop", "BatteryColourStop" + i);
            var body = new Grid { ColumnSpacing = 10, Padding = new Thickness(12, 0), MinimumHeightRequest = 44, InputTransparent = true,
                ColumnDefinitions = { new(24), new(GridLength.Star) } }; body.Add(swatch); body.Add(label, 1);
            var outline = SettingsControls.Rounded(new Grid { Children = { tap, body } }, 14, 1);
            outline.WidthRequest = 120; stopViews.Add((outline, swatch, label, tap));
            tap.Clicked += (_, _) => { if (!CanUse || ActiveTool != WidgetBatteryTool.Colour || !ValidatePending()) return; PositionChanging?.Invoke(outline); selectedStop = index; dirtyThreshold = false; Refresh(); };
        }
        var shapes = new VerticalStackLayout { Spacing = 8 };
        foreach (var shape in Enum.GetValues<WidgetBatteryShape>())
        {
            var glyph = new WidgetBatteryGlyph { IsVisible = true, WidthRequest = 64, HeightRequest = 40 };
            glyph.Update(WidgetBatteries.Defaults() with { Shape = shape }, new(75));
            var button = new ClipboardEditorActionButton(shape == WidgetBatteryShape.Percentage ? "Percentage only" : shape.ToString(), glyph,
                "BatteryShape" + shape, accentCaption: true);
            var outline = SettingsControls.Rounded(button, 18, 1); shapeViews.Add((shape, outline, button)); shapes.Children.Add(outline);
            button.Clicked += (_, _) => { if (!CanUse || ActiveTool != WidgetBatteryTool.Shape || !ValidatePending()) return; PositionChanging?.Invoke(button); Edit(x => x with { Shape = shape }); };
        }
        percentage.Toggled += (_, args) => { if (CanUse && ActiveTool == WidgetBatteryTool.Shape) { PositionChanging?.Invoke(percentage); Edit(x => x with { ShowPercentage = args.Value }); } };
        charging.Toggled += (_, args) => { if (CanUse && ActiveTool == WidgetBatteryTool.Shape) { PositionChanging?.Invoke(charging); Edit(x => x with { ShowCharging = args.Value }); } };
        opacity.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); SemanticProperties.SetDescription(opacity, "Battery layer opacity");
        opacity.ValueChanged += (_, args) => { if (CanUse && ActiveTool == WidgetBatteryTool.Opacity) Publish(layer! with { Opacity = args.NewValue }); };
        centre = new("Centre horizontally", ClipboardEditorControls.Icon(StudioSymbol.CenterHorizontal), "CentreBatteryHorizontally", accentCaption: true);
        centre.Clicked += (_, _) => { if (CanUse && ActiveTool == WidgetBatteryTool.Position && layer is { Locked: false } && ValidatePending()) CentreRequested?.Invoke(this, EventArgs.Empty); };
        nudge.PositionChanging += view => PositionChanging?.Invoke(view); rotation.PositionChanging += view => PositionChanging?.Invoke(view);
        nudge.MoveRequested += direction => { if (CanUse && ActiveTool == WidgetBatteryTool.Position && layer is { Locked: false }) NudgeRequested?.Invoke(direction); };
        rotation.Changed += degrees => { if (CanUse && ActiveTool == WidgetBatteryTool.Position && layer is { Locked: false }) RotationRequested?.Invoke(degrees); };
        var panels = new VerticalStackLayout { Spacing = 10 };
        void Add(WidgetBatteryTool tool, StudioSymbol symbol, string title, params View[] controls)
        {
            var body = new VerticalStackLayout { Spacing = 12 }; body.Children.Add(SettingsControls.Text(title, 19, bold: true));
            foreach (var view in controls) body.Children.Add(view);
            var panel = ClipboardEditorControls.Surface(body, 22, 14); panel.IsVisible = false; panel.AutomationId = "BatteryPanel" + tool;
            panels.Children.Add(panel); var button = new WidgetToolButton(symbol, "Battery " + title.ToLowerInvariant(), automationId: "BatteryTool" + tool);
            button.Clicked += (_, _) => { if (!CanUse || !ValidatePending()) return; PositionChanging?.Invoke(button); SetPanel(ActiveTool == tool ? null : tool); };
            tools.Add(tool, (button, panel));
        }
        Add(WidgetBatteryTool.Shape, StudioSymbol.Battery, "Shape", shapes, SwitchRow("Show percentage", percentage), SwitchRow("Show charging status", charging),
            SettingsControls.Text("Home Screen battery readings update when iOS refreshes the widget. They may lag behind the status bar. An unavailable reading shows —.", 12, secondary: true));
        Add(WidgetBatteryTool.Colour, StudioSymbol.Palette, "Battery colours", mode, ruleHelp, new WidgetHorizontalStrip(stopViews.Select(x => (View)x.Outline), "BatteryColourStops", 8), new ActionButtonRow(add, remove), editing,
            ClipboardEditorControls.Field(threshold, new Thickness(12, 0)), error, colour);
        Add(WidgetBatteryTool.Position, StudioSymbol.CenterHorizontal, "Position", centre, nudge, rotation,
            SettingsControls.Text("Drag or pinch the preview to move and resize. More size controls are in the layer menu. Unlock the layer to change placement.", 12, secondary: true));
        Add(WidgetBatteryTool.Opacity, StudioSymbol.Opacity, "Opacity", opacityLabel, opacity);
        delete = new WidgetToolButton(StudioSymbol.Delete, "Delete battery layer", true, "BatteryToolDelete");
        delete.Clicked += (_, _) => { if (CanUse && ValidatePending()) DeleteRequested?.Invoke(this, EventArgs.Empty); };
        Toolbar = new WidgetHorizontalStrip(tools.Values.Select(x => (View)x.Button).Append(delete), "BatteryEditorToolbar") { IsVisible = false };
        Content = panels;
    }
    private static View SwitchRow(string text, Switch toggle)
    {
        SemanticProperties.SetDescription(toggle, text); var label = SettingsControls.Text(text, 14); label.VerticalOptions = LayoutOptions.Center;
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } }; row.Add(label); row.Add(toggle, 1); return row;
    }
    private void Edit(Func<WidgetBatteryOptions, WidgetBatteryOptions> transform) { if (layer?.Battery is { } options) Publish(layer with { Battery = transform(options) }); }
    private void Publish(WidgetLayer value) { layer = value; Refresh(); Changed?.Invoke(value); }
    public bool CommitPending()
    {
        if (layer?.Battery is not { } options) return true;
        var before = committing; committing = true;
        try
        {
            if (dirtyThreshold)
            {
                if (!int.TryParse(threshold.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || selectedStop == 0 && value != 0
                    || selectedStop > 0 && value <= options.Stops[selectedStop - 1].FromPercent || value > 100
                    || selectedStop + 1 < options.Stops.Count && value >= options.Stops[selectedStop + 1].FromPercent)
                {
                    error.Text = "Enter a whole percentage between the neighbouring stops (0–100)."; error.IsVisible = true; SetPanel(WidgetBatteryTool.Colour); return false;
                }
                var stops = options.Stops.ToArray(); stops[selectedStop] = stops[selectedStop] with { FromPercent = value };
                dirtyThreshold = false; Edit(x => x with { Stops = Array.AsReadOnly(stops) });
            }
            if (!colour.CommitPendingHex()) { SetPanel(WidgetBatteryTool.Colour); return false; }
            error.IsVisible = false; return true;
        }
        finally { committing = before; }
    }
    private bool ValidatePending() { if (CommitPending()) return true; InvalidSettings?.Invoke(this, EventArgs.Empty); return false; }
    private void SetPanel(WidgetBatteryTool? tool)
    {
        var changed = ActiveTool != tool; ActiveTool = tool;
        if (changed) LayoutChanging?.Invoke(this, EventArgs.Empty);
        if (tool is { } next) tools[next].Panel.IsVisible = true;
        foreach (var (key, item) in tools) { if (key != tool) item.Panel.IsVisible = false; item.Button.IsSelected = key == tool; }
        IsVisible = layer is not null && tool is not null;
        if (changed) PanelChanged?.Invoke(this, EventArgs.Empty);
    }
    public void ClosePanel() { if (!IsBusy && CommitPending()) { threshold.Unfocus(); SetPanel(null); } }
    public void ResetPressedState() { foreach (var item in tools.Values) item.Button.Scale = 1; delete.Scale = 1; }
    public void SetAvailable(bool value)
    {
        available = value; IsEnabled = Toolbar.IsEnabled = value;
        centre.IsEnabled = nudge.IsEnabled = value && layer is { Locked: false };
    }
    public void Update(WidgetLayer? value)
    {
        var next = value?.Kind == WidgetLayerKind.Battery ? value : null;
        if (next?.Id != layer?.Id) { dirtyThreshold = false; selectedStop = 0; layer = next; SetPanel(null); }
        layer = next; Toolbar.IsVisible = next is not null; IsVisible = next is not null && ActiveTool is not null; Refresh();
    }
    private void Refresh()
    {
        if (layer?.Battery is not { } options) return;
        updating = true;
        try
        {
            selectedStop = Math.Clamp(selectedStop, 0, options.Stops.Count - 1);
            mode.Update((int)options.ColourMode); percentage.IsToggled = options.Shape == WidgetBatteryShape.Percentage || options.ShowPercentage; percentage.IsEnabled = options.Shape != WidgetBatteryShape.Percentage;
            charging.IsToggled = options.ShowCharging; opacity.Value = layer.Opacity; opacityLabel.Text = $"Opacity · {layer.Opacity:P0}";
            centre.IsEnabled = nudge.IsEnabled = available && !layer.Locked; rotation.Update(layer);
            ruleHelp.Text = options.ColourMode == WidgetBatteryColourMode.Ranges ? "Each colour applies from its threshold until the next stop." : "Colours blend smoothly between the percentage stops. After the final stop, its colour stays.";
            for (var i = 0; i < stopViews.Count; i++)
            {
                var item = stopViews[i]; item.Outline.IsVisible = i < options.Stops.Count; if (i >= options.Stops.Count) continue;
                var stop = options.Stops[i]; var end = i + 1 < options.Stops.Count ? options.Stops[i + 1].FromPercent - 1 : 100;
                var caption = options.ColourMode == WidgetBatteryColourMode.Ranges ? $"{stop.FromPercent}–{end}%" : $"{stop.FromPercent}%";
                item.Label.Text = caption; item.Swatch.BackgroundColor = Color.FromArgb(stop.ColourHex);
                Outline(item.Outline, selectedStop == i); SemanticProperties.SetDescription(item.Tap, $"Battery colour {caption}, {stop.ColourHex}" + (i == selectedStop ? ", selected" : ""));
            }
            foreach (var (shape, outline, button) in shapeViews) { Outline(outline, shape == options.Shape); button.Describe("Battery shape " + shape + (shape == options.Shape ? ", selected" : "")); }
            editing.Text = $"Editing stop {selectedStop + 1} · starts at percentage";
            if (!dirtyThreshold) threshold.Text = options.Stops[selectedStop].FromPercent.ToString(CultureInfo.InvariantCulture);
            threshold.IsReadOnly = selectedStop == 0; add.IsEnabled = options.Stops.Count < WidgetBatteries.MaximumStops;
            remove.IsEnabled = options.Stops.Count > 2 && selectedStop > 0;
            if (colour.SelectedHex != options.Stops[selectedStop].ColourHex) colour.SetSelection(options.Stops[selectedStop].ColourHex);
        }
        finally { updating = false; }
    }
    private static void Outline(Border view, bool selected)
    {
        if (selected) view.SetDynamicResource(Border.StrokeProperty, "AccentText");
        else { view.RemoveDynamicResource(Border.StrokeProperty); SettingsControls.SurfaceStroke(view); }
    }
}
