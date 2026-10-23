using ControlStudio.App.Views;
using ControlStudio.Core;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[CollectionDefinition("MAUI controls", DisableParallelization = true)]
public sealed class MauiControlsCollection;

[Collection("MAUI controls")]
public sealed class AccentControlTests : IDisposable
{
    private readonly MauiTestContext context = new();
    private Application app => context.App;
    private static IEnumerable<string> Accents => AppearanceValues.Presets.Concat(["#00FFD2", "#000000", "#FFFFFF", "#FFFF00", "#AABBCC"]);

    [Fact]
    public void ReproducesReleasedGradientOverridingAccentBackgroundColor()
    {
        var old = ClipboardEditorControls.Surface(new Label(), 25, 0);
        old.RemoveBinding(VisualElement.BackgroundProperty); old.ClearValue(VisualElement.BackgroundProperty);
        old.SetDynamicResource(VisualElement.BackgroundColorProperty, "AccentButton");
        old.Resources["AccentButton"] = Color.FromArgb("#00FFD2");
        Assert.Equal("#00FFD2", AppearanceColors.ToRgbHex(old.BackgroundColor));
        // Removing the binding does not remove its lower-specificity brush value.
        Assert.IsType<LinearGradientPaint>(((IView)old).Background);
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void SaveItemKeepsExactAccentAndReadableCaptionAcrossLiveChanges(AppTheme theme)
    {
        app.UserAppTheme = theme;
        var glyph = new ProfileGlyph(ProfileSymbol.Check, "AccentActionText");
        var button = new ClipboardEditorActionButton("Save item", glyph, "SaveClipboardItem", primary: true);
        var surface = Surface(button);
        var caption = Assert.Single(Assert.IsType<HorizontalStackLayout>(surface.Content).Children.OfType<Label>());
        foreach (var hex in Accents)
        {
            ApplyAndSwitchTheme(surface, hex, theme);
            AssertCaption(surface, caption.TextColor, 4.5); Assert.Equal(caption.TextColor, glyph.Tint);
        }
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void ProfilePrimaryActionsUseTheSameSafeFillAndContrast(AppTheme theme)
    {
        app.UserAppTheme = theme;
        foreach (var text in new[] { "Save profile", "Choose photo" })
        {
            var button = new ProfileActionButton(text, ProfileSymbol.Check, "ProfileAction");
            var surface = Surface(button);
            var caption = Assert.IsType<HorizontalStackLayout>(surface.Content);
            var label = Assert.Single(caption.Children.OfType<Label>()); var glyph = Assert.Single(caption.Children.OfType<ProfileGlyph>());
            foreach (var hex in Accents)
            {
                ApplyAndSwitchTheme(surface, hex, theme);
                AssertCaption(surface, label.TextColor, 3); Assert.Equal(label.TextColor, glyph.Tint);
            }
        }
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void CopyIconAndCompactPreviewRetainStrictContrast(AppTheme theme)
    {
        app.UserAppTheme = theme;
        var copy = Surface(new ClipboardIconButton(StudioSymbol.Clipboard, "Copy", primary: true));
        var glyph = Assert.IsType<StudioGlyph>(copy.Content);
        var star = new Label { Text = "Star", FontSize = 23 };
        var preview = new AccentSurface(star, 10, captionSize: 23, captionBold: false);
        preview.BindForeground(star, Label.TextColorProperty);
        foreach (var hex in Accents)
        {
            ApplyAndSwitchTheme(copy, hex, theme); AssertCaption(copy, glyph.Tint, 4.5);
            ApplyAndSwitchTheme(preview, hex, theme); AssertCaption(preview, star.TextColor, 4.5);
        }
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void NativeAccentButtonsKeepTheirFillAndRespondToCaptionSize(AppTheme theme)
    {
        app.UserAppTheme = theme;
        var button = new AccentButton { Text = "Action" };
        foreach (var hex in Accents)
        {
            button.Resources["AccentButton"] = Color.FromArgb(hex);
            app.UserAppTheme = theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light; app.UserAppTheme = theme;
            Assert.Equal(hex, AppearanceColors.ToRgbHex(Fill(button)));
            AssertCaption(button, button.TextColor, 3);
            button.FontSize = 12; AssertCaption(button, button.TextColor, 4.5);
            button.FontSize = 19; AssertCaption(button, button.TextColor, 3);
        }
    }

    [Fact]
    public void HeaderOverrideCanReturnToLiveAppAccentAfterRecycling()
    {
        var header = new ClipboardHeaderView();
        header.Resources["Accent"] = Color.FromArgb("#00FFD2"); header.Resources["AccentActionText"] = Colors.Black;
        header.SetItem("Default", null); Assert.Equal("#00FFD2", AppearanceColors.ToRgbHex(Fill(header)));
        header.SetItem("Custom", "#007AFF"); Assert.Equal("#007AFF", AppearanceColors.ToRgbHex(Fill(header)));
        header.SetItem("Default again", null); Assert.Equal("#00FFD2", AppearanceColors.ToRgbHex(Fill(header)));
        header.Resources["Accent"] = Colors.White; header.Resources["AccentActionText"] = Colors.Black;
        Assert.Equal("#FFFFFF", AppearanceColors.ToRgbHex(Fill(header)));
        AssertCaption(header, Assert.IsType<Label>(header.Content).TextColor, 3);
    }

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void ClipboardSelectionCanReturnFromNeutralToLiveAccent(AppTheme theme)
    {
        app.UserAppTheme = theme;
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            var segment = new ClipboardSegment(kind, StudioSymbol.Text, () => { });
            var surface = Assert.Single(Assert.IsType<Grid>(segment.Content).Children.OfType<Border>());
            var caption = Assert.IsType<VerticalStackLayout>(surface.Content);
            var label = Assert.Single(caption.Children.OfType<Label>()); var icon = Assert.Single(caption.Children.OfType<StudioGlyph>());
            var accent = Color.FromArgb("#00FFD2");
            surface.Resources["AccentSelectionBrush"] = new SolidColorBrush(accent.WithAlpha(.12f));
            surface.Resources["AccentSelectionStroke"] = new SolidColorBrush(accent);
            label.Resources["AccentText"] = accent; icon.Resources["AccentText"] = accent;
            for (var repeat = 0; repeat < 3; repeat++)
            {
                segment.Update(false); Assert.Equal(0f, Fill(surface).Alpha);
                segment.Update(true); Assert.Equal(.12f, Fill(surface).Alpha);
                Assert.Equal(accent, label.TextColor); Assert.Equal(accent, icon.Tint);
                app.UserAppTheme = theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
                Assert.Equal(accent, label.TextColor); Assert.Equal(accent, icon.Tint);
                app.UserAppTheme = theme;
            }
            var next = Color.FromArgb("#AF52DE");
            surface.Resources["AccentSelectionBrush"] = new SolidColorBrush(next.WithAlpha(.12f));
            label.Resources["AccentText"] = next; icon.Resources["AccentText"] = next;
            Assert.Equal(next, label.TextColor); Assert.Equal(next, icon.Tint);
            Assert.Equal(next.Red, Fill(surface).Red);
        }
    }

    [Fact]
    public void PrimaryPressAndDisabledFeedbackDoNotFadeTheFillOrCaption()
    {
        var glyph = new ProfileGlyph(ProfileSymbol.Check);
        var editor = new ClipboardEditorActionButton("Save item", glyph, "Save", primary: true);
        var profile = new ProfileActionButton("Save profile", ProfileSymbol.Check, "Profile");
        foreach (var button in new ContentView[] { editor, profile })
        {
            var grid = Assert.IsType<Grid>(button.Content); var surface = Surface(button);
            surface.Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromArgb("#00FFD2"));
            var hit = Assert.Single(grid.Children.OfType<Button>());
            Assert.True(grid.Children.IndexOf(hit) < grid.Children.IndexOf(surface)); Assert.True(surface.InputTransparent);
            ((IButtonController)hit).SendPressed(); Assert.Equal("#00FFD2", AppearanceColors.ToRgbHex(Fill(surface)));
            ((IButtonController)hit).SendReleased(); Assert.Equal(1d, surface.Scale);
            button.IsEnabled = false; Assert.Equal(1d, button.Opacity);
            var label = Assert.Single(Assert.IsType<HorizontalStackLayout>(surface.Content).Children.OfType<Label>());
            AssertCaption(surface, label.TextColor, 3);
            button.IsEnabled = true; AssertCaption(surface, label.TextColor, 3);
        }
    }

    [Fact]
    public void ForegroundTracksActualBrushMutationWithoutDependingOnAnUnrelatedResource()
    {
        var label = new Label(); var surface = new AccentSurface(label, 20);
        surface.BindForeground(label, Label.TextColorProperty);
        var fill = new SolidColorBrush(Colors.White); surface.Resources["AccentButtonBrush"] = fill;
        Assert.Equal(Colors.Black, label.TextColor);
        fill.Color = Colors.Black; Assert.Equal(Colors.White, label.TextColor);
        AssertCaption(surface, label.TextColor, 3);
    }

    private void ApplyAndSwitchTheme(AccentSurface surface, string hex, AppTheme theme)
    {
        surface.Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromArgb(hex));
        Assert.Equal(hex, AppearanceColors.ToRgbHex(Fill(surface)));
        app.UserAppTheme = theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Assert.Equal(hex, AppearanceColors.ToRgbHex(Fill(surface)));
        app.UserAppTheme = theme; Assert.Equal(hex, AppearanceColors.ToRgbHex(Fill(surface)));
    }
    private static AccentSurface Surface(ContentView button) => Assert.Single(Assert.IsType<Grid>(button.Content).Children.OfType<AccentSurface>());
    private static Color Fill(VisualElement view) => Assert.IsType<SolidPaint>(((IView)view).Background).Color;
    private static void AssertCaption(VisualElement view, Color foreground, double minimum)
    {
        var fill = Fill(view); Assert.Equal(1f, fill.Alpha);
        Assert.True(AppearanceColors.Contrast(fill, foreground) >= minimum, $"Foreground must remain readable against {AppearanceColors.ToRgbHex(fill)}.");
    }
    public void Dispose() => context.Dispose();
}
