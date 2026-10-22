using Xunit;

namespace ControlStudio.App.Tests;

public sealed class PhotoImportInputTests
{
    [Fact]
    public async Task ChunkedImageInputPreservesExactEncodedBytesAndLeavesSourceOpen()
    {
        var original = Enumerable.Range(0, 25_000).Select(i => (byte)(i % 256)).ToArray();
        using var stream = new ChunkedStream(original);
        Assert.Equal(original, await PhotoImportInput.ReadAsync(stream));
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task OversizedStreamAndEmptyStreamAreRejected()
    {
        using var oversized = new MemoryStream(new byte[PhotoImportInput.MaximumSourceBytes + 1]);
        await Assert.ThrowsAsync<IOException>(() => PhotoImportInput.ReadAsync(oversized));
        using var empty = new MemoryStream();
        await Assert.ThrowsAsync<IOException>(() => PhotoImportInput.ReadAsync(empty));
    }

    [Fact]
    public void NativeDataLengthIsCheckedBeforeMakingAManagedCopy()
    {
        PhotoImportInput.ValidateLength(1); PhotoImportInput.ValidateLength(PhotoImportInput.MaximumSourceBytes);
        Assert.Throws<IOException>(() => PhotoImportInput.ValidateLength(0));
        Assert.Throws<IOException>(() => PhotoImportInput.ValidateLength(PhotoImportInput.MaximumSourceBytes + 1));
        Assert.Throws<IOException>(() => PhotoImportInput.ValidateLength(ulong.MaxValue));
    }

    [Fact]
    public async Task CancelledInputDoesNotContinueReading()
    {
        using var input = new MemoryStream(new byte[20]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PhotoImportInput.ReadAsync(input, new CancellationToken(true)));
        Assert.Equal(0, input.Position);
    }

    private sealed class ChunkedStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 7)], cancellationToken);
    }
}
