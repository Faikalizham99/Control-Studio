using Foundation;
using ImageIO;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class ProfilePhotoStore(AppearancePreferences preferences) : IProfilePhotoStore
{
    private static string Folder => Path.Combine(FileSystem.Current.AppDataDirectory, "Profile");
    public string? GetPath(string key) => Guid.TryParseExact(key, "N", out _) && File.Exists(Path.Combine(Folder, key + ".jpg")) ? Path.Combine(Folder, key + ".jpg") : null;

    public async Task<string?> PickAsync(bool fromLibrary)
    {
        if (!fromLibrary)
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose profile photo", FileTypes = FilePickerFileType.Images });
            return file is null ? null : await ImportAsync(file);
        }
        var files = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Choose profile photo", SelectionLimit = 1 });
        try { return files.Count == 0 ? null : await ImportAsync(files[0]); }
        finally { foreach (var file in files) (file as IDisposable)?.Dispose(); }
    }

    public Task<string?> AdjustAsync(string key)
    {
        if (GetPath(key) is not { } path) return Task.FromResult<string?>(null);
        var original = Path.Combine(Folder, key + ".source.jpg");
        return ImportAsync(new FileResult(File.Exists(original) ? original : path));
    }

    private async Task<string?> ImportAsync(FileResult file)
    {
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
        using var thumbnail = imageSource.CreateThumbnail(0, new CGImageThumbnailOptions { CreateThumbnailFromImageAlways = true, CreateThumbnailWithTransform = true, MaxPixelSize = 2048 })
            ?? throw new IOException("This photo could not be opened.");
        using var source = new UIImage(thumbnail);
        if (source.Size.Width <= 0 || source.Size.Height <= 0) throw new IOException("This photo could not be opened.");
        using var image = await ProfilePhotoCropper.CropAsync(source, preferences.AccentHex);
        if (image is null) return null;
        using var jpeg = image.AsJPEG(.88f) ?? throw new IOException("This photo could not be saved.");
        using var originalJpeg = source.AsJPEG(.92f) ?? throw new IOException("This photo could not be saved.");
        var key = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Folder);
        var destination = Path.Combine(Folder, key + ".jpg");
        var originalDestination = Path.Combine(Folder, key + ".source.jpg");
        try
        {
            await File.WriteAllBytesAsync(originalDestination, originalJpeg.ToArray());
            await File.WriteAllBytesAsync(destination, jpeg.ToArray());
        }
        catch
        {
            DeleteFile(destination); DeleteFile(originalDestination);
            throw;
        }
        return key;
    }

    public void Remove(string key)
    {
        if (GetPath(key) is not { } path) return;
        DeleteFile(path); DeleteFile(Path.Combine(Folder, key + ".source.jpg"));
    }

    private static void DeleteFile(string path) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
