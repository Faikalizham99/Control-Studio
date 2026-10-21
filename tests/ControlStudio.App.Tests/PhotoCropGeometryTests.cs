using Microsoft.Maui.Graphics;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class PhotoCropGeometryTests
{
    [Theory]
    [InlineData(1200, 800, .375, 75, 0, 200, 0, 800)]
    [InlineData(800, 1200, .375, 0, 75, 0, 200, 800)]
    [InlineData(800, 800, .375, 0, 0, 0, 0, 800)]
    [InlineData(1200, 800, .75, 300, 150, 400, 200, 400)]
    public void MatchesVisibleCropInSourcePixels(double width, double height, double zoom, double offsetX, double offsetY, double x, double y, double side)
    {
        Assert.Equal(new Rect(x, y, side, side), PhotoCropGeometry.SourceSquare(width, height, 300, zoom, offsetX, offsetY));
    }

    [Fact]
    public void ClampsBouncingOffsetsAndOversizedCropToImage()
    {
        Assert.Equal(new Rect(0, 0, 800, 800), PhotoCropGeometry.SourceSquare(800, 1200, 300, .1, -30, -50));
        Assert.Equal(new Rect(600, 1000, 200, 200), PhotoCropGeometry.SourceSquare(800, 1200, 300, 1.5, 9000, 9000));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidCropInputs(double invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoCropGeometry.SourceSquare(invalid, 800, 300, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotoCropGeometry.SourceSquare(800, 800, 300, invalid, 0, 0));
    }
}
