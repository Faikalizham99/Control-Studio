namespace ControlStudio.App.Views;

/// <summary>Only two-touch movement may read a pinch's changing touch centre.</summary>
internal static class WidgetPinchInput
{
    public static void Update(bool changed, int touches, Action apply, Action release)
    {
        // Ended/Cancelled/Failed are completed by the owning native handler.
        // Their location can describe a remaining finger instead of the pinch.
        if (!changed) return;
        if (touches == 2) apply();
        else release(); // Freeze the last valid frame if fingers are added/lifted.
    }
}
