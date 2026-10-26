namespace ControlStudio.App.Rendering;

internal enum WidgetRenderStage { Initialising, Validation, Background, Photo, Text, Battery, Blend, Crop, Checkpoint, Snapshot, LiveUpdate }
internal static class WidgetRenderFailure
{
    public static string Describe(Exception error, WidgetRenderStage stage, string? nativeName = null)
    {
        // Expected validation/import errors already contain an actionable message.
        if (error is IOException) return error.Message;
        var operation = stage switch
        {
            WidgetRenderStage.Validation => "checking the design",
            WidgetRenderStage.Background => "preparing the background",
            WidgetRenderStage.Photo => "drawing a photo layer",
            WidgetRenderStage.Text => "drawing a text layer",
            WidgetRenderStage.Battery => "drawing a battery layer",
            WidgetRenderStage.Blend => "blending layers",
            WidgetRenderStage.Crop => "clipping layers",
            WidgetRenderStage.Checkpoint => "preparing an image checkpoint",
            WidgetRenderStage.Snapshot => "creating the preview image",
            WidgetRenderStage.LiveUpdate => "updating the live preview",
            _ => "starting image rendering"
        };
        // Native exception messages can contain private paths or layer content. A
        // stage and managed exception type identify the failure without copying them.
        var type = error.GetType().Name;
        if (nativeName is { Length: > 0 and <= 64 } && nativeName.All(c => char.IsAsciiLetterOrDigit(c) || c == '_')) type += "/" + nativeName;
        return $"Preview unavailable while {operation} ({type}). Reopen the editor to retry.";
    }
}
