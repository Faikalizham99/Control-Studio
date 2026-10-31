using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Reuses the normal editor/slots pages without removing any existing draft.</summary>
internal sealed class WidgetTapNavigation(TabbedPage tabs, NavigationPage home, NavigationPage studio,
    IWidgetSlotStore store, Func<WidgetSize, Page> gallery, Func<WidgetDesign, WidgetEditorPage> editor,
    Func<WidgetSize, Page> slots, Func<string, string, Task> notify, Func<bool>? canPresent = null)
{
    public bool Ready => tabs.Window?.IsActivated == true && tabs.Navigation.ModalStack.Count == 0
        && (canPresent?.Invoke() ?? true) && !studio.Navigation.NavigationStack.OfType<WidgetEditorPage>().Any(x => !x.CanAcceptWidgetTap);

    public async Task<bool> OpenAsync(WidgetTapLink link, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); if (!Ready) return false;
        if (link.IsCounter)
        {
            tabs.CurrentPage = home;
            await home.PopToRootAsync(false); return true;
        }
        var library = await store.ReadAsync(token);
        token.ThrowIfCancellationRequested(); if (!Ready) return false;
        var design = link.Resolve(library);
        tabs.CurrentPage = studio;
        if (await ResumeExistingEditorAsync(design)) return true;
        var size = link.Size!.Value;
        if (design is null && studio.CurrentPage is WidgetSlotsPage existingSlots)
        {
            await existingSlots.SelectSizeAsync(size); return true;
        }
        if (studio.CurrentPage?.AutomationId != "WidgetsPage")
        {
            await studio.PushAsync(gallery(size), false);
            token.ThrowIfCancellationRequested(); if (!Ready) return false;
            // Navigation can yield. Resolve again before using a saved design snapshot.
            library = await store.ReadAsync(token);
            token.ThrowIfCancellationRequested(); if (!Ready) return false;
            design = link.Resolve(library);
            if (await ResumeExistingEditorAsync(design)) return true;
        }
        token.ThrowIfCancellationRequested(); if (!Ready) return false;
        if (design is null)
        {
            if (studio.CurrentPage is not WidgetSlotsPage current || current.SelectedSize != size)
                await studio.PushAsync(slots(size), false);
        }
        else await studio.PushAsync(editor(design), false);
        return true;
    }

    private async Task<bool> ResumeExistingEditorAsync(WidgetDesign? design)
    {
        var existing = studio.Navigation.NavigationStack.OfType<WidgetEditorPage>().LastOrDefault();
        if (existing is null) return false;
        // Never pop/discard a draft or create concurrent editors for the same saved design.
        if (design?.Id != existing.DesignId || !ReferenceEquals(studio.CurrentPage, existing))
            await notify("Widget editor already open", "Your current widget edits are kept. Save or cancel this editor, then tap the Home Screen widget again.");
        return true;
    }
}
