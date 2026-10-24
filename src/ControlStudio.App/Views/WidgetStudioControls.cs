using ControlStudio.Core;
using ControlStudio.Core.Widgets;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal static class WidgetStudioStyle
{
    public static void Page(ContentPage page)
    {
        page.SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(page, false); DashboardStyle.NavigationAccent(page);
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
    }
    public static Border Card(View content, Thickness? padding = null, double radius = 22)
    {
        var border = new Border { Content = content, Padding = padding ?? new Thickness(14),
            StrokeShape = new RoundRectangle { CornerRadius = radius }, StrokeThickness = 1 };
        border.SetAppTheme<Brush>(Border.BackgroundProperty,
            Gradient("#FFFFFF", "#E9EDF7"), Gradient("#1D2430", "#0C1018"));
        border.SetAppTheme<Brush>(Border.StrokeProperty,
            Gradient("#B8C7E0", "#CDD2E1"), Gradient("#52617A", "#252D40"));
        return border;
    }
    public static View Header(string text, View back, View? trailing = null)
    {
        var title = SettingsControls.Text(text, 17, bold: true);
        title.VerticalOptions = LayoutOptions.Center; title.HorizontalTextAlignment = TextAlignment.Center;
        title.MaxLines = 2; SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var row = new Grid { Padding = new Thickness(18, 6, 18, 8), ColumnSpacing = 8,
            ColumnDefinitions = { new(48), new(GridLength.Star), new(48) } };
        back.HorizontalOptions = LayoutOptions.Start; back.VerticalOptions = LayoutOptions.Center; row.Add(back); row.Add(title, 1);
        if (trailing is not null) { trailing.HorizontalOptions = LayoutOptions.End; trailing.VerticalOptions = LayoutOptions.Center; row.Add(trailing, 2); }
        return row;
    }
    public static LinearGradientBrush Gradient(string start, string end) => new(new GradientStopCollection
    {
        new(Color.FromArgb(start), 0), new(Color.FromArgb(end), 1)
    }, new Point(0, 0), new Point(1, 1));
}

/// <summary>Shared, accent-aware text segments with native accessible button targets.</summary>
internal sealed class WidgetSegments : ContentView
{
    private readonly List<(Button Tap, AccentSurface Selected)> segments = [];
    private readonly IReadOnlyList<string> labels;
    public int SelectedIndex { get; private set; }
    public WidgetSegments(IReadOnlyList<string> labels, string automationId, Action<int> select)
    {
        this.labels = labels;
        var row = new Grid { ColumnSpacing = 2 };
        for (var i = 0; i < labels.Count; i++)
        {
            row.ColumnDefinitions.Add(new(GridLength.Star)); var index = i;
            Label Caption() => new() { Text = labels[index], FontSize = 12, MaxLines = 2,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
                InputTransparent = true };
            var idle = Caption(); idle.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#4D586E"), Color.FromArgb("#BCC7DD"));
            var caption = Caption(); caption.FontAttributes = FontAttributes.Bold;
            var selected = new AccentSurface(caption, 20, new Thickness(5, 9), captionSize: 12);
            selected.BindForeground(caption, Label.TextColorProperty); selected.InputTransparent = true;
            selected.Shadow = new Shadow { Opacity = .3f, Radius = 7, Offset = new Point(0, 1) };
            selected.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
            var tap = SettingsControls.TapTarget(labels[index], automationId + index);
            tap.Clicked += (_, _) => { if (IsEnabled && SelectedIndex != index) { Update(index); select(index); } };
            var cell = new Grid { MinimumHeightRequest = 44, Children = { tap, idle, selected } };
            AutomationProperties.SetIsInAccessibleTree(idle, false); AutomationProperties.SetIsInAccessibleTree(caption, false);
            segments.Add((tap, selected)); row.Add(cell, index);
        }
        Content = WidgetStudioStyle.Card(row, 3, 25); Update(0);
    }
    public void Update(int index)
    {
        if (index < 0 || index >= segments.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SelectedIndex = index;
        for (var i = 0; i < segments.Count; i++)
        {
            segments[i].Selected.IsVisible = i == index;
            SemanticProperties.SetDescription(segments[i].Tap, labels[i] + (i == index ? ", selected" : ""));
        }
    }
}

internal sealed class WidgetAddButton : ContentView
{
    public event EventHandler? Clicked;
    public WidgetAddButton()
    {
        MinimumWidthRequest = MinimumHeightRequest = WidthRequest = HeightRequest = 48;
        var icon = ClipboardEditorControls.Icon(StudioSymbol.Plus, size: 23);
        var surface = new AccentSurface(icon, 24, captionSize: 14, captionBold: false) { InputTransparent = true };
        surface.BindForeground(icon, StudioGlyph.TintProperty);
        surface.Shadow = new Shadow { Opacity = .4f, Radius = 10, Offset = new Point(0, 2) };
        surface.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        var tap = SettingsControls.TapTarget("Add widget design", "AddWidgetDesign");
        tap.Clicked += (_, e) => { if (IsEnabled) Clicked?.Invoke(this, e); };
        Content = new Grid { Children = { tap, surface } };
    }
}

/// <summary>Representative Home Screen geometry, scaled down only to fit the page.</summary>
internal sealed class WidgetPreviewFrame : ContentView
{
    private readonly WidgetSize size;
    private readonly WidgetCanvasSize? dimensions;
    private readonly Border frame;
    public WidgetPreviewFrame(WidgetSize size, View content, Color? background = null, double initialWidth = 320, WidgetCanvasSize? dimensions = null)
    {
        this.size = size; this.dimensions = dimensions;
        frame = new Border { Content = content, StrokeShape = new RoundRectangle { CornerRadius = 24 },
            StrokeThickness = 1, HorizontalOptions = LayoutOptions.Center };
        SettingsControls.SurfaceStroke(frame);
        if (background is not null) frame.BackgroundColor = background;
        else frame.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1C1E"));
        Content = frame; ApplyWidth(initialWidth);
        SizeChanged += (_, _) => ApplyWidth(Width);
    }
    internal static Size Fit(WidgetSize size, double availableWidth)
        => Fit(WidgetDesignRules.Canvas(size), availableWidth);
    internal static Size Fit(WidgetDesign design, double availableWidth) => Fit(WidgetDesignRules.Canvas(design), availableWidth);
    private static Size Fit((double Width, double Height) canvas, double availableWidth)
    {
        if (!double.IsFinite(availableWidth) || availableWidth <= 0) return Size.Zero;
        var scale = Math.Min(1, availableWidth / canvas.Width);
        return new(canvas.Width * scale, canvas.Height * scale);
    }
    private void ApplyWidth(double width)
    {
        var bounds = dimensions is null ? Fit(size, width) : Fit((dimensions.Width, dimensions.Height), width); if (bounds.Width == 0) return;
        if (Math.Abs(frame.WidthRequest - bounds.Width) > .5) frame.WidthRequest = bounds.Width;
        if (Math.Abs(frame.HeightRequest - bounds.Height) > .5) frame.HeightRequest = bounds.Height;
        if (Math.Abs(HeightRequest - bounds.Height) > .5) HeightRequest = bounds.Height;
    }
}

internal sealed class CounterWidgetPreview : ContentView
{
    private readonly Label value = new() { Text = "—", FontAttributes = FontAttributes.Bold, MaxLines = 1, FontAutoScalingEnabled = false };
    private readonly Label updated = new() { FontSize = 11, MaxLines = 1 };
    private readonly Label caption = new() { Text = "Last Updated", FontSize = 10 };
    private readonly bool medium;
    public CounterWidgetPreview(bool medium)
    {
        this.medium = medium; AutomationId = medium ? "MediumCounterPreview" : "SmallCounterPreview";
        var header = SettingsControls.Text("Control Studio", 11, secondary: true, bold: true);
        header.MaxLines = 1; header.FontAutoScalingEnabled = false;
        header.VerticalOptions = LayoutOptions.Center; header.VerticalTextAlignment = TextAlignment.Center;
        var mark = CounterIcon(12);
        var brand = new Grid { ColumnSpacing = 4, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        brand.Add(mark); brand.Add(header, 1);
        value.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        header.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEB2"));
        foreach (var label in new[] { updated, caption }) label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEB2"));
        var labels = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { brand, value, caption, updated } };
        var content = new Grid { Padding = 16, ColumnSpacing = 18, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        content.Add(labels);
        if (medium) content.Add(CounterIcon(42), 1);
        Content = new WidgetPreviewFrame(medium ? WidgetSize.Medium : WidgetSize.Small, content);
        // The canvas is a fixed-scale rendering preview; the enclosing view has
        // the complete VoiceOver description even when large numbers shrink.
        header.FontAutoScalingEnabled = updated.FontAutoScalingEnabled = caption.FontAutoScalingEnabled = false;
        foreach (var label in new[] { header, value, updated, caption }) AutomationProperties.SetIsInAccessibleTree(label, false);
        value.SizeChanged += (_, _) => FitValue();
        Update(null, loading: true);
    }
    private static Border CounterIcon(double size)
    {
        var glyph = new StudioGlyph(StudioSymbol.Counter, false) { WidthRequest = size, HeightRequest = size };
        glyph.SetDynamicResource(StudioGlyph.TintProperty, "Accent");
        var well = new Border { Content = glyph, Padding = 4, StrokeThickness = 0, VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = size > 20 ? 12 : 6 } };
        well.SetDynamicResource(BackgroundColorProperty, "CounterIconBackground");
        AutomationProperties.SetIsInAccessibleTree(well, false);
        AutomationProperties.SetIsInAccessibleTree(glyph, false);
        return well;
    }
    public void Update(CounterSnapshot? snapshot, bool loading = false)
    {
        value.Text = snapshot?.Value.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? (loading ? "—" : "Open Control Studio");
        value.MaxLines = snapshot is null && !loading ? 2 : 1;
        value.FontSize = snapshot is null ? (loading ? 46 : 17) : 46;
        caption.IsVisible = snapshot is not null;
        FitValue();
        updated.Text = snapshot is not null ? snapshot.UpdatedAt?.ToLocalTime().ToString("t") ?? "Not yet" : loading ? "Loading counter…" : "Shared counter unavailable";
        SemanticProperties.SetDescription(this, snapshot is not null ? $"{(medium ? "Medium" : "Small")} widget preview. Counter {value.Text}. Last updated {updated.Text}." : updated.Text);
    }
    private void FitValue()
    {
        if (!caption.IsVisible || string.IsNullOrEmpty(value.Text)) return;
        var width = value.Width > 0 ? value.Width : medium ? 240 : 132;
        var font = Math.Min(46, width / (value.Text.Length * .62));
        if (Math.Abs(value.FontSize - font) > .1) value.FontSize = font;
    }
}
