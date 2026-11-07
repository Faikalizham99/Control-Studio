using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal sealed partial class WidgetEditorPage
{
    private async Task TestShortcutAsync()
    {
        if (tapAction.Action != WidgetTapAction.RunShortcut || !tapAction.TryValidate()) return;
        var uri = WidgetTapActions.ShortcutUri(tapAction.ShortcutName);
        try
        {
            var opened = await openShortcut(uri);
            if (!departed) ShowStatus(opened ? "Shortcut launch requested. Your widget draft has not been saved."
                : "Could not open Shortcuts. Check that the Shortcuts app is installed, then try again.");
        }
        catch (Exception)
        {
            if (!departed) ShowStatus("Could not open Shortcuts. Check the app and Shortcut name, then try again.");
        }
    }
}
