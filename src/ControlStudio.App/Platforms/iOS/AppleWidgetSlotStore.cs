using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class AppleWidgetSlotStore(WidgetStudioService studio) : IWidgetSlotStore
{
    public Task<WidgetLibraryCatalog> ReadAsync(CancellationToken token) => studio.Repository.ReadLibraryAsync(token);
    public string? RenderPath(string key) => studio.Repository.RenderPath(key);
    public Task<bool> AssignAsync(WidgetSize size, int number, Guid? design, CancellationToken token) => studio.AssignSlotAsync(size, number, design, token);
    public Task<bool> PublishAsync() => studio.PublishAsync();
}
