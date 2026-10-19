using ControlStudio.Core.Actions;

namespace ControlStudio.App.Platforms.iOS;

public sealed class AppleActionPlatform : IActionPlatform
{
    public Task<bool> OpenUrlAsync(Uri uri, CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Launcher.Default.OpenAsync(uri);
        });

    public Task CopyTextAsync(string text, CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Clipboard.Default.SetTextAsync(text);
        });
}
