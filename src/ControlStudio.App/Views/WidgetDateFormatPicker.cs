using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal sealed class WidgetDateFormatPicker : ContentView
{
    private readonly List<(WidgetDateFormat Format, View Idle, View Selected, Label[] Samples, Button Tap)> choices = [];
    public event Action<View>? PositionChanging;
    public WidgetDateFormat SelectedFormat { get; private set; }
    public WidgetDateFormatPicker(Action<WidgetDateFormat> select)
    {
        AutomationId = "WidgetDateFormats";
        var rows = new VerticalStackLayout { Spacing = ActionSpacing.Gap };
        foreach (var format in WidgetDates.Formats)
        {
            VerticalStackLayout Body() => new() { Spacing = 3, InputTransparent = true, Children =
            { SettingsControls.Text(WidgetDates.Title(format), 14), SettingsControls.Text("", 18, bold: true) } };
            var idleBody = Body(); var activeBody = Body();
            var idle = new SecondaryActionSurface(idleBody, 16, new Thickness(14, 10));
            var active = new AccentSurface(activeBody, 16, new Thickness(14, 10), captionSize: 14) { InputTransparent = true };
            foreach (var label in idleBody.Children.OfType<Label>()) { idle.BindForeground(label, Label.TextColorProperty, accentWhenEnabled: false); AutomationProperties.SetIsInAccessibleTree(label, false); }
            foreach (var label in activeBody.Children.OfType<Label>()) { active.BindForeground(label, Label.TextColorProperty); AutomationProperties.SetIsInAccessibleTree(label, false); }
            var tap = SettingsControls.TapTarget(WidgetDates.Title(format), "DateFormat" + (int)format);
            var row = new Grid { MinimumHeightRequest = 48, Children = { tap, idle, active } };
            tap.Clicked += (_, _) => { if (IsEnabled && SelectedFormat != format) { PositionChanging?.Invoke(row); select(format); } };
            choices.Add((format, idle, active, [idleBody.Children.OfType<Label>().Last(), activeBody.Children.OfType<Label>().Last()], tap));
            rows.Children.Add(row);
        }
        Content = rows; Update(new());
    }
    public void Update(WidgetDateOptions options)
    {
        SelectedFormat = options.Format;
        var now = DateTimeOffset.Now;
        foreach (var choice in choices)
        {
            var selected = choice.Format == options.Format; var sample = WidgetDates.Text(WidgetDates.ChangeFormat(options, choice.Format), now);
            choice.Selected.IsVisible = selected; choice.Idle.IsVisible = !selected;
            foreach (var label in choice.Samples) label.Text = sample;
            SemanticProperties.SetDescription(choice.Tap, WidgetDates.Title(choice.Format) + ", " + sample + (selected ? ", selected" : ""));
        }
    }
}
