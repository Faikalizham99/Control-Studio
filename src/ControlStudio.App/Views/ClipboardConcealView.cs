namespace ControlStudio.App.Views;

internal sealed class ClipboardVisibilityErrorEventArgs(Exception error, bool loading) : EventArgs
{
    public Exception Error { get; } = error;
    public bool Loading { get; } = loading;
}

/// <summary>Keeps content measured while the native handler displays its transparent soft-focus blur.</summary>
internal sealed class ClipboardConcealView : ContentView
{
    private readonly ClipboardVisibilityPreferences visibility;
    private Guid? itemId;
    public bool IsConcealed { get; private set; }
    public bool AnimateTransition { get; private set; }
    public long RenderVersion { get; private set; }
    public event EventHandler<ClipboardVisibilityErrorEventArgs>? VisibilityFailed;

    public ClipboardConcealView(View content, ClipboardVisibilityPreferences visibility)
    {
        this.visibility = visibility;
        Content = content; AutomationId = "ClipboardContentVisibility";
    }

    public void Bind(Guid? id)
    {
        itemId = id;
        RenderVersion++;
        bool concealed;
        try { concealed = id is { } value && visibility.IsConcealed(value); }
        catch (Exception ex)
        {
            // A failed read must not expose a potentially concealed item on its first frame.
            Apply(id is not null, animate: false);
            VisibilityFailed?.Invoke(this, new(ex, loading: true));
            return;
        }
        Apply(concealed, animate: false);
    }

    public void RefreshContent()
    {
        if (Dispatcher.IsDispatchRequired) { Dispatcher.Dispatch(RefreshContent); return; }
        RenderVersion++;
        Handler?.UpdateValue(nameof(RenderVersion));
    }

    public void Toggle()
    {
        if (itemId is not { } id) return;
        var concealed = !IsConcealed;
        try { visibility.SetConcealed(id, concealed); }
        catch (Exception ex) { VisibilityFailed?.Invoke(this, new(ex, loading: false)); return; }
        Apply(concealed, animate: true);
    }

    private void Apply(bool concealed, bool animate)
    {
        IsConcealed = concealed; AnimateTransition = animate;
        // Disabling the subtree also invalidates native link menus; never collapse its layout.
        Content.IsEnabled = !concealed;
        Content.InputTransparent = concealed;
        Handler?.UpdateValue(nameof(IsConcealed));
    }
}
