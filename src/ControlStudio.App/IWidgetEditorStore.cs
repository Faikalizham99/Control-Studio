using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

public sealed record WidgetSaveResult(WidgetDesign Design, bool Published);

internal interface IWidgetEditorStore
{
    WidgetCanvasSizing? CanvasSizing(WidgetSize size) => null;
    string? AssetPath(string key);
    Task<WidgetLayer?> PickPhotoAsync(bool library);
    Task<WidgetSaveResult> SaveAsync(WidgetDesign draft);
    Task RemoveDraftPhotosAsync(IEnumerable<string> keys);
}
