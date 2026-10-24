using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>A persistent, accessible audition panel; browsing groups does not edit the layer.</summary>
internal sealed class WidgetBlendPanel : ContentView
{
    private readonly WidgetSegments groups;
    private readonly Label summary = SettingsControls.Text("", 14, bold: true);
    private readonly Label notice = SettingsControls.Text("", 12, secondary: true);
    private readonly Grid modes = new() { ColumnSpacing = ActionSpacing.Gap, RowSpacing = ActionSpacing.Gap,
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
    private readonly List<(WidgetBlendMode Mode, Grid Cell, Button Tap, AccentSurface Accent)> choices = [];
    private WidgetLayer? layer;
    public WidgetBlendPanel(Action<WidgetBlendMode> choose)
    {
        AutomationId = "WidgetBlendPanel"; IsVisible = false;
        groups = new WidgetSegments(WidgetBlending.Groups, "WidgetBlendGroup", ShowGroup);
        foreach (var mode in WidgetBlending.Modes)
        {
            var title = WidgetBlending.Name(mode);
            var idle = SettingsControls.Text(title, 14); idle.InputTransparent = true;
            idle.HorizontalTextAlignment = idle.VerticalTextAlignment = TextAlignment.Center;
            var selected = new Label { Text = title + " ✓", FontSize = 14, FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, InputTransparent = true };
            var accent = new AccentSurface(selected, 16, new Thickness(8), captionSize: 14);
            accent.BindForeground(selected, Label.TextColorProperty); accent.InputTransparent = true;
            var tap = SettingsControls.TapTarget(title, "WidgetBlendMode" + mode);
            tap.Clicked += (_, _) => { if (IsEnabled && layer is not null && layer.BlendMode != mode) choose(mode); };
            var cell = new Grid { MinimumHeightRequest = 48, Children = { tap, idle, accent } };
            AutomationProperties.SetIsInAccessibleTree(idle, false); AutomationProperties.SetIsInAccessibleTree(selected, false);
            choices.Add((mode, cell, tap, accent));
        }
        var done = new ClipboardEditorActionButton("Done", ClipboardEditorControls.Icon(StudioSymbol.Check), "CloseWidgetBlendPanel");
        done.Clicked += (_, _) => IsVisible = false;
        Content = new VerticalStackLayout { Spacing = 10, Children = { summary,
            SettingsControls.Text("Blend with layers underneath or a solid background. The checkerboard and Home Screen wallpaper are not part of the blend.", 12, secondary: true),
            notice, groups, modes, done } };
        ShowGroup(0);
    }
    public void Show(WidgetLayer value) { Update(value); groups.Update(WidgetBlending.Group(value.BlendMode)); ShowGroup(groups.SelectedIndex); IsVisible = true; }
    public void Update(WidgetLayer? value)
    {
        layer = value;
        if (value is null) { IsVisible = false; return; }
        summary.Text = value.Name + " · " + WidgetBlending.Name(value.BlendMode);
        notice.Text = !value.Visible ? "This layer is hidden. Show it in Layers to preview blending."
            : value.Opacity == 0 ? "This layer has 0% opacity. Increase its opacity to preview blending." : "";
        notice.IsVisible = notice.Text.Length > 0;
        foreach (var choice in choices)
        {
            var selected = choice.Mode == value.BlendMode; choice.Accent.IsVisible = selected;
            SemanticProperties.SetDescription(choice.Tap, WidgetBlending.Name(choice.Mode) + (selected ? ", selected" : "") + ", " + value.Name);
        }
    }
    private void ShowGroup(int index)
    {
        modes.Children.Clear(); modes.RowDefinitions.Clear();
        var items = choices.Where(x => WidgetBlending.Group(x.Mode) == index).ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            if (i % 2 == 0) modes.RowDefinitions.Add(new(GridLength.Auto));
            modes.Add(items[i].Cell, i % 2, i / 2);
        }
    }
}
