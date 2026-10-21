using ControlStudio.Core;
using Microsoft.Maui.Graphics;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class AppearanceColorsTests
{
    [Fact]
    public void ReproducesReleasedStartupExceptionWithPinnedMauiColor()
    {
        var accent = Color.FromArgb(AppearanceValues.DefaultAccent);
        Assert.Equal("#007AFF", accent.ToArgbHex());
        Assert.Throws<ArgumentException>(() => AppearanceValues.Luminance(accent.ToArgbHex()[3..]));
    }

    public static IEnumerable<object[]> Accents => AppearanceValues.Presets
        .Concat(new[] { "#000000", "#FFFFFF", "#FFFF00", "#AABBCC" })
        .Select(hex => new object[] { hex });

    [Theory]
    [MemberData(nameof(Accents))]
    public void StartupAccentChecksAcceptPresetsAndCustomColors(string hex)
    {
        var accent = Color.FromArgb(hex);
        Assert.Equal(hex, AppearanceColors.ToRgbHex(accent));
        Assert.Equal(AppearanceValues.Luminance(hex), AppearanceColors.Luminance(accent));
        var foreground = AppearanceColors.ButtonForeground(accent);
        Assert.True(Contrast(accent, foreground) >= 4.5);
        foreach (var dark in new[] { false, true })
            Assert.True(Contrast(AppearanceColors.TextAccent(accent, dark), Color.FromArgb(dark ? "#1C1E27" : "#F2F2F7")) >= 4.5);
    }

    [Theory]
    [InlineData(0f)] [InlineData(.5f)] [InlineData(1f)]
    public void PickerColorConversionDiscardsOnlyAlpha(float alpha)
    {
        var selected = Color.FromArgb("#007AFF").WithAlpha(alpha);
        var hex = AppearanceColors.ToRgbHex(selected);
        Assert.Equal("#007AFF", hex);
        Assert.True(AppearanceValues.TryNormalizeHex(hex, out _));
    }

    [Fact]
    public void EveryHueSliderPositionProducesAcceptedRgb()
    {
        for (var value = 0; value <= 360; value++)
        {
            var color = Color.FromHsv((float)((300 - value + 360) % 360) / 360, 1, 1);
            Assert.True(AppearanceValues.TryNormalizeHex(AppearanceColors.ToRgbHex(color), out _));
        }
    }

    [Theory]
    [InlineData("#007AFF", true)]
    [InlineData("#AF52DE", true)]
    [InlineData("#FF2D85", true)]
    [InlineData("#34C759", false)]
    [InlineData("#FF9500", false)]
    [InlineData("#00FFD2", false)]
    [InlineData("#000000", true)]
    [InlineData("#FFFFFF", false)]
    public void LargeBoldActionLabelsPreferWhiteOnlyWhenReadable(string hex, bool white)
    {
        var fill = Color.FromArgb(hex);
        var foreground = AppearanceColors.ButtonForeground(fill, 19, bold: true);
        Assert.Equal(white ? Colors.White : Colors.Black, foreground);
        Assert.True(Contrast(fill, foreground) >= 3);
        Assert.Equal(hex, AppearanceColors.ToRgbHex(fill));
    }

    [Fact]
    public void CompactPreviewAndSmallerFontsRetainStrictContrast()
    {
        var blue = Color.FromArgb("#007AFF");
        Assert.Equal(Colors.White, AppearanceColors.ButtonForeground(blue, 19, bold: true));
        foreach (var (size, bold) in new[] { (12d, true), (14d, true), (18d, true), (19d, false), (23d, false) })
        {
            var foreground = AppearanceColors.ButtonForeground(blue, size, bold);
            Assert.Equal(Colors.Black, foreground);
            Assert.True(Contrast(blue, foreground) >= 4.5);
        }
        Assert.Equal(Colors.White, AppearanceColors.ButtonForeground(blue, 24));
    }

    [Fact]
    public void CustomColorSamplesRespectBothLabelSizesWithoutChangingTheAccent()
    {
        for (var red = 0; red <= 255; red += 17)
        for (var green = 0; green <= 255; green += 17)
        for (var blue = 0; blue <= 255; blue += 17)
        {
            var fill = Color.FromRgb(red, green, blue);
            var before = AppearanceColors.ToRgbHex(fill);
            Assert.True(Contrast(fill, AppearanceColors.ButtonForeground(fill)) >= 4.5);
            Assert.True(Contrast(fill, AppearanceColors.ButtonForeground(fill, 19, bold: true)) >= 3);
            Assert.Equal(before, AppearanceColors.ToRgbHex(fill));
        }
    }

    private static double Contrast(Color a, Color b)
    {
        var first = AppearanceColors.Luminance(a); var second = AppearanceColors.Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
}
