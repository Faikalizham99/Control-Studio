using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class ClipboardArrangePage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly ILogger logger;
    private readonly ClipboardArrangement draft;
    private readonly ClipboardArrangeView list;
    private readonly PageActionButton done = new("Done", "SaveClipboardArrangement") { DisabledReason = "Please wait while the order is saved." };
    private readonly PageActionButton cancel = new("Cancel", "CancelClipboardArrangement") { DisabledReason = "Please wait while the order is saved." };
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private bool saving, closing, active;
    private CancellationTokenSource lifetime = new();

    public ClipboardArrangePage(IActionRepository repository, ILogger logger, ClipboardKind kind, IEnumerable<ActionDefinition> items)
    {
        this.repository = repository; this.logger = logger; draft = new ClipboardArrangement(items);
        Title = $"Arrange {kind.ToString().ToLowerInvariant()}"; AutomationId = "ArrangeClipboardPage";
        SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        NavigationPage.SetHasNavigationBar(this, false);
        list = new ClipboardArrangeView(draft) { AutomationId = "ClipboardArrangeList" };
        draft.Changed += DraftChanged;
        done.Clicked += async (_, _) => await SaveAsync();
        cancel.Clicked += async (_, _) => { if (!saving) await CloseAsync(); };
        var heading = new VerticalStackLayout { Padding = new Thickness(20, 16, 20, 4), Spacing = 10, Children =
        {
            SettingsControls.Text("Make it your order.", 25, bold: true),
            SettingsControls.Text("Drag the handles within each group. Changes save when you tap Done. Pin or unpin from Clipboard to change groups.", 14, secondary: true), status
        } };
        status.IsVisible = false;
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Modal(Title, cancel, done)); layout.Add(heading, 0, 1); layout.Add(list, 0, 2); Content = layout;
    }

    private void DraftChanged(object? sender, EventArgs args) { if (!saving) status.IsVisible = false; }
    protected override void OnAppearing() { base.OnAppearing(); active = true; lifetime.Dispose(); lifetime = new(); }
    protected override void OnDisappearing() { active = false; lifetime.Cancel(); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() => saving || closing || base.OnBackButtonPressed();
    private void SetEnabled(bool enabled) => done.IsEnabled = cancel.IsEnabled = list.IsEnabled = enabled;
    private async Task CloseAsync()
    {
        if (closing || !active) return;
        closing = true; SetEnabled(false);
        try { await Navigation.PopModalAsync(); }
        catch (Exception ex)
        {
            logger.LogWarning("Clipboard arrange dismissal failed. ErrorType={ErrorType}", ex.GetType().Name);
            status.Text = "Could not close Arrange. Try again."; status.IsVisible = true;
        }
        finally { closing = false; if (!saving) SetEnabled(true); }
    }
    private async Task SaveAsync()
    {
        if (saving || closing || !active) return;
        if (!draft.HasChanges) { await CloseAsync(); return; }
        saving = true; SetEnabled(false);
        status.Text = "Saving order…"; status.IsVisible = true;
        try
        {
            await repository.ReorderClipboardAsync(draft.OrderedIds, lifetime.Token);
            if (active) await CloseAsync();
        }
        catch (OperationCanceledException) { status.Text = "Order was not saved. Tap Done to try again."; }
        catch (Exception ex)
        {
            logger.LogWarning("Clipboard arrangement failed. ErrorType={ErrorType}", ex.GetType().Name);
            status.Text = ex is IOException or ActionValidationException ? ex.Message : "The order could not be saved. Try again.";
            SemanticScreenReader.Default.Announce(status.Text);
        }
        finally { saving = false; SetEnabled(true); }
    }
}
