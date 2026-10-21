using ControlStudio.App.Platforms.iOS;

namespace ControlStudio.App.Views;

internal sealed class ClipboardPhotoPage : ContentPage
{
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private bool busy;
    public ClipboardPhotoPage(string title, string key)
    {
        Title = title; AutomationId = "ClipboardPhotoPreview"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        var photo = new Image { Source = ClipboardPhotoStore.RequirePath(key), Aspect = Aspect.AspectFit, Margin = 12 };
        SemanticProperties.SetDescription(photo, title);
        var copy = DashboardStyle.Button("Copy photo");
        var share = DashboardStyle.Button("Share / Save to Photos");
        copy.Clicked += async (_, _) => await RunAsync(async () => { await ClipboardPhotoStore.CopyAsync(key); status.Text = "Photo copied. Paste it into an app that accepts images."; });
        share.Clicked += async (_, _) => await RunAsync(() => ClipboardPhotoStore.ShareAsync(key));
        var footer = new VerticalStackLayout { Spacing = 12, Padding = 16, Children =
        {
            status, copy, share, SettingsControls.Text("Use Save Image in the share sheet to add a copy to Photos.", 13, secondary: true)
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        layout.Add(photo); layout.Add(footer, 0, 1); Content = layout;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return; busy = true;
        try { await action(); }
        catch (Exception ex) { status.Text = ex is IOException ? ex.Message : "The photo action could not finish. Try again."; }
        finally { busy = false; }
    }
}
