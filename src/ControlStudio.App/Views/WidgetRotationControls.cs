using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal sealed class WidgetRotationControls : ContentView
{
    private readonly Slider angle;
    private readonly Label value = SettingsControls.Text("", 14, bold: true);
    private readonly ClipboardEditorActionButton reset;
    private bool updating;
    public event Action<double>? Changed;
    public event Action<View>? PositionChanging;
    public WidgetRotationControls(string prefix)
    {
        AutomationId = prefix;
        angle = new Slider { Minimum = -180, Maximum = 180, MinimumHeightRequest = 44, AutomationId = prefix + "Angle" };
        angle.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText");
        SemanticProperties.SetDescription(angle, "Selected layer rotation in degrees");
        angle.ValueChanged += (_, args) =>
        {
            if (updating || !IsEnabled) return;
            PositionChanging?.Invoke(angle); Changed?.Invoke(Math.Round(args.NewValue, 1));
        };
        reset = new("Reset to 0°", ClipboardEditorControls.Icon(StudioSymbol.RotateLeft, size: 18), prefix + "Reset", accentCaption: true);
        reset.Clicked += (_, _) => { if (IsEnabled) { PositionChanging?.Invoke(reset); Changed?.Invoke(0); } };
        Content = new VerticalStackLayout { Spacing = 8, Children = { value, angle, reset } };
        Update(null);
    }
    public void Update(WidgetLayer? layer)
    {
        updating = true;
        try
        {
            IsEnabled = layer is { Locked: false }; angle.Value = layer?.RotationDegrees ?? 0;
            value.Text = $"Rotation · {angle.Value:0.#}°"; reset.IsEnabled = angle.Value != 0;
            SemanticProperties.SetHint(angle, $"{angle.Value:0.#} degrees. The layer rotates around its centre.");
        }
        finally { updating = false; }
    }
}
