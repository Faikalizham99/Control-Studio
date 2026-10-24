using ControlStudio.App.Views;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetToolbarTests : IDisposable
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
    public void ToolbarHasOneScrollingLineAndSixFullSizeNativeTargets()
    {
        var symbols = new[] { StudioSymbol.Refresh, StudioSymbol.RotateLeft, StudioSymbol.Flip, StudioSymbol.Blend, StudioSymbol.Opacity, StudioSymbol.Delete };
        var buttons = symbols.Select((symbol, i) => new WidgetToolButton(symbol, "Tool " + i, destructive: i == 5)).ToArray();
        var strip = new WidgetHorizontalStrip(buttons, "Tools"); host.Attach(strip);
        var scroll = Assert.Single(Descendants(strip).OfType<ScrollView>()); Assert.Equal(ScrollOrientation.Horizontal, scroll.Orientation);
        var row = Assert.IsType<HorizontalStackLayout>(scroll.Content); Assert.Equal(6, row.Children.Count);
        Assert.Empty(Descendants(strip).OfType<Label>());
        foreach (var button in buttons)
        {
            Assert.Equal(48, button.WidthRequest); Assert.Equal(48, button.HeightRequest);
            Assert.Single(Descendants(button).OfType<Button>()); Assert.False(button.IsSelected);
        }
    }
    [Theory]
    [InlineData("#FFFFFF", AppTheme.Light)] [InlineData("#000000", AppTheme.Dark)] [InlineData("#00FFD2", AppTheme.Light)] [InlineData("#00FFD2", AppTheme.Dark)]
    public void ExpandedToolsFollowAccentAndRemainReadable(string hex, AppTheme theme)
    {
        host.App.UserAppTheme = theme; host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromArgb(hex));
        var tool = new WidgetToolButton(StudioSymbol.RotateLeft, "Rotate selected photo"); host.Attach(tool); tool.IsSelected = true;
        var surface = Assert.Single(Descendants(tool).OfType<AccentSurface>());
        Assert.True(surface.IsVisible); Assert.True(AppearanceColors.Contrast(surface.Foreground, Color.FromArgb(hex)) >= 4.5);
        var active = Assert.Single(Descendants(surface).OfType<StudioGlyph>()); Assert.Equal(surface.Foreground, active.Tint);
        var tap = Assert.Single(Descendants(tool).OfType<Button>()); Assert.Contains("expanded", SemanticProperties.GetDescription(tap));
        tool.Describe("Rotate", "Photo 2"); Assert.Contains("Photo 2", SemanticProperties.GetDescription(tap)); Assert.Contains("expanded", SemanticProperties.GetDescription(tap));
        tool.IsSelected = false; Assert.False(surface.IsVisible); Assert.DoesNotContain("expanded", SemanticProperties.GetDescription(tap));
    }
    [Fact]
    public void ClockDeleteUsesTheExistingDestructiveActionAndRespectsAvailabilityAndLockSemantics()
    {
        var controls = new WidgetClockControls(new ClockPicker());
        host.Attach(new VerticalStackLayout { Children = { controls.Toolbar, controls } });
        controls.Update(ControlStudio.Core.Widgets.WidgetClocks.New() with { Locked = true }); controls.SetAvailable(true);
        var button = Descendants(controls.Toolbar).OfType<Button>().Single(x => x.AutomationId == "ClockToolDelete");
        Assert.Contains("confirmation", SemanticProperties.GetHint(button));
        var deleted = 0; controls.DeleteRequested += (_, _) => deleted++;
        ((IButtonController)button).SendClicked(); Assert.Equal(1, deleted); Assert.Null(controls.ActiveTool);
        controls.SetAvailable(false); ((IButtonController)button).SendClicked(); Assert.Equal(1, deleted);
        controls.SetAvailable(true); controls.CanInteract = () => false; ((IButtonController)button).SendClicked(); Assert.Equal(1, deleted);
        controls.CanInteract = () => true; ((IButtonController)button).SendClicked(); Assert.Equal(2, deleted);
    }
    private sealed class ClockPicker : IColourPicker
    {
        public Task<string> PickAsync(string initialHex, Action<string> preview) => Task.FromResult(initialHex);
    }
    [Fact]
    public void DeleteRemainsDestructiveAndDisabledToolsIgnoreClicks()
    {
        var tool = new WidgetToolButton(StudioSymbol.Delete, "Delete selected photo", destructive: true); host.Attach(tool);
        var tap = Assert.Single(Descendants(tool).OfType<Button>()); var glyph = Assert.IsType<StudioGlyph>(Assert.IsType<Grid>(tool.Content).Children[1]);
        Assert.Equal(Color.FromArgb("#B42336"), glyph.Tint); host.App.UserAppTheme = AppTheme.Dark; Assert.Equal(Color.FromArgb("#FF8798"), glyph.Tint);
        Assert.Contains("confirmation", SemanticProperties.GetHint(tap));
        var calls = 0; tool.Clicked += (_, _) => calls++;
        tool.IsEnabled = false; Assert.False(tap.IsEnabled); ((IButtonController)tap).SendClicked(); Assert.Equal(0, calls);
        tool.IsEnabled = true; ((IButtonController)tap).SendClicked(); Assert.Equal(1, calls);
    }
    public void Dispose() => host.Dispose();
}
