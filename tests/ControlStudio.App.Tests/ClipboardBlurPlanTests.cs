using Xunit;

namespace ControlStudio.App.Tests;

public sealed class ClipboardBlurPlanTests
{
    [Theory]
    [InlineData(440, 30)]
    [InlineData(440, 180)]
    [InlineData(440, 170000)]
    [InlineData(1, 1000000)]
    [InlineData(1000000, 1)]
    [InlineData(1e200, 1e200)]
    public void ExpandedContentAlwaysHasABoundedFiniteRenderBudget(double width, double height)
    {
        var plan = Assert.IsType<ClipboardBlurPlan>(ClipboardBlurPlan.Create(width, height));
        Assert.InRange(plan.PixelWidth, 1, ClipboardBlurPlan.MaximumEdge); Assert.InRange(plan.PixelHeight, 1, ClipboardBlurPlan.MaximumEdge);
        Assert.InRange(plan.PixelWidth * plan.PixelHeight, 1, ClipboardBlurPlan.MaximumPixels);
        Assert.True(double.IsFinite(plan.Radius)); Assert.InRange(plan.Radius, .5, 14);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    [InlineData(-1, 50)]
    [InlineData(.01, 50)]
    [InlineData(double.NaN, 50)]
    [InlineData(50, double.PositiveInfinity)]
    public void UnmeasuredOrInvalidBoundsNeverStartARender(double width, double height) => Assert.Null(ClipboardBlurPlan.Create(width, height));

    [Fact]
    public void SmallCardsKeepTheirGeometryAndFourteenPointBlurStrength()
    {
        var plan = Assert.IsType<ClipboardBlurPlan>(ClipboardBlurPlan.Create(440, 180));
        Assert.Equal(440, plan.PixelWidth); Assert.Equal(180, plan.PixelHeight); Assert.Equal(14, plan.Radius);
        var large = Assert.IsType<ClipboardBlurPlan>(ClipboardBlurPlan.Create(440, 170000));
        Assert.InRange(Math.Abs((double)large.PixelWidth / large.PixelHeight - 440d / 170000), 0, 1d / large.PixelHeight);
    }

    [Fact]
    public void HideRevealAndRapidReversalStayContinuousMonotonicAndReachTheRequestedEndpoint()
    {
        var halfway = ClipboardBlurPlan.Amount(0, 1, ClipboardBlurPlan.Duration / 2);
        Assert.Equal(.5, halfway, precision: 10);
        foreach (var (from, to) in new[] { (0d, 1d), (1d, 0d), (halfway, 0d), (.2, 1d), (.9, .1) })
        {
            var previous = from; Assert.Equal(from, ClipboardBlurPlan.Amount(from, to, 0));
            for (var tick = 1; tick <= 100; tick++)
            {
                var amount = ClipboardBlurPlan.Amount(from, to, tick * ClipboardBlurPlan.Duration / 100);
                Assert.InRange(amount, Math.Min(from, to), Math.Max(from, to));
                Assert.True(to > from ? amount >= previous : amount <= previous);
                Assert.InRange(ClipboardBlurPlan.FrameIndex(amount), 0, ClipboardBlurPlan.AnimationFrames - 1); previous = amount;
            }
            Assert.Equal(to, ClipboardBlurPlan.Amount(from, to, ClipboardBlurPlan.Duration * 2), precision: 10);
        }
        Assert.Equal(0, ClipboardBlurPlan.FrameIndex(-1)); Assert.Equal(ClipboardBlurPlan.AnimationFrames - 1, ClipboardBlurPlan.FrameIndex(2));
        Assert.Equal(1, ClipboardBlurPlan.Amount(1, 1, 0));
    }
}
