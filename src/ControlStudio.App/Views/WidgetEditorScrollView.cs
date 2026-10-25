namespace ControlStudio.App.Views;

/// <summary>Keep settings reflow from shortening the content past the visible viewport.</summary>
internal sealed class WidgetEditorScrollView : ScrollView
{
    private View? heldContent;
    private double originalMinimum, heldY;
    public WidgetEditorScrollView()
    {
        Scrolled += (_, args) =>
        {
            // Give the temporary bottom space back as the user scrolls towards
            // earlier content. Never issue a scroll or interfere with a drag.
            if (heldContent is null || args.ScrollY >= heldY - .5) return;
            if (args.ScrollY <= .5) ReleaseViewport();
            else Hold(Math.Max(0, args.ScrollY));
        };
        SizeChanged += (_, _) => ReleaseViewport();
    }
    public void PreserveViewport()
    {
        if (Content is not View content || !double.IsFinite(Height) || Height <= 0 || !double.IsFinite(ScrollY) || ScrollY < 0) return;
        if (heldContent != content)
        {
            ReleaseViewport(); heldContent = content; originalMinimum = content.MinimumHeightRequest;
        }
        Hold(ScrollY);
    }
    private void Hold(double y)
    {
        if (heldContent is null) return;
        heldY = y;
        // Only the tail grows, never the space between cards. The widget canvas
        // keeps its width-based sizing and the page keeps the same scroll offset.
        var minimum = Math.Max(originalMinimum, y + Height);
        if (Math.Abs(heldContent.MinimumHeightRequest - minimum) > .5) heldContent.MinimumHeightRequest = minimum;
    }
    public void ReleaseViewport()
    {
        var content = heldContent; heldContent = null;
        if (content is not null) content.MinimumHeightRequest = originalMinimum;
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        if (propertyName == nameof(Content)) ReleaseViewport();
        base.OnPropertyChanged(propertyName);
    }
    protected override void OnParentSet()
    {
        if (Parent is null) ReleaseViewport();
        base.OnParentSet();
    }
}
