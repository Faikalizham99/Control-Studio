using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

internal interface IWidgetSlotStore
{
    Task<WidgetLibraryCatalog> ReadAsync(CancellationToken token);
    string? RenderPath(string key);
    Task<bool> AssignAsync(WidgetSize size, int number, Guid? design, CancellationToken token);
    Task<bool> PublishAsync();
}
