using System.Runtime.InteropServices;
using ControlStudio.Core.Widgets;
using CoreML;
using Foundation;

namespace ControlStudio.App.Platforms.iOS;

internal static class PhotoObjectRemovalImaging
{
    // Exactly one prediction at a time across windows/editors. Release the model
    // after each operation so it cannot retain GPU allocations between edits.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    public static async Task<byte[]> RemoveAsync(int width, int height, byte[] pixels, byte[] selection, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try { return await Task.Run(() => Remove(width, height, pixels, selection, token), token); }
        finally { Gate.Release(); }
    }
    private static byte[] Remove(int width, int height, byte[] pixels, byte[] selection, CancellationToken token)
    {
        using var pool = new NSAutoreleasePool(); token.ThrowIfCancellationRequested();
        var plan = PhotoObjectRemoval.Create(width, height, selection);
        var input = plan.Input(pixels, selection, token);
        using var configuration = new MLModelConfiguration { ComputeUnits = MLComputeUnits.CpuAndGpu };
        using var url = CompiledModel(); token.ThrowIfCancellationRequested();
        using var model = MLModel.Create(url, configuration, out var loadError);
        using (loadError) if (model is null)
        {
            // A purged/partial compiled cache must not make every future retry fail.
            if (url.Path is { } cache) { try { Directory.Delete(cache, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            throw new IOException("The removal model could not load. Your selection is kept; try again.");
        }
        using var tensor = new MLMultiArray(new nint[] { 1, 4, 512, 512 }, MLMultiArrayDataType.Float32, out var inputError);
        using (inputError) if (tensor is null || (long)tensor.Count != input.Length) throw new IOException("The removal input could not be prepared.");
        Exception? copyError = null; var copied = false;
        tensor.GetMutableBytes((pointer, bytes, strides) =>
        {
            try
            {
                if (strides.Count != 4) return;
                var inputLayout = new int[4];
                for (var i = 0; i < 4; i++)
                { using var stride = strides.GetItem<NSNumber>((nuint)i); if (stride is null) return; inputLayout[i] = stride.Int32Value; }
                if (bytes < input.Length * sizeof(float) || inputLayout[1] != 262144 || inputLayout[2] != 512 || inputLayout[3] != 1) return;
                Marshal.Copy(input, 0, pointer, input.Length); copied = true;
            }
            catch (Exception ex) { copyError = ex; } // Never unwind a managed exception through a native callback.
        });
        if (!copied) throw new IOException("The removal input layout is unsupported.", copyError);
        using var value = MLFeatureValue.Create(tensor);
        using var inputName = new NSString("x_1");
        using var features = new NSDictionary<NSString, NSObject>(inputName, value);
        using var provider = new MLDictionaryFeatureProvider(features, out var providerError);
        using (providerError) if (provider is null) throw new IOException("The removal input could not be prepared.");
        token.ThrowIfCancellationRequested();
        using var prediction = model.GetPrediction(provider, out var predictionError);
        using (predictionError) if (prediction is null) throw new IOException("This selection could not be removed. Try a smaller area.");
        token.ThrowIfCancellationRequested();
        using var outputValue = prediction.GetFeatureValue("var_915");
        using var output = outputValue?.MultiArrayValue;
        if (output is null || output.DataType != MLMultiArrayDataType.Float32 || !output.Shape.Select(x => checked((int)x)).SequenceEqual(new[] { 1, 3, 512, 512 }))
            throw new IOException("The removal model returned an unsupported image.");
        int[]? outputStrides = null; float[]? raw = null; copyError = null;
        output.GetBytes((pointer, bytes) =>
        {
            try
            {
                var strides = output.Strides.Select(x => checked((int)x)).ToArray();
                if (strides.Length != 4 || strides.Any(x => x <= 0)) return;
                var length = checked(2 * strides[1] + 511 * strides[2] + 511 * strides[3] + 1);
                if (length > 4_000_000 || (long)length * sizeof(float) > bytes) return;
                var buffer = new float[length]; Marshal.Copy(pointer, buffer, 0, length);
                outputStrides = strides; raw = buffer;
            }
            catch (Exception ex) { copyError = ex; }
        });
        if (raw is null || outputStrides is not { } layout) throw new IOException("The removal output layout is invalid.", copyError);
        var rgb = new float[3 * 512 * 512];
        for (var c = 0; c < 3; c++) for (var y = 0; y < 512; y++) for (var x = 0; x < 512; x++)
            rgb[c * 512 * 512 + y * 512 + x] = raw[c * layout[1] + y * layout[2] + x * layout[3]];
        return plan.Composite(pixels, selection, rgb, token);
    }
    private static NSUrl CompiledModel()
    {
        var cache = Path.Combine(FileSystem.CacheDirectory, "object-removal-6090945f20e49711.mlmodelc");
        if (!Directory.Exists(cache))
        {
            using var source = NSBundle.MainBundle.GetUrlForResource("aotgan", "mlmodel") ?? throw new IOException("The removal model is missing from this app build.");
            NSUrl? compiled = null;
            try
            {
                compiled = MLModel.CompileModelAsync(source).GetAwaiter().GetResult();
                if (compiled?.Path is null) throw new IOException("The compiled removal model is unavailable.");
                Directory.Move(compiled.Path, cache);
            }
            catch (Exception ex) when (ex is NSErrorException or IOException or UnauthorizedAccessException)
            {
                throw new IOException("The removal model could not be prepared. Check free storage and try again. Your selection is kept.", ex);
            }
            finally
            {
                // Compilation returns a temporary directory. A failed cache move
                // must not leave another model copy behind on every retry.
                if (compiled?.Path is { } temporary && Directory.Exists(temporary))
                { try { Directory.Delete(temporary, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                compiled?.Dispose();
            }
        }
        return NSUrl.FromFilename(cache);
    }
}
