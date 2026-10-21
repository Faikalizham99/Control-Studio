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

    private static double Contrast(Color a, Color b)
    {
        var first = AppearanceColors.Luminance(a); var second = AppearanceColors.Luminance(b);
        return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
}
