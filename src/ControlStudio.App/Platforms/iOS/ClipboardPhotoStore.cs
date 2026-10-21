using Foundation;
using ImageIO;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Private, orientation-correct image copies. Never modifies a library original.</summary>
internal static class ClipboardPhotoStore
{
    private static string Folder => Path.Combine(FileSystem.Current.AppDataDirectory, "ControlStudio", "ClipboardPhotos");
    public static string? GetPath(string? key) => Guid.TryParseExact(key, "N", out _) && File.Exists(Path.Combine(Folder, key + ".png"))
        ? Path.Combine(Folder, key + ".png") : null;
    public static string? GetThumbnailPath(string key) => GetPath(key) is { } path
        ? (File.Exists(Path.Combine(Folder, key + ".thumb.png")) ? Path.Combine(Folder, key + ".thumb.png") : path) : null;

    public static async Task<string?> PickAsync(bool library)
    {
        if (!library)
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose clipboard photo", FileTypes = FilePickerFileType.Images });
            return file is null ? null : await ImportAsync(file);
        }
        var files = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Choose clipboard photo", SelectionLimit = 1 });
        try { return files.Count == 0 ? null : await ImportAsync(files[0]); }
        finally { foreach (var file in files) (file as IDisposable)?.Dispose(); }
    }

    private static async Task<string> ImportAsync(FileResult file)
    {
        await using var input = await file.OpenReadAsync();
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int length;
        while ((length = await input.ReadAsync(buffer)) > 0)
        {
            if (bytes.Length + length > 10_000_000) throw new IOException("Choose a photo smaller than 10 MB.");
            bytes.Write(buffer, 0, length);
        }
        var sourceBytes = bytes.ToArray();
        return await Task.Run(async () =>
        {
            using var data = NSData.FromArray(sourceBytes);
            using var source = CGImageSource.FromData(data) ?? throw new IOException("Choose a JPEG, PNG or HEIC photo.");
            using var thumbnail = source.CreateThumbnail(0, new CGImageThumbnailOptions
            {
                CreateThumbnailFromImageAlways = true, CreateThumbnailWithTransform = true, MaxPixelSize = 4096
            }) ?? throw new IOException("This photo could not be opened.");
            using var image = new UIImage(thumbnail);
            using var png = image.AsPNG() ?? throw new IOException("This photo could not be saved.");
            if (png.Length > 20_000_000) throw new IOException("This photo is too detailed. Choose a smaller image.");
            var key = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, key + ".png");
            try
            {
                using var small = source.CreateThumbnail(0, new CGImageThumbnailOptions { CreateThumbnailFromImageAlways = true, CreateThumbnailWithTransform = true, MaxPixelSize = 512 })
                    ?? throw new IOException("This photo could not be previewed.");
                using var smallImage = new UIImage(small);
                using var smallPng = smallImage.AsPNG() ?? throw new IOException("This photo could not be previewed.");
                await File.WriteAllBytesAsync(Path.Combine(Folder, key + ".thumb.png"), smallPng.ToArray());
                await File.WriteAllBytesAsync(path, png.ToArray());
            }
            catch { Remove(key); throw; }
            return key;
        });
    }

    public static Task CopyAsync(string key) => MainThread.InvokeOnMainThreadAsync(() =>
    {
        using var image = UIImage.FromFile(RequirePath(key)) ?? throw new IOException("This saved photo could not be opened.");
        UIPasteboard.General.Image = image;
    });

    public static async Task ShareAsync(string key)
    {
        var path = RequirePath(key);
        var folder = Path.Combine(FileSystem.Current.CacheDirectory, "ClipboardShare");
        Directory.CreateDirectory(folder);
        var sharePath = Path.Combine(folder, key + ".png");
        File.Copy(path, sharePath, true);
        await Share.Default.RequestAsync(new ShareFileRequest("Share photo", new ShareFile(sharePath, "image/png")));
    }

    public static string RequirePath(string key) => GetPath(key) ?? throw new IOException("This photo is missing. Edit this item to choose it again.");
    public static void Remove(string? key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) return;
        try { File.Delete(Path.Combine(Folder, key + ".png")); File.Delete(Path.Combine(Folder, key + ".thumb.png")); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
