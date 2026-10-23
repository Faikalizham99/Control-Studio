using System.Text;
using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetDiagnosticsPage : ContentPage
{
    private readonly WidgetStudioService studio;
    private readonly ILogger logger;
    private readonly PageActionButton back = new("", "BackFromWidgetDiagnostics", ProfileSymbol.Back);
    private readonly PageActionButton read = new("Read results", "ReadWidgetDiagnostics");
    private readonly ClipboardEditorActionButton text = new("Start text test", null, "StartWidgetTextTest", primary: true);
    private readonly ClipboardEditorActionButton photo = new("Start photo test", null, "StartWidgetPhotoTest");
    private readonly ClipboardEditorActionButton stop = new("Stop test", null, "StopWidgetDiagnostics");
    private readonly ClipboardEditorActionButton copy = new("Copy report", ClipboardEditorControls.Icon(StudioSymbol.Clipboard), "CopyWidgetDiagnostics");
    private readonly Label state = SettingsControls.Text("", 15, bold: true);
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label output = SettingsControls.Text("", 12, secondary: true);
    private readonly ClipboardCopyFeedback feedback = new();
    private WidgetDiagnosticSnapshot? snapshot;
    private string report = "";
    private bool busy, active, reloadPending;
    private int lifetime, copyEpoch;
    private Window? observedWindow;

    public WidgetDiagnosticsPage(WidgetStudioService studio, ILogger logger)
    {
        this.studio = studio; this.logger = logger;
        Title = "Widget diagnostics"; AutomationId = "WidgetDiagnosticsPage"; SafeAreaEdges = SafeAreaEdges.All;
        DashboardStyle.Page(this); NavigationPage.SetHasNavigationBar(this, false);
        back.Clicked += async (_, _) => await RunAsync(() => Navigation.PopAsync());
        read.Clicked += async (_, _) => await RunAsync(LoadAsync);
        text.Clicked += async (_, _) => await RunAsync(() => ConfigureAsync(true));
        photo.Clicked += async (_, _) => await RunAsync(() => ConfigureAsync(false));
        stop.Clicked += async (_, _) => await RunAsync(() => ConfigureAsync(null));
        copy.Clicked += async (_, _) => await RunAsync(CopyAsync);
        output.FontFamily = "Menlo"; SemanticProperties.SetHint(output, "Use Copy report to share these results.");
        var content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children =
        {
            SettingsControls.Text("Find where loading stops", 25, bold: true),
            SettingsControls.Text("Compare Studio Widget with the separate Studio Widget Test on your Home Screen. Saved designs and counter widgets are kept as they are.", 14, secondary: true),
            ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 12, Children =
            {
                state,
                SettingsControls.Text("1. Save a Small design in Widgets, then start the text test here.\n2. On the Home Screen, add Control Studio's Studio Widget Test in Small. It chooses a matching design automatically; there is no design selector.\n3. Return here, tap Read results, then Copy report. Tell us whether text, a photo, an error or grey bars appeared.\n4. Start the photo test, check the same test widget, then read and copy that report too.", 14),
                text, photo, stop,
                SettingsControls.Text("Studio Widget Test uses the first saved design matching its size. For a clear comparison, use a size with one saved design. Both widget types follow this test mode, so the original Studio Widget can be compared alongside it.", 13, secondary: true),
                SettingsControls.Text("Text test skips photo loading and uses a simple text view. Photo test uses the existing image path. Each start creates a fresh report; copy results before starting the next test. iOS decides when to refresh.", 13, secondary: true)
            } }),
            status,
            ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 12, Children =
            {
                SettingsControls.Text("Latest test results", 19, bold: true), copy, output,
                SettingsControls.Text("These records show which extension stages ran. An entry being ready does not prove iOS displayed it. An empty log can also mean diagnostic storage was unavailable. Stop test restores the normal photo path when iOS next refreshes.", 12, secondary: true)
            } })
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Modal(Title, back, read)); layout.Add(new ScrollView { Content = content }, 0, 1);
        var toast = new ClipboardToastView(feedback); Grid.SetRowSpan(toast, 2); layout.Add(toast); Content = layout;
        SetEnabled();
    }
    private async Task ConfigureAsync(bool? textOnly)
    {
        var epoch = lifetime;
        var requested = await studio.ConfigureDiagnosticsAsync(textOnly);
        await LoadAsync();
        if (active && epoch == lifetime) status.Text = requested ? "Refresh requested. Return to your Home Screen to try the widget." : "Test settings saved, but the refresh request failed. Try starting the test again.";
    }
    private async Task LoadAsync()
    {
        var epoch = lifetime;
        var value = await studio.ReadDiagnosticsAsync();
        if (!active || epoch != lifetime) return;
        snapshot = value;
        state.Text = value.Settings is { Enabled: true } settings
            ? settings.TextOnly ? "Text test active" : "Photo test active" : "Diagnostics off";
        report = FormatReport(value); output.Text = report;
        var native = value.Events.Count(x => x.Source != "host");
        status.Text = value.Settings is null ? "Choose Start text test to begin."
            : native == 0 ? "No widget execution records captured yet. Try the Home Screen widget, then Read results. Host records alone do not confirm it ran."
            : $"{native} widget records and {value.Events.Count - native} host records loaded.";
    }
    private static string FormatReport(WidgetDiagnosticSnapshot value)
    {
        var result = new StringBuilder();
        result.AppendLine($"Control Studio {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})");
        result.AppendLine($"iOS {DeviceInfo.Current.VersionString}");
        if (value.Settings is not { } settings) { result.AppendLine("Diagnostics have not been started."); return result.ToString(); }
        result.AppendLine($"Mode: {(settings.Enabled ? settings.TextOnly ? "text" : "photo" : "off")}");
        result.AppendLine($"Session: {settings.Session}");
        result.AppendLine($"Started UTC: {DateTimeOffset.FromUnixTimeMilliseconds(settings.StartedAtUnixMilliseconds):O}");
        result.AppendLine($"Unreadable event files: {value.UnreadableEvents}");
        result.AppendLine($"Widget records: {value.Events.Count(x => x.Source != "host")}; host records: {value.Events.Count(x => x.Source == "host")}");
        result.AppendLine($"Captured static provider requests: {ProviderRequests(value, "static")}; configurable provider requests: {ProviderRequests(value, "configurable")}");
        result.AppendLine("path=static: Studio Widget Test; path=configurable: Studio Widget.");
        foreach (var group in value.Events.GroupBy(x => x.Request))
        {
            result.AppendLine($"Request {group.Key[..8]}:");
            foreach (var item in group.OrderBy(x => x.Sequence))
                result.AppendLine($"{DateTimeOffset.FromUnixTimeMilliseconds(item.TimestampUnixMilliseconds):yyyy-MM-dd HH:mm:ss.fff}Z {item.Source} #{item.Sequence} {item.Stage} {item.Detail}");
        }
        result.AppendLine("entry.ready is not a delivery acknowledgement from iOS.");
        return result.ToString();
    }
    private static int ProviderRequests(WidgetDiagnosticSnapshot value, string path) => value.Events
        .Count(x => x.Source is "snapshot" or "timeline" && x.Stage == "provider.begin" && x.Detail.StartsWith($"path={path};", StringComparison.Ordinal));
    private async Task CopyAsync()
    {
        var epoch = lifetime; var operation = copyEpoch;
        try { await Clipboard.Default.SetTextAsync(report); }
        catch (Exception ex)
        {
            logger.LogWarning("Diagnostic report copy failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (active && epoch == lifetime && operation == copyEpoch && Window?.IsActivated == true) _ = feedback.ErrorAsync();
            return;
        }
        if (!active || epoch != lifetime || operation != copyEpoch || Window?.IsActivated != true) return;
        _ = feedback.SuccessAsync(Guid.Empty, UIKit.UIAccessibility.IsVoiceOverRunning);
        try { SemanticScreenReader.Default.Announce("Diagnostic report copied"); } catch (Exception ex) { logger.LogDebug("Diagnostic copy announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy || !active) return; busy = true; SetEnabled();
        try { await action(); }
        catch (Exception ex)
        {
            logger.LogWarning("Widget diagnostics unavailable. ErrorType={ErrorType}", ex.GetType().Name);
            if (active) status.Text = ex is IOException ? ex.Message : "Diagnostics are unavailable. Check App Group access and try Read results again.";
        }
        finally
        {
            busy = false; SetEnabled();
            if (active && reloadPending) { reloadPending = false; await RunAsync(LoadAsync); }
        }
    }
    private void SetEnabled()
    {
        back.IsEnabled = read.IsEnabled = text.IsEnabled = photo.IsEnabled = active && !busy;
        stop.IsEnabled = active && !busy && snapshot?.Settings?.Enabled == true;
        copy.IsEnabled = active && !busy && report.Length > 0;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime++;
        if (observedWindow is not null) { observedWindow.Deactivated -= WindowDeactivated; observedWindow.Stopped -= WindowDeactivated; }
        observedWindow = Window;
        if (observedWindow is not null) { observedWindow.Deactivated += WindowDeactivated; observedWindow.Stopped += WindowDeactivated; }
        if (busy) { reloadPending = true; SetEnabled(); return; }
        await RunAsync(LoadAsync);
    }
    private void WindowDeactivated(object? sender, EventArgs args) { copyEpoch++; feedback.Clear(); }
    protected override void OnDisappearing()
    {
        active = false; lifetime++; copyEpoch++; feedback.Clear();
        if (observedWindow is not null) { observedWindow.Deactivated -= WindowDeactivated; observedWindow.Stopped -= WindowDeactivated; observedWindow = null; }
        SetEnabled(); base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();
}
