using CoreGraphics;
using Foundation;
using ImageIO;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class ProfilePhotoStore
{
    private static string Folder => Path.Combine(FileSystem.Current.AppDataDirectory, "Profile");
    public string? GetPath(string key) => Guid.TryParseExact(key, "N", out _) && File.Exists(Path.Combine(Folder, key + ".jpg")) ? Path.Combine(Folder, key + ".jpg") : null;

    public async Task<string?> PickAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose profile photo", FileTypes = FilePickerFileType.Images });
        if (file is null) return null;
        await using var stream = await file.OpenReadAsync();
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (bytes.Length + read > 10_000_000) throw new IOException("Choose a photo smaller than 10 MB.");
            bytes.Write(buffer, 0, read);
        }
        using var data = NSData.FromArray(bytes.ToArray());
        using var imageSource = CGImageSource.FromData(data) ?? throw new IOException("Choose a supported photo, such as JPEG, PNG or HEIC.");
        using var thumbnail = imageSource.CreateThumbnail(0, new CGImageThumbnailOptions { CreateThumbnailFromImageAlways = true, CreateThumbnailWithTransform = true, MaxPixelSize = 1024 })
            ?? throw new IOException("This photo could not be opened.");
        using var source = new UIImage(thumbnail);
        if (source.Size.Width <= 0 || source.Size.Height <= 0) throw new IOException("This photo could not be opened.");
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = 1; format.Opaque = true;
        using var renderer = new UIGraphicsImageRenderer(new CGSize(512, 512), format);
        using var image = renderer.CreateImage(context =>
        {
            UIColor.White.SetFill(); context.FillRect(new CGRect(0, 0, 512, 512));
            var scale = Math.Max(512 / source.Size.Width, 512 / source.Size.Height);
            var width = source.Size.Width * scale; var height = source.Size.Height * scale;
            source.Draw(new CGRect((512 - width) / 2, (512 - height) / 2, width, height));
        });
        using var jpeg = image.AsJPEG(.88f) ?? throw new IOException("This photo could not be saved.");
        var key = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Folder);
        await File.WriteAllBytesAsync(Path.Combine(Folder, key + ".jpg"), jpeg.ToArray());
        return key;
    }

    public void Remove(string key)
    {
        if (GetPath(key) is not { } path) return;
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
