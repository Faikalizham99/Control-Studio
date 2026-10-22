using ControlStudio.App.Views;
using ControlStudio.Core;
using ControlStudio.Core.Actions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class DisabledActionTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static Color Fill(VisualElement view) => Assert.IsType<SolidPaint>(((IView)view).Background).Color;
    private static SecondaryActionSurface Surface(ContentView button) => Assert.Single(Assert.IsType<Grid>(button.Content).Children.OfType<SecondaryActionSurface>());

    [Fact]
    public void DisabledPageActionKeepsReadableTextAndOutlineAcrossLiveThemesAndAccentChanges()
    {
        var button = new PageActionButton("Arrange", "Arrange") { DisabledReason = "Add another photo to arrange." };
        host.Attach(button); button.IsEnabled = false;
        var surface = Surface(button); var label = Assert.Single(Assert.IsType<HorizontalStackLayout>(surface.Content).Children.OfType<Label>());
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Dark })
        {
            host.App.UserAppTheme = theme;
            foreach (var accent in AppearanceValues.Presets.Concat(new[] { "#000000", "#FFFFFF", "#00FFD2", "#FFFF00" }))
            {
                host.App.Resources["AccentText"] = AppearanceColors.TextAccent(Color.FromArgb(accent), theme == AppTheme.Dark);
                Assert.Equal(1, button.Opacity); Assert.Equal(1, surface.Opacity); Assert.Equal(1, label.Opacity);
                Assert.Equal(theme == AppTheme.Dark ? "#BCC4D2" : "#515A6B", AppearanceColors.ToRgbHex(label.TextColor));
                Assert.True(AppearanceColors.Contrast(Fill(surface), label.TextColor) >= 4.5);
                var stroke = Assert.IsType<SolidColorBrush>(surface.Stroke).Color;
                Assert.True(AppearanceColors.Contrast(stroke, Fill(surface)) >= 3);
            }
        }
    }

    [Fact]
    public void ReenablingRestoresLiveAccentAndDisabledActionsNeverDispatchClicks()
    {
        var button = new PageActionButton("Arrange", "Arrange") { DisabledReason = "Add another photo to arrange." };
        host.Attach(button); var calls = 0; button.Clicked += (_, _) => calls++;
        var grid = Assert.IsType<Grid>(button.Content); var tap = Assert.Single(grid.Children.OfType<Button>()); var surface = Surface(button);
        Assert.True(grid.Children.IndexOf(tap) < grid.Children.IndexOf(surface)); Assert.True(surface.InputTransparent);
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        {
            host.App.UserAppTheme = theme;
            foreach (var accent in new[] { "#00FFD2", "#007AFF", "#FF9500" })
            {
                button.IsEnabled = false; Assert.False(tap.IsEnabled);
                ((IButtonController)tap).SendClicked(); Assert.Equal(0, calls);
                Assert.Equal(button.DisabledReason, SemanticProperties.GetHint(tap));
                var color = AppearanceColors.TextAccent(Color.FromArgb(accent), theme == AppTheme.Dark); host.App.Resources["AccentText"] = color;
                button.IsEnabled = true; Assert.True(tap.IsEnabled); Assert.Equal(color, surface.Foreground);
                Assert.True(AppearanceColors.Contrast(Fill(surface), surface.Foreground) >= 4.5);
                Assert.Equal("", SemanticProperties.GetHint(tap));
            }
        }
        ((IButtonController)tap).SendClicked(); Assert.Equal(1, calls);
    }

    [Fact]
    public void AncestorDisablingUpdatesGlyphsAndHintsAndClearsPressedScale()
    {
        var button = new PageActionButton("", "Back", ProfileSymbol.Back) { DisabledReason = "Please wait." };
        var parent = new Grid { Children = { button } }; host.Attach(parent);
        var grid = Assert.IsType<Grid>(button.Content); var tap = Assert.Single(grid.Children.OfType<Button>()); var surface = Surface(button);
        var glyph = Assert.Single(Assert.IsType<HorizontalStackLayout>(surface.Content).Children.OfType<ProfileGlyph>());
        ((IButtonController)tap).SendPressed(); Assert.Equal(.97, surface.Scale);
        parent.IsEnabled = false; Assert.False(button.IsEnabled); Assert.False(tap.IsEnabled); Assert.Equal(1, surface.Scale);
        Assert.Equal(surface.Foreground, glyph.Tint); Assert.Equal("Please wait.", SemanticProperties.GetHint(tap));
        parent.IsEnabled = true; Assert.True(button.IsEnabled); Assert.Equal("", SemanticProperties.GetHint(tap));
        Assert.True(button.MinimumHeightRequest >= 44); Assert.True(button.MinimumWidthRequest >= 44);
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void SecondaryEditorAndProfileActionsUseTheSameReadableDisabledPalette(AppTheme theme)
    {
        host.App.UserAppTheme = theme;
        var editor = new ClipboardEditorActionButton("Paste", new StudioGlyph(StudioSymbol.Clipboard), "Paste");
        var profile = new ProfileActionButton("Remove photo", ProfileSymbol.Trash, "Remove", primary: false);
        var parent = new VerticalStackLayout { Children = { editor, profile } }; host.Attach(parent);
        foreach (var button in new ContentView[] { editor, profile })
        {
            button.IsEnabled = false; var surface = Surface(button);
            var caption = Assert.IsType<HorizontalStackLayout>(surface.Content); var label = Assert.Single(caption.Children.OfType<Label>());
            Assert.Equal(1, button.Opacity); Assert.True(AppearanceColors.Contrast(Fill(surface), label.TextColor) >= 4.5);
            Assert.All(caption.Children.OfType<StudioGlyph>(), glyph => Assert.Equal(label.TextColor, glyph.Tint));
            Assert.All(caption.Children.OfType<ProfileGlyph>(), glyph => Assert.Equal(label.TextColor, glyph.Tint));
        }
    }

    [Fact]
    public void ActionBarsHaveFlexibleTitlesAndVisibleThemeAwareText()
    {
        var clipboard = Assert.IsType<Grid>(PageActionBar.Clipboard(new("", "Back", ProfileSymbol.Back), new("Arrange", "Arrange"), new("Add", "Add")));
        var modal = Assert.IsType<Grid>(PageActionBar.Modal("Arrange photos", new("Cancel", "Cancel"), new("Done", "Done")));
        host.Attach(new VerticalStackLayout { Children = { clipboard, modal } });
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Dark })
        {
            host.App.UserAppTheme = theme;
            foreach (var bar in new[] { clipboard, modal })
            {
                Assert.True(bar.ColumnDefinitions[1].Width.IsStar);
                var title = Assert.Single(bar.Children.OfType<Label>());
                Assert.Equal(1, title.MaxLines); Assert.Equal(LineBreakMode.TailTruncation, title.LineBreakMode);
                Assert.True(AppearanceColors.Contrast(bar.BackgroundColor, title.TextColor) >= 4.5);
            }
        }
    }

    public void Dispose() => host.Dispose();
}

public sealed class ClipboardArrangeAvailabilityTests
{
    [Theory]
    [InlineData(0, "text item")]
    [InlineData(1, "photo")]
    [InlineData(2, "link")]
    public void AvailabilityExplainsMissingItemsAndPinGroupBoundaries(int kindValue, string noun)
    {
        var kind = (ClipboardKind)kindValue;
        ActionDefinition Item(bool pinned = false) => StudioContent.Draft(null, kind, "Name", kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : kind == ClipboardKind.Link ? "https://example.com" : "text") with { IsFavorite = pinned };
        var first = Item(); var pinned = Item(true);
        var empty = ClipboardArrangeAvailability.For([], kind); Assert.False(empty.CanArrange); Assert.Equal($"Add at least two {noun}s to arrange.", empty.Reason);
        var single = ClipboardArrangeAvailability.For(new[] { first }, kind); Assert.False(single.CanArrange); Assert.Equal($"Add another {noun} to arrange.", single.Reason);
        var split = ClipboardArrangeAvailability.For(new[] { first, pinned }, kind); Assert.False(split.CanArrange); Assert.Contains("Pin or unpin", split.Reason);
        foreach (var items in new[] { new[] { first, Item() }, new[] { pinned, Item(true) }, new[] { first, pinned, Item() } })
        {
            var result = ClipboardArrangeAvailability.For(items, kind); Assert.True(result.CanArrange); Assert.Equal("", result.Reason);
        }
        var counter = new ActionDefinition(Guid.NewGuid(), "Counter", new IncrementCounterParameters());
        Assert.False(ClipboardArrangeAvailability.For(new[] { first, counter }, kind).CanArrange);
    }
}
