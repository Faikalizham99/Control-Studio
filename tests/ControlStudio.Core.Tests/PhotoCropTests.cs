using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class PhotoCropTests
{
    [Fact]
    public void PixelCropCopiesExactRowsAndAlphaWithoutChangingTheSource()
    {
        var pixels = Enumerable.Range(0, 4 * 3 * 4).Select(x => (byte)x).ToArray(); var original = pixels.ToArray();
        var result = new PhotoCropRect(1, 1, 2, 2).CopyRgba(pixels, 4, 3);
        Assert.Equal(pixels[20..28].Concat(pixels[36..44]), result); Assert.Equal(original, pixels);
        Assert.Throws<IOException>(() => new PhotoCropRect(-1, 0, 2, 2).CopyRgba(pixels, 4, 3));
        Assert.Throws<IOException>(() => new PhotoCropRect(int.MaxValue, 0, 2, 2).Validate(4, 3));
        Assert.Throws<IOException>(() => new PhotoCropRect(0, 0, 2, 2).CopyRgba(new byte[3], 4, 3));
    }
    [Theory]
    [InlineData(800, 600)] [InlineData(600, 800)] [InlineData(1, 1024)] [InlineData(1024, 1)] [InlineData(1, 1)]
    public void AllAspectsAndHandlesStayInsideSourceAtExtremeDrags(int width, int height)
    {
        foreach (var aspect in Enum.GetValues<PhotoCropAspect>()) foreach (var handle in Enum.GetValues<PhotoCropHandle>())
        foreach (var delta in new[] { -10000d, -10, 0, 10, 10000 })
        {
            var session = new PhotoCropSession(width, height); session.SetAspect(aspect); var previous = session.Rect;
            session.Begin(); session.Drag(handle, delta, -delta); session.Rect.Validate(width, height); session.End();
            if (handle == PhotoCropHandle.Move) { Assert.Equal(previous.Width, session.Rect.Width); Assert.Equal(previous.Height, session.Rect.Height); }
            var ratio = aspect switch { PhotoCropAspect.Original => (double)width / height, PhotoCropAspect.Square => 1,
                PhotoCropAspect.FourThree => 4d / 3, PhotoCropAspect.SixteenNine => 16d / 9, _ => 0 };
            // Pixel quantization can differ by a source pixel, especially on 1px images.
            if (ratio > 0) Assert.True(Math.Abs(session.Rect.Width - session.Rect.Height * ratio) <= Math.Max(1.6, ratio));
        }
    }
    [Fact]
    public void GestureHistoryGroupsMovementCancelsAndRetainsRedoUntilAnActualEdit()
    {
        var session = new PhotoCropSession(400, 300); session.SetAspect(PhotoCropAspect.Square); var square = session.Rect;
        session.Begin(); for (var i = 0; i < 50; i++) session.Drag(PhotoCropHandle.TopLeft, i, i); session.End(); var cropped = session.Rect;
        session.Undo(); Assert.Equal(square, session.Rect); session.Redo(); Assert.Equal(cropped, session.Rect);
        session.Undo(); session.Begin(); session.Drag(PhotoCropHandle.Move, 10, 10); session.End(true); Assert.Equal(square, session.Rect); Assert.True(session.CanRedo);
        session.Reset(); Assert.Equal(PhotoCropRect.Full(400, 300), session.Rect); session.Undo(); Assert.Equal(square, session.Rect);
        session.Begin(); session.Drag(PhotoCropHandle.Move, double.NaN, 1); session.End(); Assert.Equal(square, session.Rect);
    }
    [Fact]
    public void ReopeningDoesNotRecropAndResetRestoresAllOriginalPixels()
    {
        var saved = new PhotoCropRect(10, 20, 300, 200); var session = new PhotoCropSession(800, 600, saved);
        Assert.False(session.HasChanges); Assert.Equal(saved, session.Rect);
        session.Reset(); Assert.True(session.HasChanges); Assert.Equal(PhotoCropRect.Full(800, 600), session.Rect);
        session.Undo(); Assert.False(session.HasChanges); Assert.Equal(saved, session.Rect);
        session.SetAspect(PhotoCropAspect.Free); Assert.False(session.CanUndo);
        Assert.Throws<IOException>(() => new PhotoCropSession(20, 20, saved));
    }
    [Fact]
    public void LockedCornerFollowsContinuousDiagonalMovementWithoutAnAxisSwitchJump()
    {
        var session = new PhotoCropSession(800, 600, new(100, 100, 200, 200)); session.SetAspect(PhotoCropAspect.Square); session.Begin();
        session.Drag(PhotoCropHandle.TopLeft, 10, -10); var first = session.Rect;
        session.Drag(PhotoCropHandle.TopLeft, 10, -11); var second = session.Rect;
        Assert.InRange(Math.Abs(first.Width - second.Width), 0, 2); Assert.InRange(Math.Abs(first.X - second.X), 0, 2);
        session.Drag(PhotoCropHandle.TopLeft, 0, 0); Assert.Equal(new PhotoCropRect(100, 100, 200, 200), session.Rect);
        session.End(); Assert.False(session.HasChanges);
    }
    [Fact]
    public void FreeEdgesKeepTheirOppositeAnchorAndHistoryIsBounded()
    {
        var initial = new PhotoCropRect(100, 80, 200, 120); var session = new PhotoCropSession(800, 600, initial);
        session.Begin(); session.Drag(PhotoCropHandle.TopLeft, 20, 30); session.End(); Assert.Equal(new PhotoCropRect(120, 110, 180, 90), session.Rect);
        session.Undo(); session.Begin(); session.Drag(PhotoCropHandle.BottomRight, -30, -20); session.End(); Assert.Equal(new PhotoCropRect(100, 80, 170, 100), session.Rect);
        for (var i = 0; i < 100; i++) { session.Begin(); session.Drag(PhotoCropHandle.Move, 1, 0); session.End(); }
        var steps = 0; while (session.CanUndo) { steps++; session.Undo(); } Assert.Equal(30, steps);
    }
    [Theory]
    [InlineData(PhotoCropAspect.Free)] [InlineData(PhotoCropAspect.Square)]
    public void FractionalCornerDragsKeepTheOppositePixelEdgesExactlyFixed(PhotoCropAspect aspect)
    {
        var initial = new PhotoCropRect(100, 100, 201, 201);
        var session = new PhotoCropSession(800, 600, initial); session.SetAspect(aspect); session.Begin();
        session.Drag(PhotoCropHandle.TopLeft, 11.5, 11.5);
        Assert.Equal(initial.X + initial.Width, session.Rect.X + session.Rect.Width);
        Assert.Equal(initial.Y + initial.Height, session.Rect.Y + session.Rect.Height);
        session.End(); session.Undo(); Assert.Equal(initial, session.Rect);
    }
    [Fact]
    public void SelectingTheSameRatioTwiceDoesNotTrimAnotherPixelOrCreateHistory()
    {
        var session = new PhotoCropSession(6, 5); session.SetAspect(PhotoCropAspect.FourThree); var crop = session.Rect;
        session.SetAspect(PhotoCropAspect.FourThree); Assert.Equal(crop, session.Rect);
        session.Undo(); Assert.Equal(PhotoCropRect.Full(6, 5), session.Rect); Assert.False(session.CanUndo);
    }
    [Fact]
    public void VariedFractionalCropSequencesKeepSourceBoundsAnchorsAndUndoConsistent()
    {
        var random = new Random(72148);
        for (var example = 0; example < 100; example++)
        {
            var width = random.Next(1, 1025); var height = random.Next(1, 1025);
            var x = random.Next(width); var y = random.Next(height);
            var session = new PhotoCropSession(width, height, new(x, y, random.Next(1, width - x + 1), random.Next(1, height - y + 1)));
            session.SetAspect((PhotoCropAspect)random.Next(5));
            for (var step = 0; step < 20; step++)
            {
                var before = session.Rect; var handle = (PhotoCropHandle)random.Next(9);
                session.Begin(); session.Drag(handle, Math.Round((random.NextDouble() * 4 - 2) * width * 2) / 2,
                    Math.Round((random.NextDouble() * 4 - 2) * height * 2) / 2); session.End(); var after = session.Rect;
                after.Validate(width, height);
                if (handle == PhotoCropHandle.Move) { Assert.Equal(before.Width, after.Width); Assert.Equal(before.Height, after.Height); }
                if (handle is PhotoCropHandle.Left or PhotoCropHandle.TopLeft or PhotoCropHandle.BottomLeft) Assert.Equal(before.X + before.Width, after.X + after.Width);
                if (handle is PhotoCropHandle.Right or PhotoCropHandle.TopRight or PhotoCropHandle.BottomRight) Assert.Equal(before.X, after.X);
                if (handle is PhotoCropHandle.Top or PhotoCropHandle.TopLeft or PhotoCropHandle.TopRight) Assert.Equal(before.Y + before.Height, after.Y + after.Height);
                if (handle is PhotoCropHandle.Bottom or PhotoCropHandle.BottomLeft or PhotoCropHandle.BottomRight) Assert.Equal(before.Y, after.Y);
                if (before != after) { session.Undo(); Assert.Equal(before, session.Rect); session.Redo(); Assert.Equal(after, session.Rect); }
            }
        }
    }
}
