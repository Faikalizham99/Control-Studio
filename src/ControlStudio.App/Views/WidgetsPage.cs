using ControlStudio.Core;
using ControlStudio.Core.Widgets;
using ControlStudio.App.Platforms.iOS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class WidgetsPage : ContentPage
{
    private readonly ICounterRepository repository;
    private readonly WidgetStudioService studio;
    private readonly AppleColorPicker colourPicker;
    private readonly ILogger logger;
    private readonly VerticalStackLayout designs = new() { Spacing = 12 };
    private readonly Label counterStatus = SettingsControls.Text("", 13, secondary: true);
    private readonly PageActionButton add = new("Add", "AddWidgetDesign");
    private readonly PageActionButton back = new("", "BackFromWidgets", ProfileSymbol.Back);
    private readonly ClipboardEditorActionButton refresh = new("Refresh widgets", ClipboardEditorControls.Icon(StudioSymbol.Widgets), "RefreshWidgetDesigns");
    private readonly ClipboardEditorActionButton diagnostics = new("Widget diagnostics", ClipboardEditorControls.Icon(StudioSymbol.Widgets), "OpenWidgetDiagnostics");
    private readonly CounterWidgetPreview small = new(false);
    private readonly CounterWidgetPreview medium = new(true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private CancellationTokenSource lifetime = new();
    private bool busy;
    private bool active;
    private bool reloadPending;

    public WidgetsPage(ICounterRepository repository, WidgetStudioService studio, AppleColorPicker colourPicker, ILogger logger)
    {
        this.repository = repository; this.studio = studio; this.colourPicker = colourPicker; this.logger = logger;
        Title = "Widgets"; AutomationId = "WidgetsPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        NavigationPage.SetHasNavigationBar(this, false);
        add.Clicked += async (_, _) => await GuardAsync(() => Navigation.PushAsync(new WidgetEditorPage(studio, colourPicker, logger)));
        back.Clicked += async (_, _) => await GuardAsync(() => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await ReloadAsync(publish: true);
        var setup = DashboardStyle.Button("How to add a widget");
        setup.Clicked += async (_, _) => await GuardAsync(() => Navigation.PushAsync(new AboutPage()));
        diagnostics.Clicked += async (_, _) => await GuardAsync(() => Navigation.PushAsync(new WidgetDiagnosticsPage(studio, logger)));
        var content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children =
        {
            SettingsControls.Text("Your widgets", 28, bold: true),
            SettingsControls.Text("Build a widget with optional photo layers. Choose Small, Medium, Large or Extra Large. Portrait Extra Large works on iPhone and iPad with iOS/iPadOS 27+.", 15, secondary: true),
            designs, status, refresh, diagnostics,
            SettingsControls.Text("Add Control Studio → Studio Widget on your Home Screen. Touch and hold → Edit Widget → choose a design in the matching size.", 13, secondary: true),
            SettingsControls.Text("Counter widgets", 28, bold: true),
            SettingsControls.Text("Preview your counter in the two available sizes.", 15, secondary: true),
            SettingsControls.Text("Small", 14, bold: true), small,
            SettingsControls.Text("Medium", 14, bold: true), medium,
            counterStatus,
            SettingsControls.Text("These are in-app previews. iOS renders the Home Screen widget and schedules its updates.", 13, secondary: true), setup
        } } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Modal("Widgets", back, add)); layout.Add(content, 0, 1); Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime.Dispose(); lifetime = new(); await ReloadAsync();
    }
    protected override void OnDisappearing() { active = false; lifetime.Cancel(); base.OnDisappearing(); }

    private async Task ReloadAsync(bool publish = false)
    {
        if (!active) return;
        if (busy) { reloadPending = true; return; }
        busy = true; SetEnabled(false);
        var token = lifetime.Token;
        try
        {
            reloadPending = false;
            var saved = await studio.Repository.ReadAsync(token); token.ThrowIfCancellationRequested();
            ShowDesigns(saved);
            if (publish || saved.Count > 0)
            {
                var published = await studio.PublishAsync(); token.ThrowIfCancellationRequested();
                status.Text = published ? "Widget update requested. iOS controls when it appears." : "Saved on this device. Home Screen update unavailable. Check App Group signing, then tap Refresh.";
            }
            else status.Text = "Tap Add to create your first widget. Photos are optional.";
            try
            {
                var snapshot = StudioContent.WidgetSnapshot(await repository.ReadAsync(token));
                token.ThrowIfCancellationRequested(); small.Update(snapshot); medium.Update(snapshot);
                counterStatus.Text = snapshot is null ? "Shared counter unavailable. Review App Group signing in widget setup." : "";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Counter preview failed. ErrorType={ErrorType}", ex.GetType().Name);
                small.Update(null); medium.Update(null); counterStatus.Text = "Counter preview unavailable. Try Refresh.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning("Widget list failed. ErrorType={ErrorType}", ex.GetType().Name);
            status.Text = ex is IOException ? ex.Message : "Saved widget designs are unavailable. Try Refresh.";
        }
        finally
        {
            status.IsVisible = !string.IsNullOrEmpty(status.Text); counterStatus.IsVisible = !string.IsNullOrEmpty(counterStatus.Text); busy = false; SetEnabled(true);
            if (active && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }

    private void ShowDesigns(IReadOnlyList<WidgetDesign> saved)
    {
        designs.Children.Clear();
        foreach (var design in saved)
        {
            var edit = new ClipboardIconButton(StudioSymbol.Edit, "Edit"); edit.Describe("Edit widget", design.Name);
            var delete = new ClipboardIconButton(StudioSymbol.Delete, "Delete", destructive: true); delete.Describe("Delete widget", design.Name);
            edit.Clicked += async (_, _) => await GuardAsync(() => Navigation.PushAsync(new WidgetEditorPage(studio, colourPicker, logger, design)));
            delete.Clicked += async (_, _) => await GuardAsync(async () =>
            {
                if (!await DisplayAlertAsync("Delete widget design?", "Remove this saved design? Any Home Screen widgets using it will ask you to choose another design. Your original photos are kept.", "Delete", "Cancel") || !active) return;
                var published = await studio.DeleteAsync(design.Id);
                if (!active) return;
                ShowDesigns(await studio.Repository.ReadAsync());
                status.Text = published ? "Widget design deleted." : "Deleted on this device. Tap Refresh to update Home Screen widgets."; status.IsVisible = true;
            });
            var previewPath = studio.Repository.ThumbnailPath(design.RenderKey);
            View preview = previewPath is not null
                ? new Image { Source = previewPath, Aspect = Aspect.AspectFit, HeightRequest = 150, InputTransparent = true }
                : SettingsControls.Text("Preview missing. Open Edit and save again.", 13, secondary: true);
            SemanticProperties.SetDescription(preview, $"{WidgetDesignRules.SizeName(design.Size)} design preview: {design.Name}");
            var title = SettingsControls.Text(design.Name, 19, bold: true); title.MaxLines = 2; title.LineBreakMode = LineBreakMode.TailTruncation;
            var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { edit, delete } };
            designs.Children.Add(new GlassCard(new VerticalStackLayout { Spacing = 10, Children =
            {
                title, SettingsControls.Text(WidgetDesignRules.SizeName(design.Size), 13, secondary: true), preview, actions
            } }));
        }
    }
    private void SetEnabled(bool enabled) => add.IsEnabled = back.IsEnabled = refresh.IsEnabled = diagnostics.IsEnabled = designs.IsEnabled = enabled;
    private async Task GuardAsync(Func<Task> operation)
    {
        if (busy || !active) return; busy = true; SetEnabled(false);
        try { await operation(); }
        catch (Exception ex) { logger.LogWarning("Widget action failed. ErrorType={ErrorType}", ex.GetType().Name); if (active) { status.Text = ex is IOException ? ex.Message : "The widget action could not finish. Try again."; status.IsVisible = true; } }
        finally { busy = false; SetEnabled(true); if (active && reloadPending) { reloadPending = false; await ReloadAsync(); } }
    }

    private sealed class CounterWidgetPreview : ContentView
    {
        private readonly Label value = new() { Text = "—", FontAttributes = FontAttributes.Bold, MaxLines = 1, FontAutoScalingEnabled = false };
        private readonly Label updated = new() { FontSize = 11, MaxLines = 1 };
        private readonly Label caption = new() { Text = "Last Updated", FontSize = 10 };
        private readonly bool medium;

        public CounterWidgetPreview(bool medium)
        {
            this.medium = medium;
            AutomationId = medium ? "MediumCounterPreview" : "SmallCounterPreview";
            HeightRequest = 164;
            if (!medium) { WidthRequest = 164; HorizontalOptions = LayoutOptions.Center; }
            var header = SettingsControls.Text("Control Studio", 11, secondary: true, bold: true); header.MaxLines = 1; header.FontAutoScalingEnabled = false;
            var headerMark = new StudioGlyph(StudioSymbol.Counter, useAccent: false) { WidthRequest = 12, HeightRequest = 12, VerticalOptions = LayoutOptions.Center };
            headerMark.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            var brand = new Grid { ColumnSpacing = 4, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            brand.Add(headerMark); brand.Add(header, 1);
            value.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
            updated.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            var labels = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { brand, value, caption, updated } };
            var content = new Grid { ColumnSpacing = 18, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            content.Add(labels);
            if (medium)
            {
                var mark = new StudioGlyph(StudioSymbol.Counter, useAccent: false) { Tint = Color.FromArgb("#007AFF"), WidthRequest = 44, HeightRequest = 44, VerticalOptions = LayoutOptions.Center };
                // The existing WidgetKit medium design uses a fixed blue mark.
                content.Add(mark, 1);
            }
            var border = new Border { Content = content, Padding = 16, StrokeShape = new RoundRectangle { CornerRadius = 24 }, StrokeThickness = 1 };
            SettingsControls.SurfaceStroke(border); border.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1C1E"));
            Content = border; Update(null, loading: true);
        }

        public void Update(CounterSnapshot? snapshot, bool loading = false)
        {
            value.Text = snapshot?.Value.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? (loading ? "—" : "Open Control Studio");
            value.MaxLines = snapshot is null && !loading ? 2 : 1;
            value.FontSize = snapshot is null ? (loading ? 46 : 17) : Math.Min(46, (medium ? 180 : 132) / (value.Text.Length * .62));
            caption.IsVisible = snapshot is not null;
            updated.Text = snapshot is not null ? snapshot.UpdatedAt?.ToLocalTime().ToString("t") ?? "Not yet" : loading ? "Loading counter…" : "Shared counter unavailable";
            SemanticProperties.SetDescription(this, snapshot is not null ? $"{(medium ? "Medium" : "Small")} widget preview. Counter {value.Text}. Last updated {updated.Text}." : updated.Text);
        }
    }
}
