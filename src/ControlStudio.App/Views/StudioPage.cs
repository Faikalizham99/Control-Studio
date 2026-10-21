using ControlStudio.Core;
using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace ControlStudio.App.Views;

public sealed class StudioPage : ContentPage
{
    private readonly ICounterRepository counter;
    private readonly IActionRepository actions;
    private readonly IActionValidator validator;
    private readonly IActionExecutor executor;
    private readonly ILoggerFactory logs;
    private bool opening;

    public StudioPage(ICounterRepository counter, IActionRepository actions, IActionValidator validator, IActionExecutor executor, ILoggerFactory logs)
    {
        this.counter = counter; this.actions = actions; this.validator = validator; this.executor = executor; this.logs = logs;
        Title = "Studio"; AutomationId = "StudioPage"; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false); DashboardStyle.NavigationAccent(this);
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(8, 8, 8, 24), Spacing = 12, Children =
            {
                Header(),
                Entry("Widgets", "Small, Medium, Large\nand Extra Large widgets", StudioSymbol.Widgets, "OpenWidgets", () => new WidgetsPage(this.counter),
                    [("Counters", StudioSymbol.Bars), ("Dates", StudioSymbol.Calendar), ("Photos", StudioSymbol.Photo)]),
                Entry("Clipboard", "Text, photos and links.\nKeep it ready to use.", StudioSymbol.Clipboard, "OpenClipboard", () => new ClipboardPage(this.actions, this.validator, this.executor, this.logs.CreateLogger<ClipboardPage>()),
                    [("Text", StudioSymbol.Text), ("Links", StudioSymbol.Link), ("Images", StudioSymbol.Photo)])
            }
        } };
    }

    private static View Header() => new VerticalStackLayout
    {
        Spacing = 2, Margin = new Thickness(6, 0, 6, 8), Children =
        {
            SettingsControls.Text("Studio", 32, bold: true),
            SettingsControls.Text("Create, capture and customise\nyour content.", 14, secondary: true)
        }
    };

    private GlassCard Entry(string title, string description, StudioSymbol symbol, string automationId, Func<Page> destination, (string Text, StudioSymbol Icon)[] categories)
    {
        var widgets = symbol == StudioSymbol.Widgets;
        var glyph = CategoryGlyph(symbol, widgets, 34);
        var icon = SettingsControls.Rounded(new Grid { Children = { glyph } }, 16);
        icon.WidthRequest = icon.HeightRequest = 64; icon.VerticalOptions = LayoutOptions.Center;
        icon.SetAppTheme<Brush>(Border.BackgroundProperty,
            Gradient("#FFFFFF", "#E7EAF0"), Gradient("#383A3C", "#18191B"));
        icon.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(Color.FromArgb("#CDD2DC")), new SolidColorBrush(Colors.White.WithAlpha(.18f)));
        var name = SettingsControls.Text(title, 22, bold: true);
        var detail = SettingsControls.Text(description, 14, secondary: true);
        AutomationProperties.SetIsInAccessibleTree(name, false); AutomationProperties.SetIsInAccessibleTree(detail, false);
        var names = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children =
        {
            name, detail
        } };
        var arrowGlyph = new StudioGlyph(StudioSymbol.Chevron, useAccent: false) { WidthRequest = 18, HeightRequest = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        arrowGlyph.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#3B4351"), Color.FromArgb("#E5E7EC"));
        var arrow = new Border { Content = arrowGlyph, WidthRequest = 36, HeightRequest = 36, StrokeShape = new Ellipse(), StrokeThickness = 1, VerticalOptions = LayoutOptions.Center };
        arrow.SetAppThemeColor(BackgroundColorProperty, Colors.Black.WithAlpha(.035f), Colors.White.WithAlpha(.07f));
        arrow.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Colors.Black.WithAlpha(.06f)), new SolidColorBrush(Colors.White.WithAlpha(.06f)));
        var row = new Grid { ColumnSpacing = 14, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, InputTransparent = true };
        row.Add(icon); row.Add(names, 1); row.Add(arrow, 2);
        var chips = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center, Margin = new Thickness(0, 0, -6, -6), InputTransparent = true };
        foreach (var category in categories) chips.Children.Add(Chip(category.Text, category.Icon, widgets));
        var visuals = new VerticalStackLayout { Padding = 14, Spacing = 14, InputTransparent = true, Children = { row, chips } };
        var button = new Button { BackgroundColor = Colors.Transparent, Text = "", Padding = 0, CornerRadius = 24, AutomationId = automationId, MinimumHeightRequest = 130 };
        SemanticProperties.SetDescription(button, title + ". " + description.Replace('\n', ' '));
        var card = new GlassCard(new Grid { Children = { visuals, button } }, 0);
        var surface = (Border)card.Content;
        surface.SetAppTheme<Brush>(Border.BackgroundProperty,
            Gradient("#FCFDFE", "#F1F3F7"), new LinearGradientBrush(new GradientStopCollection
            {
                new(Color.FromArgb("#D92B2D30"), 0), new(Color.FromArgb("#EB111214"), .55f), new(Color.FromArgb("#D9242527"), 1)
            }, new Point(0, 0), new Point(1, 1)));
        surface.SetAppTheme<Brush>(Border.StrokeProperty,
            Gradient("#C6CCD7", "#D8DCE4"), new LinearGradientBrush(new GradientStopCollection
            {
                new(Colors.White.WithAlpha(.3f), 0), new(Colors.White.WithAlpha(.09f), .55f), new(Colors.White.WithAlpha(.22f), 1)
            }, new Point(0, 0), new Point(1, 1)));
        button.Pressed += (_, _) => visuals.Opacity = .75;
        button.Released += (_, _) => visuals.Opacity = 1;
        button.Clicked += async (_, _) =>
        {
            if (opening) return;
            opening = true; button.IsEnabled = false;
            try { await Navigation.PushAsync(destination()); }
            finally { opening = false; button.IsEnabled = true; }
        };
        return card;
    }

    private static View Chip(string text, StudioSymbol symbol, bool widgets)
    {
        var glyph = CategoryGlyph(symbol, widgets, 15);
        var label = SettingsControls.Text(text, 11); label.VerticalOptions = LayoutOptions.Center;
        AutomationProperties.SetIsInAccessibleTree(label, false);
        var row = new HorizontalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = { glyph, label } };
        var chip = new Border { Content = row, Padding = new Thickness(8, 6), Margin = new Thickness(0, 0, 6, 6), StrokeShape = new RoundRectangle { CornerRadius = 16 }, StrokeThickness = 1, InputTransparent = true };
        chip.SetAppTheme<Brush>(Border.BackgroundProperty, Gradient("#F8F9FC", "#E9ECF2"), Gradient("#2B2D30", "#232426"));
        chip.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#D5DAE4")), new SolidColorBrush(Colors.White.WithAlpha(.1f)));
        return chip;
    }

    private static StudioGlyph CategoryGlyph(StudioSymbol symbol, bool widgets, double size)
    {
        var glyph = new StudioGlyph(symbol, useAccent: false) { WidthRequest = size, HeightRequest = size, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        glyph.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb(widgets ? "#0072C8" : "#853CCE"), Color.FromArgb(widgets ? "#009EFF" : "#AE56FF"));
        return glyph;
    }

    private static LinearGradientBrush Gradient(string start, string end) => new(new GradientStopCollection
    {
        new(Color.FromArgb(start), 0), new(Color.FromArgb(end), 1)
    }, new Point(0, 0), new Point(1, 1));
}
