namespace ControlStudio.App.Views;

/// <summary>A true disabled hit target beneath independently drawn, readable visuals.</summary>
internal sealed class PageActionButton : ContentView
{
    private readonly Button tap;
    private readonly SecondaryActionSurface surface;
    private readonly bool destructive;
    public static readonly BindableProperty DisabledReasonProperty = BindableProperty.Create(nameof(DisabledReason), typeof(string), typeof(PageActionButton), "",
        propertyChanged: (view, _, _) => ((PageActionButton)view).UpdateHint());
    public string DisabledReason { get => (string)GetValue(DisabledReasonProperty); set => SetValue(DisabledReasonProperty, value); }
    public event EventHandler? Clicked;

    public PageActionButton(string text, string automationId, ProfileSymbol? symbol = null, bool destructive = false)
    {
        this.destructive = destructive;
        MinimumHeightRequest = MinimumWidthRequest = 44;
        Label? label = null; ProfileGlyph? icon = null;
        if (symbol is { } value) icon = new ProfileGlyph(value);
        if (text.Length > 0)
        {
            label = new Label { Text = text, FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        }
        var caption = ActionCaption.Create(label, icon, 6);
        surface = new SecondaryActionSurface(caption, 22, text.Length > 0 ? new Thickness(14, 8) : 10, destructive: destructive);
        if (label is not null) surface.BindForeground(label, Label.TextColorProperty);
        if (icon is not null) surface.BindForeground(icon, ProfileGlyph.TintProperty);
        tap = SettingsControls.TapTarget(text.Length > 0 ? text : "Back", automationId); tap.CornerRadius = 22;
        tap.Clicked += (_, args) => { if (IsEnabled) Clicked?.Invoke(this, args); };
        tap.Pressed += (_, _) => { if (IsEnabled) surface.Scale = .97; };
        tap.Released += (_, _) => surface.Scale = 1;
        Content = new Grid { MinimumWidthRequest = 44, MinimumHeightRequest = 44, Children = { tap, surface } };
        UpdateHint();
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) { if (surface is not null) surface.Scale = 1; UpdateHint(); }
    }
    private void UpdateHint() { if (tap is not null) SemanticProperties.SetHint(tap, IsEnabled ? (destructive ? "Asks for confirmation." : "") : DisabledReason); }
}

internal static class PageActionBar
{
    public static View Clipboard(PageActionButton back, PageActionButton arrange, PageActionButton add)
    {
        var title = Title("Clipboard");
        var row = Row(); row.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) };
        row.Add(back); row.Add(title, 1); row.Add(arrange, 2); row.Add(add, 3); return row;
    }

    public static View Modal(string text, PageActionButton cancel, PageActionButton done)
    {
        var title = Title(text); title.HorizontalTextAlignment = TextAlignment.Center;
        var row = Row(); row.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) };
        row.Add(cancel); row.Add(title, 1); row.Add(done, 2); return row;
    }

    private static Label Title(string text)
    {
        var title = SettingsControls.Text(text, 16, bold: true);
        title.MaxLines = 1; title.LineBreakMode = LineBreakMode.TailTruncation; title.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        return title;
    }
    private static Grid Row()
    {
        var row = new Grid { Padding = new Thickness(16, 6), ColumnSpacing = ActionSpacing.Gap };
        row.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Colors.Black);
        return row;
    }
}
