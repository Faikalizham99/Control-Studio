using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Only real, available weights are offered for the selected typeface.</summary>
internal sealed class WidgetClockWeightPicker : ContentView
{
    private readonly WidgetClockFont font;
    private readonly List<(WidgetClockWeight Weight, View Cell, Button Tap, View Idle, View Selected)> choices = [];
    private readonly Label caption = SettingsControls.Text("Weight", 13, bold: true);
    public WidgetClockWeight SelectedWeight { get; private set; }
    public WidgetClockWeightPicker(WidgetClockFont font, Action<WidgetClockWeight> select)
    {
        this.font = font; AutomationId = "ClockWeights" + (int)font;
        var weights = WidgetClockWeights.Available(font);
        if (weights.Count == 1)
        {
            Content = SettingsControls.Text("Weight: " + weights[0] + " · fixed for this font", 12, secondary: true);
            SelectedWeight = weights[0]; return;
        }
        foreach (var weight in weights)
        {
            WidgetClockFontLabel Caption() => new() { Text = weight.ToString(), Typeface = new(Font: font, Weight: weight), FontSize = 14, MaxLines = 2,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, InputTransparent = true };
            var idleCaption = Caption(); var activeCaption = Caption();
            var idle = new SecondaryActionSurface(idleCaption, 17, new Thickness(8, 7)); idle.BindForeground(idleCaption, Label.TextColorProperty, accentWhenEnabled: false);
            var active = new AccentSurface(activeCaption, 17, new Thickness(8, 7), captionSize: 14, captionBold: false) { InputTransparent = true };
            active.BindForeground(activeCaption, Label.TextColorProperty);
            AutomationProperties.SetIsInAccessibleTree(idleCaption, false); AutomationProperties.SetIsInAccessibleTree(activeCaption, false);
            var tap = SettingsControls.TapTarget(weight + " clock weight", "ClockWeight" + (int)font + weight);
            tap.MinimumHeightRequest = 48;
            tap.Clicked += (_, _) => { if (IsEnabled && SelectedWeight != weight) select(weight); };
            var cell = new Grid { MinimumHeightRequest = 48, MinimumWidthRequest = 100, Children = { tap, idle, active } };
            choices.Add((weight, cell, tap, idle, active));
        }
        var strip = new WidgetHorizontalStrip(choices.Select(x => x.Cell), "ClockWeightOptions" + (int)font, spacing: ActionSpacing.Gap);
        Content = new VerticalStackLayout { Spacing = 6, Children = { caption, strip } };
        Update(WidgetClockWeights.Default(font));
    }
    public void Update(WidgetClockWeight weight)
    {
        if (!WidgetClockWeights.Available(font).Contains(weight)) throw new ArgumentOutOfRangeException(nameof(weight));
        SelectedWeight = weight;
        caption.Text = "Weight · " + weight;
        foreach (var choice in choices)
        {
            var selected = choice.Weight == weight;
            choice.Selected.IsVisible = selected; choice.Idle.IsVisible = !selected;
            SemanticProperties.SetDescription(choice.Tap, WidgetClockFonts.Title(font) + ", " + choice.Weight + (selected ? ", selected" : ""));
        }
    }
}
