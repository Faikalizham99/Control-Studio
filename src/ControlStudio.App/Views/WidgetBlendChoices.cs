using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal sealed class WidgetBlendChoice : Grid
{
    private readonly Label caption;
    private readonly Label check;
    private readonly AccentSurface selected;
    private readonly WidgetBlendGroupGlyph? groupIcon;
    private bool? marked;
    public Button Tap { get; }
    public WidgetBlendChoice(string title, string id, View picture, Action choose, bool group = false)
    {
        MinimumHeightRequest = group ? 64 : 94; MinimumWidthRequest = group ? 76 : 96;
        caption = SettingsControls.Text(title, group ? 12 : 14, bold: true);
        caption.HorizontalTextAlignment = TextAlignment.Center; caption.LineBreakMode = LineBreakMode.WordWrap; caption.InputTransparent = true;
        groupIcon = picture as WidgetBlendGroupGlyph;
        var idle = SettingsControls.Rounded(new Grid(), 16); idle.InputTransparent = true;
        selected = new AccentSurface(new Grid(), 16, captionSize: group ? 12 : 14) { IsVisible = false, InputTransparent = true };
        picture.HorizontalOptions = LayoutOptions.Center;
        var body = new VerticalStackLayout { Spacing = group ? 4 : 6, Padding = 8, InputTransparent = true, Children = { picture, caption } };
        check = new Label { Text = "✓", FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start, Margin = 5, InputTransparent = true, IsVisible = false };
        selected.BindForeground(check, Label.TextColorProperty);
        Tap = SettingsControls.TapTarget(title, id); Tap.CornerRadius = 16;
        Tap.Clicked += (_, _) => { if (IsEnabled) choose(); };
        Tap.Pressed += (_, _) => { if (IsEnabled) Scale = .97; }; Tap.Released += (_, _) => Scale = 1;
        Children.Add(Tap); Children.Add(idle); Children.Add(selected); Children.Add(body); Children.Add(check);
        AutomationProperties.SetIsInAccessibleTree(body, false); AutomationProperties.SetIsInAccessibleTree(check, false);
        MarkSelected(false);
    }
    public void MarkSelected(bool value)
    {
        Scale = 1;
        if (marked == value) return;
        marked = value; selected.IsVisible = check.IsVisible = value;
        caption.RemoveBinding(Label.TextColorProperty); caption.RemoveDynamicResource(Label.TextColorProperty);
        if (value) selected.BindForeground(caption, Label.TextColorProperty);
        else caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#252A34"), Color.FromArgb("#F5F5F7"));
        if (groupIcon is not null)
        {
            groupIcon.RemoveBinding(WidgetBlendGroupGlyph.TintProperty); groupIcon.RemoveDynamicResource(WidgetBlendGroupGlyph.TintProperty);
            if (value) selected.BindForeground(groupIcon, WidgetBlendGroupGlyph.TintProperty);
            else groupIcon.SetDynamicResource(WidgetBlendGroupGlyph.TintProperty, "AccentText");
        }
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName); if (propertyName == nameof(IsEnabled)) Scale = 1;
    }
}

internal sealed class WidgetBlendGroups : ContentView
{
    private readonly List<WidgetBlendChoice> cells = [];
    private readonly WidgetHorizontalStrip strip;
    public int SelectedIndex { get; private set; }
    public WidgetBlendGroups(Action<int> select)
    {
        for (var i = 0; i < WidgetBlending.Groups.Count; i++)
        {
            var index = i;
            var cell = new WidgetBlendChoice(WidgetBlending.Groups[i], "WidgetBlendGroup" + i, new WidgetBlendGroupGlyph(i), () =>
            {
                if (!IsEnabled || SelectedIndex == index) return;
                Update(index); select(index);
            }, group: true);
            cells.Add(cell);
        }
        strip = new WidgetHorizontalStrip(cells, "WidgetBlendGroupStrip"); Content = strip; Update(0);
    }
    public void Update(int index)
    {
        if (index < 0 || index >= cells.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
        for (var i = 0; i < cells.Count; i++)
        {
            cells[i].MarkSelected(i == index);
            SemanticProperties.SetDescription(cells[i].Tap, WidgetBlending.Groups[i] + " group" + (i == index ? ", selected" : ""));
        }
    }
}
