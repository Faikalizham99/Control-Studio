using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetRefreshButtonTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static IEnumerable<Element> Descendants(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in Descendants(child)) yield return value;
    }
    [Fact]
    public void RefreshProgressBlocksDuplicateTapsAndRestoresTheAccessibleAccentAction()
    {
        var refresh = new WidgetRefreshButton(); host.Attach(refresh);
        var tap = Assert.Single(Descendants(refresh).OfType<Button>());
        var progress = Assert.Single(Descendants(refresh).OfType<ActivityIndicator>());
        var icon = Assert.Single(Descendants(refresh).OfType<StudioGlyph>());
        Assert.Equal(48, refresh.WidthRequest); Assert.Equal(48, refresh.HeightRequest);
        Assert.Equal("RefreshWidgetDesigns", tap.AutomationId); Assert.Equal("Refresh widgets", SemanticProperties.GetDescription(tap));
        Assert.False(progress.IsRunning); Assert.False(progress.IsVisible); Assert.True(icon.IsVisible);
        var calls = 0; refresh.Clicked += (_, _) => { calls++; refresh.IsRefreshing = true; };
        ((IButtonController)tap).SendClicked(); ((IButtonController)tap).SendClicked();
        Assert.Equal(1, calls); Assert.True(progress.IsRunning); Assert.True(progress.IsVisible); Assert.False(icon.IsVisible);
        Assert.Equal("Refreshing widgets", SemanticProperties.GetDescription(tap));
        host.App.Resources["AccentText"] = Colors.Purple; Assert.Equal(Colors.Purple, progress.Color);
        refresh.IsRefreshing = false; Assert.True(icon.IsVisible); Assert.Equal(Colors.Purple, icon.Tint);
        Assert.False(progress.IsRunning); Assert.False(progress.IsVisible); Assert.Equal("Refresh widgets", SemanticProperties.GetDescription(tap));
        refresh.IsEnabled = false; ((IButtonController)tap).SendClicked(); Assert.Equal(1, calls); Assert.False(tap.IsEnabled);
        refresh.IsEnabled = true; ((IButtonController)tap).SendClicked(); Assert.Equal(2, calls);
        refresh.IsRefreshing = false;
    }
    public void Dispose() => host.Dispose();
}
