using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

internal interface IWidgetGalleryStore : IWidgetEditorStore
{
    bool HasSavedLibrary { get; }
    Task<IReadOnlyList<WidgetDesign>> ReadAsync(CancellationToken token);
    string? RenderPath(string key);
    DateTimeOffset? RenderUpdatedAt(string key);
    Task<bool> PublishAsync(bool force, CancellationToken token);
    Task<bool> DeleteAsync(Guid id);
    IWidgetSlotStore Slots { get; }
}
