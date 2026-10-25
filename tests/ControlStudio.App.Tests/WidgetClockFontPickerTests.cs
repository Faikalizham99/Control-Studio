using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetClockFontPickerTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var descendant in Descendants(child)) yield return descendant;
    }
    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void EveryChoiceShowsItsOwnFaceAndDigitsAndKeepsReadableAccentSelection(AppTheme theme)
    {
        host.App.UserAppTheme = theme;
        WidgetClockFontPicker? picker = null; var requests = 0;
        picker = new(font => { requests++; picker!.Update(font); }); host.Attach(picker);
        Assert.Equal(Enum.GetValues<WidgetClockFont>(), WidgetClockFonts.All);
        var buttons = Descendants(picker).OfType<Button>().Where(x => x.AutomationId?.StartsWith("ClockFont") == true).ToArray(); Assert.Equal(8, buttons.Length);
        foreach (var font in WidgetClockFonts.All)
        {
            var button = Assert.Single(buttons, x => x.AutomationId == "ClockFont" + (int)font);
            Assert.True(button.MinimumHeightRequest >= 48); Assert.True(button.MinimumWidthRequest >= 44);
            foreach (var label in Descendants(button.Parent).OfType<Label>())
            {
                Assert.Equal(WidgetClockFonts.Family(font), label.FontFamily);
                Assert.Equal(font is WidgetClockFont.System or WidgetClockFont.SfProRounded ? FontAttributes.Bold : FontAttributes.None, label.FontAttributes);
                Assert.Contains(label.Text, new[] { WidgetClockFonts.Title(font), "12:34:56" });
            }
            ((IButtonController)button).SendClicked(); Assert.Equal(font, picker.SelectedFont);
            Assert.Contains("selected", SemanticProperties.GetDescription(button));
            var surface = Descendants(button.Parent).OfType<AccentSurface>().Single(); Assert.True(surface.IsVisible);
            foreach (var hex in new[] { "#FFFFFF", "#000000", "#00FFD2", "#007AFF", "#F5D90A" })
            {
                var fill = Color.FromArgb(hex); surface.Resources["AccentButtonBrush"] = new SolidColorBrush(fill);
                foreach (var label in Descendants(surface).OfType<Label>())
                    Assert.True(ControlStudio.App.AppearanceColors.Contrast(fill, label.TextColor) >= 4.5);
            }
            Assert.Single(Descendants(picker).OfType<WidgetClockWeightPicker>(), x => x.IsVisible);
        }
        var count = requests; picker.IsEnabled = false;
        ((IButtonController)buttons[0]).SendClicked(); Assert.Equal(count, requests);
    }
    [Theory]
    [InlineData(WidgetClockFont.System, 6)] [InlineData(WidgetClockFont.SfProRounded, 6)]
    [InlineData(WidgetClockFont.Monospaced, 2)] [InlineData(WidgetClockFont.Serif, 2)]
    [InlineData(WidgetClockFont.Rounded, 0)] [InlineData(WidgetClockFont.RocknRollOne, 0)]
    [InlineData(WidgetClockFont.DelaGothicOne, 0)] [InlineData(WidgetClockFont.KaiseiDecol, 0)]
    public void WeightsAppearOnlyBelowTheSelectedFontWithActualSupportedSpecimens(WidgetClockFont font, int count)
    {
        var current = new WidgetClockOptions(Font: font); WidgetClockFontPicker? picker = null;
        picker = new(next => { current = WidgetClockWeights.ChangeFont(current, next); picker!.Update(current.Font, current.Weight); },
            weight => { current = current with { Weight = weight }; picker!.Update(current.Font, current.Weight); });
        host.Attach(picker); picker.Update(current.Font, current.Weight);
        var weights = Assert.Single(Descendants(picker).OfType<WidgetClockWeightPicker>(), x => x.IsVisible);
        var buttons = Descendants(weights).OfType<Button>().ToArray(); Assert.Equal(count, buttons.Length);
        foreach (var weight in WidgetClockWeights.Available(font))
        {
            if (count == 0) { Assert.Contains(Descendants(weights).OfType<Label>(), x => x.Text!.Contains("fixed for this font")); continue; }
            var button = Assert.Single(buttons, x => x.AutomationId == "ClockWeight" + (int)font + weight);
            ((IButtonController)button).SendClicked(); Assert.Equal(weight, WidgetClockWeights.Resolve(current));
            Assert.Equal(weight, weights.SelectedWeight); Assert.Contains("selected", SemanticProperties.GetDescription(button));
            foreach (var label in Descendants(button.Parent).OfType<WidgetClockFontLabel>())
            { Assert.Equal(font, label.Typeface.Font); Assert.Equal(weight, label.Typeface.Weight); }
            var selected = Descendants(button.Parent).OfType<AccentSurface>().Single(); Assert.True(selected.IsVisible);
            foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
            foreach (var hex in new[] { "#FFFFFF", "#000000", "#00FFD2", "#007AFF" })
            {
                host.App.UserAppTheme = theme; var fill = Color.FromArgb(hex);
                selected.Resources["AccentButtonBrush"] = new SolidColorBrush(fill);
                Assert.All(Descendants(selected).OfType<WidgetClockFontLabel>(), label => Assert.True(ControlStudio.App.AppearanceColors.Contrast(fill, label.TextColor) >= 4.5));
            }
            var fontRow = Descendants(picker).OfType<Button>().Single(x => x.AutomationId == "ClockFont" + (int)font).Parent;
            Assert.All(Descendants(fontRow).OfType<WidgetClockFontLabel>(), sample => Assert.Equal(weight, WidgetClockWeights.Resolve(sample.Typeface)));
        }
        var edits = current;
        picker.IsEnabled = false;
        foreach (var button in buttons) ((IButtonController)button).SendClicked(); Assert.Equal(edits, current);
    }
    public void Dispose() => host.Dispose();
}
