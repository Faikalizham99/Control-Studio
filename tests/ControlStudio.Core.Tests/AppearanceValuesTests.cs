using ControlStudio.Core;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class AppearanceValuesTests
{
    [Theory]
    [InlineData("#007aff", "#007AFF")]
    [InlineData("ff9500", "#FF9500")]
    [InlineData("  #aabbcc  ", "#AABBCC")]
    [InlineData("#FFFFFF", "#FFFFFF")]
    public void NormalizesUserEnteredRgb(string input, string expected)
    {
        Assert.True(AppearanceValues.TryNormalizeHex(input, out var hex));
        Assert.Equal(expected, hex);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("#FFF")] [InlineData("#GGHHII")] [InlineData("#80007AFF")] [InlineData("red")] [InlineData("#00 7FF")]
    public void RejectsUnsupportedOrMalformedColors(string? input) => Assert.False(AppearanceValues.TryNormalizeHex(input, out _));
    [Fact]
    public void LuminanceSupportsContrastCalculation()
    {
        Assert.Equal(0, AppearanceValues.Luminance("#000000"));
        Assert.Equal(1, AppearanceValues.Luminance("#FFFFFF"), 6);
        Assert.InRange(AppearanceValues.Luminance("#007AFF"), .20, .22);
        Assert.Throws<ArgumentException>(() => AppearanceValues.Luminance("#XYZ"));
    }
    [Fact]
    public void LocalProfileNameIsBoundedAndSupportsUnicode()
    {
        Assert.True(AppearanceValues.TryNormalizeName("  Faikal 🐱  ", out var name)); Assert.Equal("Faikal 🐱", name);
        foreach (var invalid in new[] { "", "  ", "A\0B", new string('x', 41) }) Assert.False(AppearanceValues.TryNormalizeName(invalid, out _));
    }
}
