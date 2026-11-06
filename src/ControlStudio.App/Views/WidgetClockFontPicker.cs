using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Readable font specimens with one accessible native target per face.</summary>
internal sealed class WidgetClockFontPicker : ContentView
{
    private readonly List<(WidgetClockFont Font, Button Tap, View Idle, View Selected, VerticalStackLayout Row, WidgetClockFontLabel[] Samples, WidgetClockFontLabel[] Previews)> choices = [];
    private readonly Dictionary<WidgetClockFont, WidgetClockWeightPicker> weights = [];
    private readonly Action<WidgetClockFont> select;
    private readonly Action<WidgetClockWeight>? selectWeight;
    private WidgetClockWeight? selectedWeight;
    private bool built;
    public WidgetClockFont SelectedFont { get; private set; }
    public event Action<View>? PositionChanging;
    private string sampleText = "12:34:56";
    public WidgetClockFontPicker(Action<WidgetClockFont> select, Action<WidgetClockWeight>? selectWeight = null, bool defer = false)
    {
        this.select = select; this.selectWeight = selectWeight; AutomationId = "ClockFontPicker";
        if (!defer) EnsureLoaded();
    }
    internal void EnsureLoaded()
    {
        if (built) return;
        var rows = new VerticalStackLayout { Spacing = ActionSpacing.Gap };
        foreach (var font in WidgetClockFonts.All)
        {
            var title = WidgetClockFonts.Title(font);
            VerticalStackLayout Sample() => new() { Spacing = 4, InputTransparent = true, Children =
            {
                new WidgetClockFontLabel { Text = title, Typeface = new(Font: font), FontSize = 16, MaxLines = 2 },
                new WidgetClockFontLabel { Text = sampleText, Typeface = new(Font: font), FontSize = 26, MaxLines = 1, FontAutoScalingEnabled = false }
            } };
            var idleBody = Sample(); var selectedBody = Sample();
            var idle = new SecondaryActionSurface(idleBody, 16, new Thickness(14, 10));
            var selected = new AccentSurface(selectedBody, 16, new Thickness(14, 10), captionSize: 16, captionBold: false) { InputTransparent = true };
            foreach (var label in idleBody.Children.OfType<Label>()) { idle.BindForeground(label, Label.TextColorProperty, accentWhenEnabled: false); AutomationProperties.SetIsInAccessibleTree(label, false); }
            foreach (var label in selectedBody.Children.OfType<Label>()) { selected.BindForeground(label, Label.TextColorProperty); AutomationProperties.SetIsInAccessibleTree(label, false); }
            var tap = SettingsControls.TapTarget(title, "ClockFont" + (int)font);
            tap.MinimumHeightRequest = 48;
            SemanticProperties.SetHint(tap, "Apply this font to the text layer. Sample: " + sampleText + ".");
            var row = new Grid { MinimumHeightRequest = 48, Children = { tap, idle, selected } };
            tap.Clicked += (_, _) => { if (IsEnabled && SelectedFont != font) { PositionChanging?.Invoke(row); select(font); } };
            var container = new VerticalStackLayout { Spacing = 6, Children = { row } };
            choices.Add((font, tap, idle, selected, container, idleBody.Children.Concat(selectedBody.Children).OfType<WidgetClockFontLabel>().ToArray(),
                [ (WidgetClockFontLabel)idleBody.Children[1], (WidgetClockFontLabel)selectedBody.Children[1] ]));
            rows.Children.Add(container);
        }
        Content = rows; built = true; RefreshSelection();
    }
    public void Update(WidgetClockFont font, WidgetClockWeight? weight = null)
    {
        if (!Enum.IsDefined(font)) throw new ArgumentOutOfRangeException(nameof(font));
        if (SelectedFont == font && selectedWeight == weight) return;
        SelectedFont = font; selectedWeight = weight;
        if (built) RefreshSelection();
    }
    private void RefreshSelection()
    {
        foreach (var choice in choices)
        {
            var selected = choice.Font == SelectedFont;
            choice.Selected.IsVisible = selected; choice.Idle.IsVisible = !selected;
            if (selected && !weights.ContainsKey(choice.Font))
            {
                var font = choice.Font;
                var picker = new WidgetClockWeightPicker(font, weight => { if (IsEnabled && SelectedFont == font) selectWeight?.Invoke(weight); });
                picker.PositionChanging += view => PositionChanging?.Invoke(view);
                weights.Add(font, picker); choice.Row.Children.Add(picker);
            }
            var typeface = new WidgetClockOptions(Font: choice.Font, Weight: selected ? selectedWeight : null);
            foreach (var sample in choice.Samples) sample.Typeface = typeface;
            if (weights.TryGetValue(choice.Font, out var weightPicker))
            {
                weightPicker.IsVisible = selected;
                if (selected) weightPicker.Update(WidgetClockWeights.Resolve(typeface));
            }
            SemanticProperties.SetDescription(choice.Tap, WidgetClockFonts.Title(choice.Font) + (selected ? ", selected" : ""));
        }
    }
    public void SetSample(string text)
    {
        if (sampleText == text) return;
        foreach (var choice in choices)
        {
            foreach (var label in choice.Previews) label.Text = text;
            SemanticProperties.SetHint(choice.Tap, "Apply this font to the text layer. Sample: " + text + ".");
        }
        sampleText = text;
    }
}
