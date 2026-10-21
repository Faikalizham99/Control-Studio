using ControlStudio.Core;
using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;

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
            Padding = new Thickness(16, 8, 16, 24), Spacing = 16, Children =
            {
                SettingsControls.Text("Studio", 34, bold: true),
                Entry("Widgets", "Small and Medium counter widgets", StudioSymbol.Widgets, "OpenWidgets", () => new WidgetsPage(counter)),
                Entry("Clipboard", "Save text. Copy it in a tap.", StudioSymbol.Clipboard, "OpenClipboard", () => new ClipboardPage(actions, validator, executor, logs.CreateLogger<ClipboardPage>()))
            }
        } };
    }

    private GlassCard Entry(string title, string description, StudioSymbol symbol, string automationId, Func<Page> destination)
    {
        var glyph = new StudioGlyph(symbol) { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var icon = SettingsControls.Rounded(new Grid { Children = { glyph } }, 18);
        icon.WidthRequest = icon.HeightRequest = 60; icon.VerticalOptions = LayoutOptions.Center;
        var names = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children =
        {
            SettingsControls.Text(title, 22, bold: true), SettingsControls.Text(description, 14, secondary: true)
        } };
        var arrow = SettingsControls.Text("›", 30, secondary: true); arrow.VerticalOptions = LayoutOptions.Center;
        var row = new Grid { ColumnSpacing = 14, Padding = new Thickness(4, 10), ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, InputTransparent = true };
        row.Add(icon); row.Add(names, 1); row.Add(arrow, 2);
        var button = new Button { BackgroundColor = Colors.Transparent, Text = "", AutomationId = automationId, MinimumHeightRequest = 100 };
        SemanticProperties.SetDescription(button, title + ". " + description);
        button.Clicked += async (_, _) =>
        {
            if (opening) return;
            opening = true; button.IsEnabled = false;
            try { await Navigation.PushAsync(destination()); }
            finally { opening = false; button.IsEnabled = true; }
        };
        return new GlassCard(new Grid { Children = { row, button } });
    }
}
