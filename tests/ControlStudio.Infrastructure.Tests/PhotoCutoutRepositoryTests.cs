using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class PhotoCutoutRepositoryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "cutout-tests-" + Guid.NewGuid().ToString("N"));
    private WidgetDesignRepository Repository => new(directory);
    private static byte[] Png(int width = 2, int height = 2)
    {
        var bytes = new byte[24]; new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height); return bytes;
    }
    [Fact]
    public async Task Reopening_retains_original_mask_and_independent_revision()
    {
        var repo = Repository; var original = Png(); var mask = new byte[] { 0, 50, 150, 255 };
        var first = await repo.ImportCutoutAsync(original, mask, Png());
        var loaded = await Repository.ReadCutoutAsync(first); Assert.Equal(original, loaded.Original); Assert.Equal(mask, loaded.Mask);
        var second = await repo.ImportCutoutAsync(loaded.Original, new byte[4], Png()); Assert.NotEqual(first, second);
        await repo.RemoveDraftPhotosAsync([second]); Assert.Equal(mask, (await repo.ReadCutoutAsync(first)).Mask);
        Assert.False(File.Exists(Path.Combine(directory, "photos", second + ".mask")));
        Assert.False(File.Exists(Path.Combine(directory, "photos", second + ".original.png")));
    }
    [Fact]
    public async Task Saved_duplicates_protect_all_parts_until_last_reference_is_removed()
    {
        var repo = Repository; var key = await repo.ImportCutoutAsync(Png(), new byte[4], Png());
        var layer = new WidgetLayer(Guid.NewGuid(), "Photo", key, 2, 2, new());
        var design = WidgetDesignRules.New() with { Layers = [layer, layer with { Id = Guid.NewGuid() }] };
        var saved = await repo.SaveAsync(design, Png());
        await repo.RemoveDraftPhotosAsync([key]); Assert.NotNull((await repo.ReadCutoutAsync(key)).Mask);
        var one = await repo.SaveAsync(saved with { Layers = [saved.Layers[0]] }, Png());
        Assert.NotNull((await repo.ReadCutoutAsync(key)).Mask);
        await repo.DeleteAsync(one.Id); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "photos")));
    }
    [Fact]
    public async Task Plain_photo_can_be_edited_and_invalid_or_partial_sidecars_are_rejected()
    {
        var repo = Repository; var key = await repo.ImportPhotoAsync(Png()); Assert.Null((await repo.ReadCutoutAsync(key)).Mask);
        await File.WriteAllBytesAsync(Path.Combine(directory, "photos", key + ".mask"), new byte[4]);
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        await Assert.ThrowsAsync<IOException>(() => repo.ImportCutoutAsync(Png(), new byte[3], Png()));
        await Assert.ThrowsAsync<IOException>(() => repo.ImportCutoutAsync(Png(), new byte[4], Png(3, 2)));
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync("../invalid"));
    }
    [Fact]
    public async Task Repaired_colours_reopen_independently_of_original_and_cleanup_with_last_reference()
    {
        var repo = Repository; var original = Png(); var working = Png(); working[10] = 9;
        var key = await repo.ImportCutoutAsync(original, new byte[4], Png(), working: working);
        var loaded = await Repository.ReadCutoutAsync(key);
        Assert.Equal(original, loaded.Original); Assert.Equal(working, loaded.Working);
        var layer = new WidgetLayer(Guid.NewGuid(), "Repaired", key, 2, 2, new());
        var saved = await repo.SaveAsync(WidgetDesignRules.New() with { Layers = [layer, layer with { Id = Guid.NewGuid() }] }, Png());
        await repo.RemoveDraftPhotosAsync([key]); Assert.Equal(working, (await repo.ReadCutoutAsync(key)).Working);
        await repo.DeleteAsync(saved.Id); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "photos")));
    }
    [Fact]
    public async Task Missing_or_modified_repaired_colours_cannot_silently_reopen_original_object()
    {
        var repo = Repository; var working = Png(); working[10] = 9;
        var key = await repo.ImportCutoutAsync(Png(), new byte[4], Png(), working: working);
        var path = Path.Combine(directory, "photos", key + ".working.png");
        File.Delete(path);
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        await File.WriteAllBytesAsync(path, working);
        Assert.Equal(working, (await repo.ReadCutoutAsync(key)).Working);
        var corrupted = working.ToArray(); corrupted[10] = 12;
        await File.WriteAllBytesAsync(path, corrupted);
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
    }
    [Fact]
    public async Task Invalid_repair_dimensions_or_incomplete_sidecars_are_rejected()
    {
        var repo = Repository;
        await Assert.ThrowsAsync<IOException>(() => repo.ImportCutoutAsync(Png(), new byte[4], Png(), working: Png(3, 2)));
        var key = await repo.ImportPhotoAsync(Png());
        await File.WriteAllBytesAsync(Path.Combine(directory, "photos", key + ".working.png"), Png());
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        await repo.RemoveDraftPhotosAsync([key]); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "photos")));
    }
    [Fact]
    public async Task Cancelled_import_leaves_no_partial_asset()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository.ImportCutoutAsync(Png(), new byte[4], Png(), cancel.Token));
        var photos = Path.Combine(directory, "photos"); if (Directory.Exists(photos)) Assert.Empty(Directory.GetFiles(photos));
    }
    [Fact]
    public async Task CropReopensFullOriginalMaskAndRepairsAndRemainsEditableAfterRevision()
    {
        var repo = Repository; var original = Png(4, 3); var working = Png(4, 3); working[10] = 42; var mask = Enumerable.Range(0, 12).Select(x => (byte)(x * 20)).ToArray();
        var crop = new PhotoCropRect(1, 1, 2, 2);
        var key = await repo.ImportCutoutAsync(original, mask, Png(), working: working, crop: crop);
        var reopened = await Repository.ReadCutoutAsync(key);
        Assert.Equal(original, reopened.Original); Assert.Equal(mask, reopened.Mask); Assert.Equal(working, reopened.Working); Assert.Equal(crop, reopened.Crop);
        var revision = await repo.ImportCutoutAsync(reopened.Original, mask, Png(4, 3), working: reopened.Working);
        Assert.Null((await repo.ReadCutoutAsync(revision)).Crop); Assert.Equal(crop, (await repo.ReadCutoutAsync(key)).Crop);
        var layer = new WidgetLayer(Guid.NewGuid(), "Cropped", key, 2, 2, new());
        var saved = await repo.SaveAsync(WidgetDesignRules.New() with { Layers = [layer, layer with { Id = Guid.NewGuid() }] }, Png());
        await repo.RemoveDraftPhotosAsync([key, revision]); Assert.Equal(crop, (await repo.ReadCutoutAsync(key)).Crop);
        await repo.DeleteAsync(saved.Id); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "photos")));
    }
    [Fact]
    public async Task LostOrTamperedCropMetadataCannotSilentlyRestoreOrChangeTheVisibleArea()
    {
        var repo = Repository; var key = await repo.ImportCutoutAsync(Png(4, 3), new byte[12], Png(), crop: new(1, 1, 2, 2));
        var path = Path.Combine(directory, "photos", key + ".crop.json"); var proof = Path.Combine(directory, "photos", key + ".crop.sha256");
        var bytes = await File.ReadAllBytesAsync(path); await File.WriteAllTextAsync(path, "{}");
        await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        await File.WriteAllBytesAsync(path, bytes); File.Delete(proof); await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        File.Delete(path); await Assert.ThrowsAsync<IOException>(() => repo.ReadCutoutAsync(key));
        await repo.RemoveDraftPhotosAsync([key]); Assert.Empty(Directory.GetFiles(Path.Combine(directory, "photos")));
    }
    [Fact]
    public async Task InvalidCropOrOutputDimensionsAndCancelledCropLeaveNoPartialAssets()
    {
        var repo = Repository;
        await Assert.ThrowsAsync<IOException>(() => repo.ImportCutoutAsync(Png(4, 3), new byte[12], Png(), crop: new(3, 2, 2, 2)));
        await Assert.ThrowsAsync<IOException>(() => repo.ImportCutoutAsync(Png(4, 3), new byte[12], Png(), crop: new(0, 0, 3, 2)));
        using var token = new CancellationTokenSource(); token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ImportCutoutAsync(Png(4, 3), new byte[12], Png(), token.Token, crop: new(1, 1, 2, 2)));
        var photos = Path.Combine(directory, "photos"); if (Directory.Exists(photos)) Assert.Empty(Directory.GetFiles(photos));
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
