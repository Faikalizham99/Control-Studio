namespace ControlStudio.App.Views;

/// <summary>Query navigation ownership, rather than caching native picker appearance callbacks.</summary>
internal static class EditorPageActivity
{
    public static bool IsForeground(Page page)
    {
        if (page.Window is not { IsActivated: true, Page: { } root }) return false;
        Page current = root.Navigation.ModalStack.LastOrDefault() ?? root;
        while (true)
        {
            var child = current switch
            {
                NavigationPage navigation => navigation.CurrentPage,
                TabbedPage tabs => tabs.CurrentPage,
                FlyoutPage flyout => flyout.Detail,
                Shell shell => shell.CurrentPage,
                _ => null
            };
            if (child is null || ReferenceEquals(child, current)) return ReferenceEquals(current, page);
            current = child;
        }
    }
}
