using ControlStudio.Core;
using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

public sealed class StudioPage : ContentPage
{
    private readonly ILogger logger;
    private bool opening;
    public StudioPage(ICounterRepository counter, IActionRepository actions, IActionValidator validator, IActionExecutor executor, ILoggerFactory logs, AppearancePreferences preferences, AppleColorPicker colourPicker, WidgetStudioService widgets)
    {
        logger = logs.CreateLogger<StudioPage>();
        Title = "Studio"; AutomationId = "StudioPage"; WidgetStudioStyle.Page(this);
        var title = SettingsControls.Text("Studio", 34, bold: true);
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10, 24, 28), Spacing = 16, MaximumWidthRequest = 800, Children =
            {
                new VerticalStackLayout { Spacing = 5, Margin = new Thickness(0, 0, 0, 12), Children =
                {
                    title, SettingsControls.Text("Create, customise and manage your widgets.", 14, secondary: true)
                } },
                Entry("My Widgets", "Create and manage\nyour custom widget designs.", StudioSymbol.Widgets, "OpenMyWidgets",
                    () => new WidgetsPage(widgets, colourPicker, logs.CreateLogger<WidgetsPage>())),
                Entry("Counter Widgets", "Preview your counters\nand widget sizes.", StudioSymbol.Hash, "OpenCounterWidgets",
                    () => new CounterWidgetsPage(counter, logs.CreateLogger<CounterWidgetsPage>())),
                Entry("Clipboard", "Text, photos and links.\nKeep it ready to use.", StudioSymbol.Clipboard, "OpenClipboard",
                    () => new ClipboardPage(actions, validator, executor, logs.CreateLogger<ClipboardPage>(), preferences, colourPicker))
            }
        } };
    }
    private Border Entry(string title, string description, StudioSymbol symbol, string automationId, Func<Page> destination)
    {
        var glyph = new StudioGlyph(symbol, false) { WidthRequest = 36, HeightRequest = 36, Tint = Color.FromArgb("#C0DFFF"),
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var icon = SettingsControls.Rounded(glyph, 20);
        icon.WidthRequest = icon.HeightRequest = 70; icon.VerticalOptions = LayoutOptions.Center;
        icon.Background = WidgetStudioStyle.Gradient("#1578C8", "#4E32A0");
        icon.Stroke = WidgetStudioStyle.Gradient("#5CBDFF", "#8471D9");
        icon.Shadow = new Shadow { Brush = Color.FromArgb("#177DFF"), Opacity = .3f, Radius = 12, Offset = new Point(0, 2) };
        var name = SettingsControls.Text(title, 20, bold: true);
        var detail = SettingsControls.Text(description, 13, secondary: true);
        AutomationProperties.SetIsInAccessibleTree(name, false); AutomationProperties.SetIsInAccessibleTree(detail, false);
        var labels = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = { name, detail } };
        var arrow = new StudioGlyph(StudioSymbol.Chevron, false) { WidthRequest = 16, HeightRequest = 16, VerticalOptions = LayoutOptions.Center };
        arrow.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#46536A"), Color.FromArgb("#DFE6F4"));
        var row = new Grid { Padding = 14, ColumnSpacing = 13, MinimumHeightRequest = 114, InputTransparent = true,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        row.Add(icon); row.Add(labels, 1); row.Add(arrow, 2);
        var tap = SettingsControls.TapTarget(title + ". " + description.Replace('\n', ' '), automationId);
        tap.CornerRadius = 24;
        tap.Pressed += (_, _) => row.Opacity = .8; tap.Released += (_, _) => row.Opacity = 1;
        tap.Clicked += async (_, _) =>
        {
            if (opening) return;
            opening = true; tap.IsEnabled = false;
            try { await Navigation.PushAsync(destination()); }
            catch (Exception ex)
            {
                logger.LogWarning("Studio navigation failed. ErrorType={ErrorType}", ex.GetType().Name);
                if (Window?.IsActivated == true) await DisplayAlertAsync("Unable to open page", "Please try again.", "OK");
            }
            finally { opening = false; tap.IsEnabled = true; row.Opacity = 1; }
        };
        var card = WidgetStudioStyle.Card(new Grid { Children = { tap, row } }, 0, 24);
        card.Shadow = new Shadow { Brush = Color.FromArgb("#23468E"), Opacity = .13f, Radius = 12, Offset = new Point(0, 3) };
        return card;
    }
}
