namespace ControlStudio.App;

/// <summary>One bounded encoded-image input contract for Photo Library, Files and Paste.</summary>
internal static class PhotoImportInput
{
    public const int MaximumSourceBytes = 10_000_000;

    public static void ValidateLength(ulong length)
    {
        if (length == 0) throw new IOException("This photo is empty. Choose or copy another image.");
        if (length > MaximumSourceBytes) throw new IOException("Choose or copy a photo smaller than 10 MB.");
    }

    public static async Task<byte[]> ReadAsync(Stream input, CancellationToken cancellationToken = default)
    {
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int length;
        while ((length = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            ValidateLength((ulong)(bytes.Length + length));
            await bytes.WriteAsync(buffer.AsMemory(0, length), cancellationToken);
        }
        ValidateLength((ulong)bytes.Length);
        return bytes.ToArray();
    }
}
