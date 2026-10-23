using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Reuses full saved composites with a page-wide cap on live image sources.</summary>
internal sealed class WidgetSlotPreviewWindow
{
    private readonly ScrollView scroll;
    private readonly VisualElement body;
    private readonly List<(VisualElement Frame, WidgetRenderPreview Image)> previews = [];
    private bool active, updating;
    public WidgetSlotPreviewWindow(ScrollView scroll, VisualElement body)
    {
        this.scroll = scroll; this.body = body;
        scroll.Scrolled += (_, _) => Update(); scroll.SizeChanged += Changed; body.SizeChanged += Changed;
    }
    public void Add(VisualElement frame, WidgetRenderPreview image) { previews.Add((frame, image)); frame.SizeChanged += Changed; }
    private void Changed(object? sender, EventArgs args) => Update();
    public void Activate(bool value) { active = value; Update(); }
    public void Clear()
    {
        foreach (var entry in previews) { entry.Frame.SizeChanged -= Changed; entry.Image.Dispose(); }
        previews.Clear();
    }
    private Rect Bounds(VisualElement frame)
    {
        double top = 0; VisualElement? current = frame;
        while (current is not null && current != body) { top += current.Y; current = current.Parent as VisualElement; }
        return current == body ? new Rect(0, top, frame.Width, frame.Height) : Rect.Zero;
    }
    public void Update()
    {
        if (updating) return;
        updating = true;
        try
        {
            var visible = active ? WidgetPreviewWindow.Select(previews.Select(x => Bounds(x.Frame)).ToArray(), scroll.ScrollY, scroll.Height) : new HashSet<int>();
            for (var i = 0; i < previews.Count; i++) if (!visible.Contains(i)) previews[i].Image.SetVisible(false);
            foreach (var i in visible) previews[i].Image.SetVisible(true);
        }
        finally { updating = false; }
    }
}

internal static class WidgetSlotCards
{
    public static WidgetPreviewFrame Preview(WidgetSize size, WidgetDesign? design, string? path, WidgetSlotPreviewWindow window, double? width = null)
    {
        var content = new Grid(); var background = design is null ? Colors.Transparent : Color.FromArgb(design.BackgroundHex);
        if (design?.TransparentBackground == true) { content.Children.Add(new WidgetCheckerboard()); background = Colors.Transparent; }
        WidgetRenderPreview? image = null;
        if (design is not null)
        {
            image = new WidgetRenderPreview(path, Color.FromArgb(design.BackgroundHex));
            if (path is null && image.Content is Label missing)
            {
                missing.Text = "No preview"; missing.FontSize = 11; missing.Margin = 2;
                missing.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#515A6B"), Color.FromArgb("#BCC4D2"));
                image.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#E8ECF2"), Color.FromArgb("#292D36"));
            }
            content.Children.Add(image);
        }
        else content.Children.Add(new StudioGlyph(StudioSymbol.Plus) { WidthRequest = 28, HeightRequest = 28, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center });
        var frame = new WidgetPreviewFrame(size, content, background, initialWidth: width ?? 140);
        if (design is null && frame.Content is Border outline) outline.StrokeDashArray = new DoubleCollection { 5, 3 };
        if (width is { } value) frame.WidthRequest = value;
        frame.InputTransparent = true; AutomationProperties.SetIsInAccessibleTree(frame, false);
        if (image is not null) window.Add(frame, image);
        return frame;
    }
    public static View SizeBadge(WidgetSize size)
    {
        var label = SettingsControls.Text(size == WidgetSize.ExtraLargePortrait ? "XL" : size.ToString(), 11);
        var badge = new AccentSurface(label, 10, new Thickness(8, 3), captionSize: 11, captionBold: false) { HorizontalOptions = LayoutOptions.Start };
        badge.BindForeground(label, Label.TextColorProperty); return badge;
    }
}

internal sealed class WidgetDesignChoiceCard : ContentView
{
    private readonly Border surface;
    private readonly View mark;
    private readonly Button tap;
    private readonly string name;
    public WidgetDesignChoiceCard(WidgetDesign design, View preview, Action select, bool hasPreview = true)
    {
        name = design.Name;
        var title = SettingsControls.Text(name, 15, bold: true); title.MaxLines = 2; title.LineBreakMode = LineBreakMode.TailTruncation;
        var check = ClipboardEditorControls.Icon(StudioSymbol.Check, size: 18);
        var selected = new AccentSurface(check, 14, captionSize: 14) { WidthRequest = 28, HeightRequest = 28, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = 5 };
        selected.BindForeground(check, StudioGlyph.TintProperty); mark = selected;
        var art = new Grid { Children = { preview, mark } };
        var visual = new VerticalStackLayout { Spacing = 7, Children = { art, title, WidgetSlotCards.SizeBadge(design.Size) }, InputTransparent = true };
        if (!hasPreview) visual.Children.Add(SettingsControls.Text("Preview unavailable. Open Edit and save again.", 12, secondary: true));
        surface = WidgetStudioStyle.Card(visual, 8, 18); surface.InputTransparent = true;
        tap = SettingsControls.TapTarget("Select " + name, "ChooseWidgetDesign" + design.Id.ToString("N"));
        tap.Clicked += (_, _) => { if (IsEnabled) select(); };
        Content = new Grid { Children = { tap, surface } }; Update(false);
        AutomationProperties.SetIsInAccessibleTree(visual, false);
    }
    public void Update(bool selected)
    {
        mark.IsVisible = selected; surface.StrokeThickness = selected ? 2 : 1;
        if (selected) surface.SetDynamicResource(Border.StrokeProperty, "AccentText");
        else { surface.RemoveDynamicResource(Border.StrokeProperty); SettingsControls.SurfaceStroke(surface); }
        SemanticProperties.SetDescription(tap, name + (selected ? ", selected" : ", not selected"));
        SemanticProperties.SetHint(tap, "Selects this design. Use the Assign button to save the slot.");
    }
}
