namespace ControlStudio.App.Views;

/// <summary>Keep settings reflow from shortening the content past the visible viewport.</summary>
internal sealed class WidgetEditorScrollView : ScrollView
{
    private View? heldContent;
    private double originalMinimum, heldY;
    private View? anchor;
    private View? fallback;
    private double anchorScreenY, capturedOffset;
    private double fallbackScreenY;
    private int generation;
    public WidgetEditorScrollView()
    {
        Scrolled += (_, args) =>
        {
            // Manual scrolling, keyboard movement and accessibility navigation
            // take precedence over a pending layout correction.
            if (anchor is not null && Math.Abs(args.ScrollY - capturedOffset) > .5) CancelAnchor();
            // Give the temporary bottom space back as the user scrolls towards
            // earlier content. Never issue a scroll or interfere with a drag.
            if (heldContent is null || args.ScrollY >= heldY - .5) return;
            if (args.ScrollY <= .5) ReleaseViewport();
            else Hold(Math.Max(0, args.ScrollY));
        };
        SizeChanged += (_, _) => ReleaseViewport();
        Unloaded += (_, _) => CancelAnchor();
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
    public void PreserveAnchor(View view)
    {
        CancelAnchor();
        PreserveViewport();
        if (Height <= 0 || ScrollY < 0 || Position(view) is not { } y || view.Height <= 0 ||
            y + view.Height <= ScrollY || y >= ScrollY + Height) return;
        anchor = view; anchorScreenY = y - ScrollY; capturedOffset = ScrollY;
        // If a Done action removes its own panel, retain the next visible
        // section instead. Do not anchor content that was offscreen.
        if (Content is Layout layout)
            foreach (var next in layout.Children.OfType<View>())
                if (Position(next) is { } top && top > y && top < ScrollY + Height && next.Height > 0)
                { fallback = next; fallbackScreenY = top - ScrollY; break; }
    }
    // Called once after a form arrange, not on a timer or from Scroll events.
    public void CompleteReflow()
    {
        if (anchor is null) return;
        var token = generation;
        Dispatcher.Dispatch(() =>
        {
            if (token != generation || anchor is not { } view) return;
            var y = Position(view);
            var screenY = anchorScreenY;
            if (y is null && fallback is { } next) { y = Position(next); screenY = fallbackScreenY; }
            if (y is null || Height <= 0 || Math.Abs(ScrollY - capturedOffset) > .5) { CancelAnchor(); return; }
            var target = Math.Max(0, y.Value - screenY);
            if (Math.Abs(target - ScrollY) <= .5) { CancelAnchor(); return; }
            // Let the larger minimum reach native content layout before asking
            // for an offset that would otherwise be clamped to the old bottom.
            if (Content.Height + .5 < target + Height) { Hold(target); return; }
            CancelAnchor();
            Hold(target);
            // Direct numeric, instant request to the attached handler only.
            // Unlike ScrollToAsync, this never parks a request when detached.
            ((IScrollView)this).RequestScrollTo(ScrollX, target, true);
        });
    }
    private double? Position(View view)
    {
        double y = 0;
        for (Element? current = view; current is not null; current = current.Parent)
        {
            if (current == Content) return y;
            if (current is View item)
            {
                if (!item.IsVisible || !double.IsFinite(item.Y) || item.Height < 0) return null;
                y += item.Y;
            }
        }
        return null;
    }
    public void CancelAnchor() { anchor = fallback = null; generation++; }
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
        CancelAnchor();
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
    protected override void OnHandlerChanged()
    {
        if (Handler is null) CancelAnchor();
        base.OnHandlerChanged();
    }
}
