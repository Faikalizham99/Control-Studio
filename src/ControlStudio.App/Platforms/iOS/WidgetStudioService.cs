using System.Runtime.InteropServices;
using ControlStudio.Core;
using ControlStudio.Core.Widgets;
using ControlStudio.Infrastructure;
using CoreGraphics;
using Foundation;
using ImageIO;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class WidgetStudioService : IWidgetEditorStore
{
    public WidgetDesignRepository Repository { get; }
    string? IWidgetEditorStore.AssetPath(string key) => Repository.AssetPath(key);
    Task IWidgetEditorStore.RemoveDraftPhotosAsync(IEnumerable<string> keys) => Repository.RemoveDraftPhotosAsync(keys);
    WidgetCanvasSizing IWidgetEditorStore.CanvasSizing(WidgetSize size) => AppleWidgetCanvasSizing.Read(size, SharedDirectory);
    private readonly WidgetDesignPublisher publisher;
    private readonly ILogger<WidgetStudioService> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    [DllImport("__Internal", EntryPoint = "cs_widgets_reload_slots")]
    private static extern void ReloadNative();
    [DllImport("__Internal", EntryPoint = "cs_widgets_reload_slot")]
    private static extern void ReloadSlotNative(int number);

    public WidgetStudioService(ILogger<WidgetStudioService> logger)
    {
        this.logger = logger;
        Repository = new(Path.Combine(FileSystem.Current.AppDataDirectory, "ControlStudio", "WidgetDesigns"));
        publisher = new(Repository, SharedDirectory);
    }
    private static string SharedDirectory()
    {
        var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConfiguration.Identifier)?.Path;
        if (container is null) throw new IOException("App Group access is unavailable. Check signing in Settings.");
        var basePath = Path.Combine(container, "Library", "Application Support");
        if (AppGroupConfiguration.Identifier != "group.com.faikalizham.controlstudio") basePath = Path.Combine(basePath, "ControlStudio");
        return Path.Combine(basePath, "WidgetDesigns");
    }
    public async Task<WidgetLayer?> PickPhotoAsync(bool library)
    {
        FileResult? file;
        var pickedFiles = new List<FileResult>();
        if (library)
        {
            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = "Choose a widget photo", SelectionLimit = 1 });
            pickedFiles.AddRange(photos); file = photos.FirstOrDefault();
        }
        else
        {
            file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a widget photo", FileTypes = FilePickerFileType.Images });
            if (file is not null) pickedFiles.Add(file);
        }
        if (file is null) return null;
        try
        {
            await using var stream = await file.OpenReadAsync();
            var bytes = await PhotoImportInput.ReadAsync(stream);
            var normalized = await Task.Run(() =>
            {
                using var pool = new NSAutoreleasePool();
                using var data = NSData.FromArray(bytes);
                using var source = CGImageSource.FromData(data) ?? throw new IOException("Choose a JPEG, PNG or HEIC photo.");
                using var bitmap = source.CreateThumbnail(0, new CGImageThumbnailOptions
                {
                    CreateThumbnailFromImageAlways = true, CreateThumbnailWithTransform = true, MaxPixelSize = WidgetDesignRules.MaximumImageEdge
                }) ?? throw new IOException("This photo could not be opened.");
                using var image = new UIImage(bitmap);
                using var png = image.AsPNG() ?? throw new IOException("This photo could not be prepared.");
                if (png.Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("This photo is too detailed. Choose a smaller image.");
                return (Bytes: png.ToArray(), Width: (int)bitmap.Width, Height: (int)bitmap.Height);
            });
            var key = await Repository.ImportPhotoAsync(normalized.Bytes);
            return new WidgetLayer(Guid.NewGuid(), "Photo", key, normalized.Width, normalized.Height, new());
        }
        finally { foreach (var picked in pickedFiles) (picked as IDisposable)?.Dispose(); }
    }
    public async Task<WidgetSaveResult> SaveAsync(WidgetDesign draft)
    {
        WidgetDesignRules.Validate(draft);
        await gate.WaitAsync();
        try
        {
            var battery = draft.Layers.Any(x => x.Kind == WidgetLayerKind.Battery)
                ? await MainThread.InvokeOnMainThreadAsync(AppleWidgetBattery.Read) : default;
            var render = await Task.Run(() => Render(draft, battery));
            var saved = await Repository.SaveAsync(draft, render.Full, thumbnail: render.Thumbnail, scene: render.Scene);
            return new(saved, await PublishCoreAsync());
        }
        finally { gate.Release(); }
    }
    public async Task<WidgetLayer?> EditPhotoAsync(WidgetLayer layer, Func<bool> canPresent, Action didPresent)
    {
        if (layer.Kind != WidgetLayerKind.Photo || layer.Locked) return null;
        var windows = Application.Current?.Windows.Where(x => x.IsActivated).ToArray();
        if (windows is not { Length: 1 } || windows[0].Handler?.PlatformView is not UIWindow owner)
            throw new IOException("Return to Control Studio, then tap Edit photo again.");
        var data = await Repository.ReadCutoutAsync(layer.AssetKey);
        using var imaging = await Task.Run(() => { using var pool = new NSAutoreleasePool(); return new PhotoCutoutImaging(data.Original); });
        if (imaging.Width != layer.PixelWidth || imaging.Height != layer.PixelHeight) throw new IOException("The photo dimensions changed. Choose the photo again.");
        var accent = Application.Current?.Resources.TryGetValue("Accent", out var value) == true && value is Color colour ? colour : Color.FromArgb("#007AFF");
        string? imported = null, accepted = null;
        try
        {
            accepted = await MainThread.InvokeOnMainThreadAsync(() => PhotoCutoutEditor.EditAsync(owner, canPresent, didPresent, imaging, data.Mask, accent, async result =>
            {
                if (data.Mask is { } old && old.AsSpan().SequenceEqual(result.Mask)
                    || data.Mask is null && result.Mask.All(x => x == 255)) return layer.AssetKey;
                imported = await Repository.ImportCutoutAsync(data.Original, result.Mask, result.Png);
                return imported;
            }));
            return accepted is null || accepted == layer.AssetKey ? null : layer with { AssetKey = accepted };
        }
        finally
        {
            if (imported is not null && imported != accepted)
            {
                try { await Repository.RemoveDraftPhotosAsync([imported]); }
                catch (Exception ex) { logger.LogWarning("Cancelled cut-out cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
            }
        }
    }
    public async Task<bool> PublishAsync(bool force = true, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try { return await PublishCoreAsync(force: force, token: token); } finally { gate.Release(); }
    }
    public async Task<bool> AssignSlotAsync(WidgetSize size, int number, Guid? designId, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            await Repository.AssignSlotAsync(size, number, designId, token);
            return await PublishCoreAsync(number, token: token);
        }
        finally { gate.Release(); }
    }
    public async Task<bool> DeleteAsync(Guid id)
    {
        await gate.WaitAsync();
        try { await Repository.DeleteAsync(id); return await PublishCoreAsync(); } finally { gate.Release(); }
    }
    private async Task<bool> PublishCoreAsync(int? slot = null, bool force = false, CancellationToken token = default)
    {
        try
        {
            if (force) await publisher.PublishAsync(token); else await publisher.PublishIfChangedAsync(token);
            token.ThrowIfCancellationRequested();
            if (slot is { } number) ReloadSlotNative(number); else ReloadNative(); return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning("Widget publication failed. ErrorType={ErrorType}", ex.GetType().Name); return false; }
    }
    private (byte[] Full, byte[] Thumbnail, WidgetSceneExport? Scene) Render(WidgetDesign design, WidgetBatteryReading battery)
    {
        using var pool = new NSAutoreleasePool();
        using var compositor = new WidgetPhotoCompositor(Repository.AssetPath);
        using var rendered = compositor.Render(design, battery: battery);
        using var png = rendered.AsPNG() ?? throw new IOException("The widget preview could not be rendered.");
        if (png.Length > WidgetDesignRules.MaximumImageBytes) throw new IOException("This widget is too detailed. Try fewer photos.");
        var width = rendered.Size.Width; var height = rendered.Size.Height;
        var smallScale = Math.Min(1, 256 / Math.Max(width, height));
        var smallWidth = Math.Max(1, Math.Floor(width * smallScale)); var smallHeight = Math.Max(1, Math.Floor(height * smallScale));
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = 1; format.Opaque = !design.TransparentBackground; format.PreferredRange = UIGraphicsImageRendererFormatRange.Standard;
        using var smallRenderer = new UIGraphicsImageRenderer(new CGSize(smallWidth, smallHeight), format);
        using var small = smallRenderer.CreateImage(_ => rendered.Draw(new CGRect(0, 0, smallWidth, smallHeight)));
        using var thumb = small.AsPNG() ?? throw new IOException("The widget thumbnail could not be rendered.");
        return (png.ToArray(), thumb.ToArray(), WidgetScenes.IsLive(design) ? compositor.Scene(design) : null);
    }
}
