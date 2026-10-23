using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ControlStudio.App.Views;

internal sealed class WidgetDiagnosticsPage : ContentPage
{
    private readonly Func<Task<WidgetBackgroundReport>> read;
    private readonly Func<string, Task> copy;
    private readonly Label status = SettingsControls.Text("Loading widget diagnostics…", 16);
    private readonly Label report = SettingsControls.Text("", 12);
    private readonly PageActionButton refresh = new("Refresh report", "RefreshWidgetDiagnostics");
    private readonly PageActionButton copyButton = new("Copy report", "CopyWidgetDiagnostics");
    private WidgetBackgroundReport? displayed;
    private Window? observedWindow;
    private bool active, busy, pending;
    private int epoch;

    internal WidgetDiagnosticsPage(Func<Task<WidgetBackgroundReport>> read, Func<string, Task>? copy = null)
    {
        this.read = read; this.copy = copy ?? (text => Clipboard.Default.SetTextAsync(text));
        Title = "Widget diagnostics"; AutomationId = "WidgetDiagnosticsPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        status.AutomationId = "WidgetDiagnosticsStatus"; report.AutomationId = "WidgetDiagnosticsReport";
        refresh.MinimumHeightRequest = copyButton.MinimumHeightRequest = 48;
        report.FontFamily = "Courier"; report.LineBreakMode = LineBreakMode.CharacterWrap;
        refresh.Clicked += async (_, _) => await RefreshAsync(); copyButton.Clicked += async (_, _) => await CopyAsync();
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children = {
            SettingsControls.Text("Check widget transparency", 24, bold: true),
            SettingsControls.Text("Open the Home Screen widget picker and view a Studio slot. Return here, tap Refresh report, then Copy report to share the result. You do not need to delete the app or your designs.", 16),
            SettingsControls.Text("Reports stay on this iPhone. They contain build details and adapter status, with no photos, design names or clipboard content. Nothing is sent automatically.", 14, secondary: true),
            DashboardStyle.Card(status), refresh, copyButton, SettingsControls.Rounded(report, 16, 14)
        } } };
        UpdateEnabled();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; epoch++;
        if (observedWindow is not null) { observedWindow.Activated -= Activated; observedWindow.Deactivated -= Deactivated; }
        observedWindow = Window;
        if (observedWindow is not null) { observedWindow.Activated += Activated; observedWindow.Deactivated += Deactivated; }
        await RefreshAsync();
    }
    protected override void OnDisappearing()
    {
        active = false; epoch++; pending = false;
        if (observedWindow is not null) { observedWindow.Activated -= Activated; observedWindow.Deactivated -= Deactivated; observedWindow = null; }
        UpdateEnabled(); base.OnDisappearing();
    }
    private async void Activated(object? sender, EventArgs args) { active = true; epoch++; await RefreshAsync(); }
    private void Deactivated(object? sender, EventArgs args) { active = false; epoch++; UpdateEnabled(); }
    internal async Task RefreshAsync()
    {
        if (!active) return;
        if (busy) { pending = true; return; }
        busy = true; var version = epoch; UpdateEnabled();
        try
        {
            var result = await read();
            if (active && version == epoch) { displayed = result; status.Text = result.Status; report.Text = result.Text; }
        }
        catch (Exception)
        {
            if (active && version == epoch) { displayed = null; report.Text = ""; status.Text = "The report could not be loaded. Unlock this iPhone and try Refresh report."; }
        }
        finally
        {
            busy = false; UpdateEnabled();
            if (active && pending) { pending = false; await RefreshAsync(); }
        }
    }
    internal async Task CopyAsync()
    {
        if (!active || busy || displayed is null) return;
        busy = true; var version = epoch; var text = displayed.Text; UpdateEnabled();
        try { await copy(text); if (active && version == epoch) CopyStatus("Report copied. Paste it into your message."); }
        catch (Exception) { if (active && version == epoch) CopyStatus("The report could not be copied. Try Copy report again."); }
        finally
        {
            busy = false; UpdateEnabled();
            if (active && pending) { pending = false; await RefreshAsync(); }
        }
    }
    private void CopyStatus(string message)
    {
        status.Text = message;
        try { Microsoft.Maui.Accessibility.SemanticScreenReader.Default.Announce(message); }
        catch (Exception) { /* Optional feedback cannot change a completed copy. */ }
    }
    private void UpdateEnabled()
    {
        refresh.DisabledReason = !active ? "Open this page to refresh the report." : "A report action is in progress.";
        copyButton.DisabledReason = !active ? "Open this page to copy the report." : busy ? "A report action is in progress." : "Load a report first.";
        refresh.IsEnabled = active && !busy; copyButton.IsEnabled = active && !busy && displayed is not null;
    }
}
