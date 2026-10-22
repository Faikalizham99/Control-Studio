using Foundation;
using ImageIO;
using UIKit;
using UniformTypeIdentifiers;

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
        return await ImportEncodedAsync(await PhotoImportInput.ReadAsync(input));
    }

    /// <summary>Called only by the explicit Paste photo action; never inspect content on page load.</summary>
    public static async Task<string> PasteAsync()
    {
        // Reading providers is content access, so iOS may ask for approval. Do it only after a tap.
        NSItemProvider[] providers;
        try { providers = await MainThread.InvokeOnMainThreadAsync(() => UIPasteboard.General.ItemProviders) ?? []; }
        catch (Exception ex) { throw new IOException("The clipboard could not be read. Allow pasting if iOS asks, then try again.", ex); }
        try
        {
            foreach (var provider in providers)
            {
                var type = provider.RegisteredTypeIdentifiers.FirstOrDefault(IsImageType);
                if (type is null) continue;
                NSData data;
                try { data = await provider.LoadDataRepresentationAsync(type); }
                catch (Exception ex) { throw new IOException("The copied photo could not be loaded. Copy it again and allow pasting if iOS asks.", ex); }
                using (data)
                {
                    if (data is null) throw new IOException("The copied photo is unavailable. Copy an image and try again.");
                    PhotoImportInput.ValidateLength((ulong)data.Length);
                    return await ImportEncodedAsync(data.ToArray());
                }
            }
            throw new IOException("No photo is available to paste. Copy an image and allow pasting if iOS asks. A copied link belongs in Link.");
        }
        finally { foreach (var provider in providers) provider.Dispose(); }
    }

    private static bool IsImageType(string identifier)
    {
        using var type = UTType.CreateFromIdentifier(identifier);
        return type?.ConformsTo(UTTypes.Image) == true;
    }

    private static Task<string> ImportEncodedAsync(byte[] sourceBytes)
    {
        return Task.Run(async () =>
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
