using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class PhotoCutoutMaskTests
{
    [Fact]
    public void Stroke_is_one_undo_step_and_restore_is_reversible()
    {
        var mask = new PhotoCutoutMask(40, 30);
        mask.BeginStroke(); mask.Paint(3, 10, 3, 0, false); mask.Paint(35, 10, 3, 0, false);
        Assert.False(mask.CanUndo); mask.EndStroke(); var erased = mask.Pixels.ToArray();
        Assert.All(Enumerable.Range(3, 32), x => Assert.Equal(0, mask.Pixels[10 * 40 + x]));
        mask.Undo(); Assert.All(mask.Pixels, p => Assert.Equal(255, p));
        mask.Redo(); Assert.Equal(erased, mask.Pixels);
        mask.BeginStroke(); mask.Paint(15, 10, 2, 0, true); mask.EndStroke(); Assert.Equal(255, mask.Pixels[10 * 40 + 15]);
        mask.Undo(); Assert.Equal(erased, mask.Pixels);
    }
    [Fact]
    public void Cancel_noop_and_invalid_input_do_not_destroy_redo()
    {
        var mask = new PhotoCutoutMask(20, 20);
        mask.BeginStroke(); mask.Paint(10, 10, 4, .5, false); mask.EndStroke(); mask.Undo();
        mask.BeginStroke(); mask.Paint(1, 1, 4, 0, false); mask.EndStroke(cancel: true);
        Assert.True(mask.CanRedo); Assert.All(mask.Pixels, p => Assert.Equal(255, p));
        mask.BeginStroke(); mask.Paint(double.NaN, 2, 3, 0, false); mask.EndStroke(); Assert.True(mask.CanRedo);
    }
    [Fact]
    public void Soft_edges_are_partial_and_masks_are_copied()
    {
        var source = Enumerable.Repeat((byte)255, 400).ToArray(); var mask = new PhotoCutoutMask(20, 20, source);
        mask.BeginStroke(); mask.Paint(10.5, 10.5, 5, 1, false); mask.EndStroke();
        Assert.Equal(0, mask.Pixels[10 * 20 + 10]); Assert.InRange(mask.Pixels[10 * 20 + 13], (byte)1, (byte)254);
        Assert.All(source, p => Assert.Equal(255, p));
        var replace = new byte[400]; mask.Replace(replace); replace[0] = 45; Assert.Equal(0, mask.Pixels[0]);
    }
    [Fact]
    public void History_is_bounded_and_new_work_discards_redo()
    {
        var mask = new PhotoCutoutMask(10, 10);
        for (var i = 0; i < 50; i++) mask.Replace(Enumerable.Repeat((byte)i, 100).ToArray());
        var steps = 0; while (mask.CanUndo) { mask.Undo(); steps++; }
        Assert.Equal(PhotoCutoutMask.MaximumSteps, steps);
        mask.Replace(new byte[100]); Assert.False(mask.CanRedo);
    }
    [Theory]
    [InlineData(0, 2)] [InlineData(1025, 2)] [InlineData(20, -1)]
    public void Invalid_dimensions_are_rejected(int width, int height) => Assert.Throws<IOException>(() => new PhotoCutoutMask(width, height));
    [Fact]
    public void Detailed_masks_respect_memory_budget_while_undoing_and_redoing()
    {
        var mask = new PhotoCutoutMask(1024, 1024); var random = new Random(42); var pixels = new byte[1024 * 1024];
        for (var i = 0; i < 20; i++) { random.NextBytes(pixels); mask.Replace(pixels); }
        var expected = mask.Pixels.ToArray(); var undone = 0;
        while (mask.CanUndo) { mask.Undo(); undone++; }
        Assert.InRange(undone, 1, 16);
        var redone = 0; while (mask.CanRedo) { mask.Redo(); redone++; }
        Assert.Equal(undone, redone); Assert.Equal(expected, mask.Pixels);
    }
    [Fact]
    public void Masking_preserves_source_transparency_and_premultiplied_edges_without_changing_original()
    {
        byte[] original = [128, 64, 32, 128, 255, 0, 0, 255, 0, 0, 0, 0];
        Assert.Equal(new byte[] { 64, 32, 16, 64, 0, 0, 0, 0, 0, 0, 0, 0 }, PhotoCutoutPixels.Apply(original, [128, 0, 255]));
        Assert.Equal(original, PhotoCutoutPixels.Apply(original, [255, 255, 255]));
        Assert.Equal(128, original[3]); Assert.Equal(255, original[7]);
        Assert.Throws<IOException>(() => PhotoCutoutPixels.Apply(original, [255]));
    }
}
