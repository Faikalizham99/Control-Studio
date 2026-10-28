using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed partial class WidgetEditorPage
{
    private readonly WidgetEditHistory history;
    private readonly WidgetHistoryControls historyControls = new();
    private readonly List<WidgetHistorySliderBinding> historySliders = [];
    private readonly SemaphoreSlim photoCleanupGate = new(1, 1);
    private bool restoringHistory;
    private WidgetEditorState CaptureHistory() => new(draft, selected, name.Text ?? "");
    private void RecordHistory(string? typingKey = null)
    {
        if (restoringHistory || saved || departed) return;
        history.Record(CaptureHistory(), typingKey); RefreshHistory();
        if (ownedPhotos.Count > 0 && !history.IsInteracting) _ = CleanupPhotosAsync();
    }
    private void RefreshHistory()
    {
        var available = !busy && !saved && !departed && !canvas.IsInteracting && !layerList.IsReordering
            && !background.IsBusy && !clockControls.IsBusy && !batteryControls.IsBusy;
        historyControls.Update(available && history.CanUndo, available && history.CanRedo, history.UndoCount, history.RedoCount);
    }
    private void FinishHistoryInteraction(object source)
    {
        if (!history.End(source)) return;
        RefreshHistory();
        if (ownedPhotos.Count > 0 && !history.IsInteracting) _ = CleanupPhotosAsync();
    }
    private void BreakHistoryTyping()
    {
        history.BreakTyping();
        if (ownedPhotos.Count > 0 && !history.IsInteracting) _ = CleanupPhotosAsync();
    }
    private void BindHistoryInputs()
    {
        historyControls.UndoRequested += (_, _) => RestoreHistory(redo: false);
        historyControls.RedoRequested += (_, _) => RestoreHistory(redo: true);
        name.TextChanged += (_, _) => { if (!restoringHistory && !updating && CanEdit) RecordHistory("widget-name"); };
        // All sliders, including shared nested colour/rotation controls, use their
        // native drag boundaries. Accessibility value changes remain individual edits.
        foreach (var element in HistoryDescendants(form))
        {
            if (element is Slider slider)
            {
                historySliders.Add(new(slider, form, () => CanEdit,
                    () => { history.Begin(slider); RefreshHistory(); }, () => FinishHistoryInteraction(slider)));
            }
            if (element is InputView input)
            {
                input.Focused += (_, _) => BreakHistoryTyping();
                input.Unfocused += (_, _) => BreakHistoryTyping();
            }
            if (element is ColourPickerView picker)
                picker.BusyChanged += (_, _) =>
                {
                    if (picker.IsBusy && !saved && !departed) { history.Begin(picker); RefreshHistory(); }
                    else FinishHistoryInteraction(picker);
                };
        }
    }
    private static IEnumerable<Element> HistoryDescendants(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var element in HistoryDescendants(child)) yield return element;
    }
    private void RestoreHistory(bool redo)
    {
        if (!CanEdit || history.IsInteracting) return;
        if (!(redo ? history.CanRedo : history.CanUndo)) return;
        scroll.PreserveViewport(); restoringHistory = true;
        try
        {
            // Pending HEX/threshold strings have not changed the preview. Discard
            // them before Unfocused can commit stale values into restored layers.
            background.DiscardPendingHex(); clockControls.DiscardPending(); batteryControls.DiscardPending();
            foreach (var input in HistoryDescendants(form).OfType<InputView>()) input.Unfocus();
            var state = redo ? history.Redo()! : history.Undo()!;
            if (selected != state.Selection) { CloseTools(); precise.IsVisible = false; }
            draft = state.Design; selected = state.Selection; name.Text = state.NameText;
            if (!draft.Layers.Any(x => x.Id == selected)) selected = null;
            ApplyCanvasSizing(); history.Rebase(CaptureHistory());
            sizes.Update(Array.IndexOf(WidgetDesignRules.SupportedSizes.ToArray(), draft.Size));
            background.SetSelection(draft.BackgroundHex); RefreshBackground(); RefreshLayers();
        }
        finally { restoringHistory = false; RefreshActions(); }
        ShowStatus(redo ? "Edit redone." : "Edit undone.");
        if (ownedPhotos.Count > 0) _ = CleanupPhotosAsync();
    }
    private async Task CleanupPhotosAsync(bool all = false)
    {
        await photoCleanupGate.WaitAsync();
        try
        {
            var retained = all || saved || departed ? null : history.PhotoKeys();
            var retired = ownedPhotos.Where(x => retained is null || !retained.Contains(x)).ToArray();
            if (retired.Length == 0) return;
            await studio.RemoveDraftPhotosAsync(retired);
            ownedPhotos.ExceptWith(retired);
        }
        catch (Exception ex) { logger.LogWarning("Widget draft photo cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
        finally { photoCleanupGate.Release(); }
    }
}
