using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class PhotoObjectRemovalTests
{
    private static byte[] Image(int width, int height) => Enumerable.Range(0, width * height).SelectMany(_ => new byte[] { 20, 40, 60, 255 }).ToArray();
    [Fact]
    public void Prediction_fills_only_selection_preserves_alpha_and_feathers_edges()
    {
        var pixels = Image(3, 2); pixels[11] = 128; pixels[8] = 10; pixels[9] = 20; pixels[10] = 30;
        pixels[23] = 0; pixels[20] = pixels[21] = pixels[22] = 0;
        var selection = new byte[] { 0, 255, 128, 0, 0, 255 };
        var plan = PhotoObjectRemoval.Create(3, 2, selection);
        var output = plan.Composite(pixels, selection, Enumerable.Repeat(1f, 3 * 512 * 512).ToArray());
        Assert.Equal(pixels[..4], output[..4]); Assert.Equal(pixels[12..20], output[12..20]);
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, output[4..8]);
        Assert.InRange(output[8], (byte)65, (byte)75); Assert.Equal((byte)128, output[11]);
        Assert.Equal(pixels[20..24], output[20..24]); Assert.Equal((byte)20, pixels[0]);
    }
    [Fact]
    public void Context_crop_keeps_square_proportions_and_model_mask_survives_thin_strokes()
    {
        var selection = new byte[1024 * 256]; selection[1023] = 255;
        var plan = PhotoObjectRemoval.Create(1024, 256, selection);
        var input = plan.Input(Image(1024, 256), selection);
        Assert.Equal(4 * 512 * 512, input.Length); Assert.True(plan.Left <= 1023 && plan.Left + plan.Edge > 1023);
        Assert.Contains(1f, input[(3 * 512 * 512)..]);
        for (var i = 0; i < 512 * 512; i++) if (input[3 * 512 * 512 + i] == 1)
            Assert.Equal(1f, input[i]);
    }
    [Fact]
    public void Edge_padding_does_not_reintroduce_selected_object_as_known_context()
    {
        var pixels = Image(2, 2); var selected = new byte[] { 255, 0, 0, 0 };
        var input = PhotoObjectRemoval.Create(2, 2, selected).Input(pixels, selected);
        var plane = PhotoObjectRemoval.ModelEdge * PhotoObjectRemoval.ModelEdge;
        // Top-left padding repeats the top-left image pixel. It must repeat the
        // mask as well, otherwise the erased object leaks into known context.
        Assert.Equal(1f, input[3 * plane]); Assert.Equal(1f, input[0]);
        Assert.Equal(0f, input[4 * plane - 1]);
    }
    [Fact]
    public void Invalid_empty_nonfinite_and_cancelled_work_never_changes_source()
    {
        Assert.Throws<IOException>(() => PhotoObjectRemoval.Create(2, 2, new byte[4]));
        Assert.Throws<IOException>(() => PhotoObjectRemoval.Create(2, 2, new byte[3]));
        Assert.Throws<IOException>(() => PhotoObjectRemoval.Create(2, 2, new byte[] { 255, 255, 255, 255 }));
        var selection = new byte[] { 255, 0, 0, 0 }; var pixels = Image(2, 2); var before = pixels.ToArray();
        var plan = PhotoObjectRemoval.Create(2, 2, selection);
        var prediction = new float[3 * 512 * 512]; prediction[2] = float.NaN;
        Assert.Throws<IOException>(() => plan.Composite(pixels, selection, prediction));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => plan.Input(pixels, selection, cancel.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => plan.Composite(pixels, selection, new float[3 * 512 * 512], cancel.Token));
        Assert.Equal(before, pixels);
    }
    [Fact]
    public void Unified_history_restores_repairs_transparency_and_selection_in_order()
    {
        var original = Image(4, 4); var session = new PhotoEditSession(4, 4, original);
        session.BeginStroke(false); session.Paint(.5, .5, 1, 0, false); session.EndStroke();
        var mask = session.Mask.Pixels.ToArray(); Assert.True(session.HasChanges);
        session.BeginStroke(true); session.Paint(2.5, 2.5, 1, 0, true); session.EndStroke();
        var selected = session.Selection.Pixels.ToArray();
        var repaired = session.Pixels.ToArray();
        for (var i = 0; i < selected.Length; i++) if (selected[i] != 0) repaired[i * 4] = 150;
        session.AcceptRepair(repaired); Assert.True(session.HasRepairs); Assert.False(session.HasSelection);
        session.Undo(); Assert.False(session.HasRepairs); Assert.Equal(selected, session.Selection.Pixels); Assert.Equal(mask, session.Mask.Pixels);
        session.Redo(); Assert.True(session.HasRepairs); Assert.False(session.HasSelection);
        session.Reset(); Assert.False(session.HasRepairs); Assert.All(session.Mask.Pixels, x => Assert.Equal((byte)255, x));
        session.Undo(); Assert.True(session.HasRepairs); Assert.Equal(mask, session.Mask.Pixels);
        Assert.Equal(Image(4, 4), original);
    }
    [Fact]
    public void Selection_alone_does_not_change_saved_photo_and_cancelled_strokes_are_discarded()
    {
        var original = Image(3, 3); var session = new PhotoEditSession(3, 3, original);
        session.Reset(); Assert.False(session.CanUndo); Assert.False(session.HasChanges);
        session.BeginStroke(true); session.Paint(1.5, 1.5, 1, 0, true); session.EndStroke(true);
        Assert.False(session.HasSelection); Assert.False(session.CanUndo);
        session.BeginStroke(true); session.Paint(1.5, 1.5, 1, 0, true); session.EndStroke();
        Assert.True(session.HasSelection); Assert.False(session.HasChanges);
        var invalid = original.ToArray(); invalid[0] = 250;
        Assert.Throws<IOException>(() => session.AcceptRepair(invalid)); Assert.Equal(original, session.Pixels);
        session.ClearSelection(); session.Undo(); Assert.True(session.HasSelection);
        session.Undo(); Assert.False(session.HasSelection); Assert.False(session.HasChanges);
    }
    [Fact]
    public void Reopened_repaired_source_is_preserved_until_reset_and_new_edits_clear_redo()
    {
        var original = Image(3, 3); var working = original.ToArray(); working[0] = 100;
        var session = new PhotoEditSession(3, 3, original, working: working);
        Assert.True(session.HasRepairs); Assert.False(session.HasChanges);
        session.Reset(); Assert.True(session.HasChanges); session.Undo(); Assert.Equal(working, session.Pixels);
        session.BeginStroke(false); session.Paint(2.5, 2.5, 1, 0, false); session.EndStroke(); Assert.False(session.CanRedo);
        working[0] = 200; Assert.Equal((byte)100, session.Pixels[0]);
    }
    [Fact]
    public void Unified_history_is_bounded_and_undo_redo_retain_completed_selections()
    {
        var session = new PhotoEditSession(64, 1, Image(64, 1));
        for (var i = 0; i < 40; i++)
        { session.BeginStroke(true); session.Paint(i + .5, .5, .5, 0, true); session.EndStroke(); }
        var completed = session.Selection.Pixels.ToArray(); var count = 0;
        while (session.CanUndo) { session.Undo(); count++; }
        Assert.Equal(PhotoCutoutMask.MaximumSteps, count);
        while (session.CanRedo) session.Redo();
        Assert.Equal(completed, session.Selection.Pixels);
    }
}
