using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>A persistent, accessible audition panel; browsing groups does not edit the layer.</summary>
internal sealed class WidgetBlendPanel : ContentView
{
    private readonly WidgetBlendGroups groups;
    private readonly Label summary = SettingsControls.Text("", 14, bold: true);
    private readonly Label notice = SettingsControls.Text("", 12, secondary: true);
    private readonly Grid modes = new() { ColumnSpacing = ActionSpacing.Gap, RowSpacing = ActionSpacing.Gap,
        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
    private readonly List<(WidgetBlendMode Mode, WidgetBlendChoice Cell, WidgetBlendThumbnail Picture)> choices = [];
    private WidgetLayer? layer;
    public event EventHandler? LayoutChanging;
    public WidgetBlendPanel(Action<WidgetBlendMode> choose, Func<WidgetBlendMode, ImageSource?>? sampleSource = null)
    {
        AutomationId = "WidgetBlendPanel"; IsVisible = false;
        groups = new WidgetBlendGroups(ShowGroup);
        foreach (var mode in WidgetBlending.Modes)
        {
            var title = WidgetBlending.Name(mode);
            var picture = new WidgetBlendThumbnail(mode, sampleSource);
            var cell = new WidgetBlendChoice(title, "WidgetBlendMode" + mode, picture, () =>
            {
                if (IsEnabled && IsVisible && layer is not null && groups.SelectedIndex == WidgetBlending.Group(mode) && layer.BlendMode != mode) choose(mode);
            });
            choices.Add((mode, cell, picture));
        }
        var done = new ClipboardEditorActionButton("Done", ClipboardEditorControls.Icon(StudioSymbol.Check), "CloseWidgetBlendPanel");
        done.Clicked += (_, _) => { if (IsEnabled && IsVisible) { LayoutChanging?.Invoke(this, EventArgs.Empty); IsVisible = false; } };
        Content = new VerticalStackLayout { Spacing = 10, Children = { summary,
            SettingsControls.Text("Blue/orange examples show each effect. Photo layers blend together until a clock layer separates them. Clock text and wallpaper are not included in photo blending.", 12, secondary: true),
            notice, groups, modes, done } };
        PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IsVisible)) RefreshPictures(); };
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
            var selected = choice.Mode == value.BlendMode; choice.Cell.MarkSelected(selected);
            SemanticProperties.SetDescription(choice.Cell.Tap, WidgetBlending.Name(choice.Mode) + (selected ? ", selected" : "") + ", " + value.Name);
        }
    }
    private void ShowGroup(int index)
    {
        LayoutChanging?.Invoke(this, EventArgs.Empty);
        foreach (var choice in choices) choice.Picture.SetActive(false);
        modes.Children.Clear(); modes.RowDefinitions.Clear();
        var items = choices.Where(x => WidgetBlending.Group(x.Mode) == index).ToArray();
        for (var i = 0; i < items.Length; i++)
        {
            if (i % 2 == 0) modes.RowDefinitions.Add(new(GridLength.Auto));
            modes.Add(items[i].Cell, i % 2, i / 2);
        }
        RefreshPictures();
    }
    private void RefreshPictures()
    {
        foreach (var choice in choices) choice.Picture.SetActive(IsVisible && layer is not null && WidgetBlending.Group(choice.Mode) == groups.SelectedIndex);
    }
}
