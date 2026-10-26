using ControlStudio.App.Rendering;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class ImageRenderBatchTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public void IdentityBlendAndCropStayAliveUntilSnapshotAndNeverDisposeCachedInputs(bool blendReturnsInput, bool cropReturnsInput)
    {
        using var cached = new Image(); var batch = new ImageRenderBatch<Image>();
        var background = batch.Own(new Image()); batch.Borrow(cached);
        var blend = batch.Own(blendReturnsInput ? cached : new Image());
        var crop = batch.Own(cropReturnsInput ? blend : new Image());
        cached.Use(); background.Use(); blend.Use(); crop.Use();
        var snapshot = batch.Checkpoint(new Image()); snapshot.Use(); cached.Use();
        Assert.Equal(1, background.Disposals);
        if (!ReferenceEquals(blend, cached)) Assert.Equal(1, blend.Disposals);
        if (!ReferenceEquals(crop, cached)) Assert.Equal(1, crop.Disposals);
        Assert.Equal(0, cached.Disposals); Assert.Equal(0, snapshot.Disposals);
        var returned = batch.Detach(snapshot); batch.Dispose(); batch.Dispose(); returned.Use(); Assert.Equal(0, cached.Disposals);
        returned.Dispose(); Assert.Equal(1, returned.Disposals);
    }
    [Fact]
    public void CroppingTheBackgroundDoesNotDisposeItUntilTheIndependentSnapshot()
    {
        using var batch = new ImageRenderBatch<Image>(); var background = batch.Own(new Image());
        var blend = batch.Own(background); var crop = batch.Own(blend); crop.Use();
        var snapshot = batch.Checkpoint(new Image()); Assert.Equal(1, background.Disposals); snapshot.Use();
        var alsoSame = batch.Own(snapshot); batch.Checkpoint(alsoSame); snapshot.Use(); Assert.Equal(0, snapshot.Disposals);
    }
    [Fact]
    public void SeparateWrappersThatCompareEqualAreEachReleasedAndBorrowedCheckpointsAreRejected()
    {
        var first = new Image(); var second = new Image(); var cached = new Image();
        using (var batch = new ImageRenderBatch<Image>())
        {
            batch.Own(first); batch.Own(second); batch.Borrow(cached); batch.Own(cached);
            Assert.Throws<InvalidOperationException>(() => batch.Checkpoint(cached));
            Assert.Throws<InvalidOperationException>(() => batch.Detach(cached)); first.Use(); second.Use(); cached.Use();
        }
        Assert.Equal(1, first.Disposals); Assert.Equal(1, second.Disposals); Assert.Equal(0, cached.Disposals); cached.Dispose();
    }
    [Fact]
    public void FailedRenderingReleasesOwnedWrappersOnceAndDoesNotInvalidateTheCache()
    {
        var temporary = new Image(); var cached = new Image(); var batch = new ImageRenderBatch<Image>();
        Assert.Throws<IOException>((Action)(() =>
        {
            using (batch) { batch.Borrow(cached); batch.Own(temporary); batch.Own(temporary); throw new IOException("Native rendering failed"); }
        }));
        batch.Dispose(); Assert.Equal(1, temporary.Disposals); cached.Use();
        Assert.Throws<ObjectDisposedException>(() => batch.Own(new Image()));
        cached.Dispose();
    }
    [Theory]
    [InlineData(1)] [InlineData(4)] [InlineData(5)] [InlineData(8)] [InlineData(30)]
    public void RepeatedFourLayerCheckpointsAllowCacheEvictionAndOwnedReturnWithoutDeadReferences(int count)
    {
        var cache = new Queue<Image>(); var outputs = new List<Image>(); using var batch = new ImageRenderBatch<Image>();
        var root = batch.Own(new Image()); outputs.Add(root); var chunkSize = 0;
        for (var i = 0; i < count; i++)
        {
            if (cache.Count == 4) cache.Dequeue().Dispose();
            var input = new Image(); cache.Enqueue(input); batch.Borrow(input);
            // Alternate identity and distinct native results, including identity crops.
            root = batch.Own(i % 3 == 0 ? input : i % 3 == 1 ? root : new Image()); outputs.Add(root); root.Use();
            foreach (var image in cache) image.Use(); chunkSize++;
            if (chunkSize == 4) { root = batch.Checkpoint(new Image()); outputs.Add(root); chunkSize = 0; }
        }
        if (chunkSize > 0) { root = batch.Checkpoint(new Image()); outputs.Add(root); }
        var result = batch.Detach(root); batch.Dispose(); result.Use();
        foreach (var image in cache) { image.Use(); image.Dispose(); }
        result.Dispose(); Assert.All(outputs.Distinct(ReferenceEqualityComparer.Instance).Cast<Image>(), image => Assert.Equal(1, image.Disposals));
    }
    // Native wrapper identity, rather than value equality, controls ownership.
    private sealed class Image : IDisposable
    {
        public int Disposals { get; private set; }
        public void Use() { if (Disposals > 0) throw new ObjectDisposedException(nameof(Image)); }
        public void Dispose() { if (++Disposals > 1) throw new InvalidOperationException("Double dispose"); }
        public override bool Equals(object? obj) => obj is Image;
        public override int GetHashCode() => 1;
    }
}
