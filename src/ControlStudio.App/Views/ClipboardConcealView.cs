namespace ControlStudio.App.Views;

/// <summary>Keeps content measured while the native handler displays its transparent soft-focus blur.</summary>
internal sealed class ClipboardConcealView : ContentView
{
    private readonly ISet<Guid> concealedItems;
    private Guid? itemId;
    public bool IsConcealed { get; private set; }
    public bool AnimateTransition { get; private set; }
    public long RenderVersion { get; private set; }

    public ClipboardConcealView(View content, ISet<Guid> concealedItems)
    {
        this.concealedItems = concealedItems;
        Content = content; AutomationId = "ClipboardContentVisibility";
    }

    public void Bind(Guid? id)
    {
        itemId = id;
        RenderVersion++;
        Apply(id is { } value && concealedItems.Contains(value), animate: false);
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
        if (concealed) concealedItems.Add(id); else concealedItems.Remove(id);
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
