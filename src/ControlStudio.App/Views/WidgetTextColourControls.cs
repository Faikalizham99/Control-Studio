using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>One shared picker edits the solid colour or one of three retained gradient stops.</summary>
internal sealed class WidgetTextColourControls : ContentView
{
    private readonly ColourPickerView picker;
    private readonly WidgetSegments mode, direction;
    private readonly VerticalStackLayout gradientBody;
    private readonly Border ramp = SettingsControls.Rounded(new Grid(), 8);
    private readonly List<(Border Outline, Border Swatch, ClipboardEditorActionButton Button)> stops = [];
    private readonly Label editing = SettingsControls.Text("Solid colour", 14, bold: true);
    private WidgetClockOptions style = new();
    private Guid? layerId;
    private int selectedStop;
    private bool updating, committing;
    public Func<bool>? CanInteract { get; set; }
    public bool IsBusy => picker.IsBusy;
    public event Action<WidgetClockOptions>? StyleChanged;
    public event EventHandler? BusyChanged;
    public event EventHandler? LayoutChanging;
    public event Action<View>? PositionChanging;
    private bool CanUse => IsEnabled && !IsBusy && (CanInteract?.Invoke() ?? true);
    public WidgetTextColourControls(IColourPicker source)
    {
        AutomationId = "WidgetTextColourControls";
        picker = new(source, "ClockText", "Layer text");
        mode = new(["Solid", "Gradient"], "TextFillMode", SelectMode);
        direction = new(["Horizontal", "Vertical", "Diagonal"], "TextGradientDirection", index =>
        {
            if (!CanUse || !CommitPendingHex() || style.Gradient is not { Enabled: true } gradient) { Refresh(); return; }
            PositionChanging?.Invoke(direction!); Publish(style with { Gradient = gradient with { Direction = (WidgetGradientDirection)index } });
        });
        var row = new Grid { ColumnSpacing = 8 };
        for (var index = 0; index < 3; index++)
        {
            row.ColumnDefinitions.Add(new(GridLength.Star)); var stop = index;
            var swatch = SettingsControls.Rounded(new Grid(), 5); swatch.WidthRequest = swatch.HeightRequest = 18;
            var button = new ClipboardEditorActionButton((index + 1).ToString(), swatch, "TextGradientStop" + index, accentCaption: true, compact: true);
            var outline = SettingsControls.Rounded(button, 22, 1); row.Add(outline, index); stops.Add((outline, swatch, button));
            button.Clicked += (_, _) =>
            {
                if (!CanUse || style.Gradient is not { Enabled: true } || !CommitPendingHex()) return;
                PositionChanging?.Invoke(button); selectedStop = stop; Refresh();
            };
        }
        ramp.HeightRequest = 16; ramp.InputTransparent = true;
        gradientBody = new() { Spacing = 10, IsVisible = false, Children = { SettingsControls.Text("Gradient colours", 14, bold: true), row,
            SettingsControls.Text("Direction", 14, bold: true), direction, ramp } };
        picker.ColourChanged += (_, args) =>
        {
            if (updating || (!CanUse && !IsBusy && !committing)) return;
            Publish(style.Gradient is { Enabled: true } gradient
                ? style with { Gradient = WidgetGradients.WithColour(gradient, selectedStop, args.Hex) }
                : style with { ColourHex = args.Hex });
        };
        picker.BusyChanged += (_, _) => BusyChanged?.Invoke(this, EventArgs.Empty);
        Content = new VerticalStackLayout { Spacing = 12, Children = { mode, gradientBody, editing, picker } };
        Refresh();
    }
    private void SelectMode(int index)
    {
        if (!CanUse || !CommitPendingHex()) { Refresh(); return; }
        PositionChanging?.Invoke(mode); LayoutChanging?.Invoke(this, EventArgs.Empty);
        var gradient = style.Gradient ?? new WidgetGradientOptions(StartHex: style.ColourHex);
        Publish(style with { Gradient = gradient with { Enabled = index == 1 } });
    }
    public bool CommitPendingHex()
    {
        var previous = committing; committing = true;
        try { return picker.CommitPendingHex(); }
        finally { committing = previous; }
    }
    private void Publish(WidgetClockOptions value) { style = value; Refresh(); StyleChanged?.Invoke(value); }
    public void Update(WidgetClockOptions value, Guid id)
    {
        if (layerId != id) selectedStop = 0;
        layerId = id; style = value; Refresh();
    }
    private void Refresh()
    {
        updating = true;
        try
        {
            var active = style.Gradient is { Enabled: true }; mode.Update(active ? 1 : 0); gradientBody.IsVisible = active;
            var gradient = style.Gradient ?? new WidgetGradientOptions(StartHex: style.ColourHex); var colours = WidgetGradients.Colours(gradient);
            direction.Update((int)gradient.Direction);
            for (var i = 0; i < stops.Count; i++)
            {
                stops[i].Swatch.BackgroundColor = Color.FromArgb(colours[i]);
                if (i == selectedStop) stops[i].Outline.SetDynamicResource(Border.StrokeProperty, "AccentText");
                else { stops[i].Outline.RemoveDynamicResource(Border.StrokeProperty); SettingsControls.SurfaceStroke(stops[i].Outline); }
                stops[i].Button.Describe($"Gradient colour {i + 1}, {colours[i]}" + (i == selectedStop ? ", selected" : ""));
            }
            var points = WidgetGradients.Points(gradient.Direction);
            ramp.Background = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(colours[0]), 0), new(Color.FromArgb(colours[1]), .5f), new(Color.FromArgb(colours[2]), 1) },
                new Point(points.Start.X, points.Start.Y), new Point(points.End.X, points.End.Y));
            SemanticProperties.SetDescription(ramp, $"{gradient.Direction} gradient: {string.Join(", ", colours)}");
            editing.Text = active ? $"Editing colour {selectedStop + 1}" : "Solid colour";
            var hex = active ? colours[selectedStop] : style.ColourHex;
            if (picker.SelectedHex != hex) picker.SetSelection(hex);
        }
        finally { updating = false; }
    }
}
