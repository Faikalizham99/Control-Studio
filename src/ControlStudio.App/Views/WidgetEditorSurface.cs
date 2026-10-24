namespace ControlStudio.App.Views;

/// <summary>Blank page taps can clear photo selection without stealing control/canvas touches.</summary>
internal sealed class WidgetEditorSurface : ContentView
{
    private readonly Func<bool> canDeselect;
    private readonly Action deselect;
    public IReadOnlyList<View> ProtectedAreas { get; }
    public WidgetEditorSurface(View content, Func<bool> canDeselect, Action deselect, params View[] protectedAreas)
    {
        Content = content; this.canDeselect = canDeselect; this.deselect = deselect;
        ProtectedAreas = Array.AsReadOnly(protectedAreas);
    }
    public bool CanDeselect => IsEnabled && canDeselect();
    public bool DeselectFromBlankSpace()
    {
        if (!CanDeselect) return false;
        deselect(); return true;
    }
}
