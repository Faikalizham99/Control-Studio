using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Stable layer controls: gesture updates never reload thumbnails or native switches.</summary>
internal sealed class WidgetLayerRow : ContentView
{
    private readonly Label name = SettingsControls.Text("", 15, bold: true);
    private readonly Label detail = SettingsControls.Text("Image", 12, secondary: true);
    private readonly Image photo = new() { WidthRequest = 48, HeightRequest = 48, Aspect = Aspect.AspectFill, InputTransparent = true };
    private readonly Switch visible = SettingsControls.RowSwitch();
    private readonly Border surface;
    private readonly Button tap;
    private readonly ClipboardIconButton more;
    private readonly ClipboardIconButton placementLock;
    public Guid LayerId { get; }
    public View DragHandle { get; }
    public event EventHandler? ActionStarting;
    private bool updating, reordering, layerVisible;
    public WidgetLayerRow(WidgetLayer layer, string? path, string dragScope, Action<Guid> select,
        Action<Guid, bool> show, Action<Guid> menu, Action<Guid, Guid> reorder, Action<Guid>? toggleLock = null)
    {
        LayerId = layer.Id;
        photo.Source = path; name.MaxLines = 2; name.LineBreakMode = LineBreakMode.TailTruncation;
        var thumbnail = ClipboardEditorControls.Field(WidgetTextLayers.IsText(layer) ? ClipboardEditorControls.Icon(layer.Kind switch { WidgetLayerKind.Text => StudioSymbol.Text, WidgetLayerKind.Date => StudioSymbol.Calendar, _ => StudioSymbol.Clock }, size: 28) : photo, 0); thumbnail.WidthRequest = thumbnail.HeightRequest = 48;
        thumbnail.VerticalOptions = LayoutOptions.Center;
        var thumbnailTap = new TapGestureRecognizer(); thumbnailTap.Tapped += (_, _) => Act(() => select(layer.Id));
        thumbnail.GestureRecognizers.Add(thumbnailTap);
        var title = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center,
            InputTransparent = true, Children = { name, detail } };
        tap = SettingsControls.TapTarget("Select " + layer.Name, "SelectWidgetLayer" + layer.Id.ToString("N"));
        tap.Clicked += (_, _) => Act(() => select(layer.Id));
        var selection = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(48), new(GridLength.Star) } };
        var labels = new Grid { Children = { tap, title } }; selection.Add(thumbnail); selection.Add(labels, 1);
        AutomationProperties.SetIsInAccessibleTree(title, false); AutomationProperties.SetIsInAccessibleTree(thumbnail, false);
        var drag = new Grid { WidthRequest = 44, HeightRequest = 48, VerticalOptions = LayoutOptions.Start, Children = { ClipboardEditorControls.Icon(StudioSymbol.Drag, size: 18) } };
        DragHandle = drag;
        SemanticProperties.SetDescription(drag, "Reorder " + layer.Name); SemanticProperties.SetHint(drag, "Touch and hold, then drag to another position. Move forward and backward are also in the layer menu.");
        // iOS handles the entire row snapshot and animated gap in WidgetLayerListHandler.
#if !IOS
        var start = new DragGestureRecognizer();
        start.DragStarting += (_, args) =>
        {
            if (!IsEnabled) { args.Cancel = true; return; }
            args.Data.Properties["WidgetLayerScope"] = dragScope; args.Data.Properties["WidgetLayerId"] = layer.Id;
        };
        drag.GestureRecognizers.Add(start);
        var drop = new DropGestureRecognizer { AllowDrop = true };
        drop.DragOver += (_, args) => args.AcceptedOperation = ValidDrag(args.Data.Properties, dragScope) ? DataPackageOperation.Copy : DataPackageOperation.None;
        drop.Drop += (_, args) =>
        {
            if (IsEnabled && ValidDrag(args.Data.Properties, dragScope) && args.Data.Properties["WidgetLayerId"] is Guid source) reorder(source, layer.Id);
        };
        GestureRecognizers.Add(drop);
#endif
        SemanticProperties.SetDescription(visible, "Show " + layer.Name);
        visible.AutomationId = "WidgetLayerVisible" + layer.Id.ToString("N");
        visible.Toggled += (_, args) => { if (!updating) Act(() => show(layer.Id, args.Value)); };
        more = new ClipboardIconButton(StudioSymbol.More, "Layer actions") { VerticalOptions = LayoutOptions.Center }; more.Describe("Layer actions", layer.Name);
        more.AutomationId = "WidgetLayerOptions" + layer.Id.ToString("N");
        more.Clicked += (_, _) => Act(() => menu(layer.Id));
        placementLock = new ClipboardIconButton(StudioSymbol.Unlock, "Toggle photo lock") { AutomationId = "WidgetLayerLock" + layer.Id.ToString("N") };
        placementLock.Clicked += (_, _) => Act(() => toggleLock?.Invoke(layer.Id));
        var actions = new ActionButtonRow(visible, placementLock, more);
        var header = new CardActionHeader(selection, actions);
        var row = new Grid { ColumnSpacing = ActionSpacing.Gap, ColumnDefinitions = { new(44), new(GridLength.Star) } };
        row.Add(drag); row.Add(header, 1);
        surface = WidgetStudioStyle.Card(row, new Thickness(6, 8), 16); surface.StrokeThickness = 2; Content = surface; Update(layer, false);
    }
    private void Act(Action action)
    {
        if (!IsEnabled || reordering) return;
        ActionStarting?.Invoke(this, EventArgs.Empty); action();
    }
    private static bool ValidDrag(DataPackagePropertySet properties, string scope) => properties.TryGetValue("WidgetLayerScope", out var value) && value is string text && text == scope;
    private static bool ValidDrag(DataPackagePropertySetView properties, string scope) => properties.TryGetValue("WidgetLayerScope", out var value) && value is string text && text == scope;
    public void Update(WidgetLayer layer, bool selected)
    {
        updating = true; name.Text = layer.Name; visible.IsToggled = layerVisible = layer.Visible; updating = false;
        var kind = WidgetTextLayers.IsText(layer) ? WidgetTextLayers.Name(layer) : "photo";
        detail.Text = WidgetTextLayers.IsText(layer) ? layer.Kind.ToString()
            : layer.BlendMode == WidgetBlendMode.Normal ? "Image" : "Image · " + WidgetBlending.Name(layer.BlendMode);
        placementLock.Symbol = layer.Locked ? StudioSymbol.Lock : StudioSymbol.Unlock;
        placementLock.Describe((layer.Locked ? "Unlock " : "Lock ") + kind + " layer", layer.Name + (layer.Locked ? ", locked" : ", unlocked"));
        if (selected) surface.SetDynamicResource(Border.StrokeProperty, "AccentText");
        else { surface.RemoveDynamicResource(Border.StrokeProperty); SettingsControls.SurfaceStroke(surface); }
        SemanticProperties.SetDescription(tap, "Select " + layer.Name + (selected ? ", selected" : "") + (layer.Visible ? "" : ", hidden") + (layer.Locked ? ", locked" : ""));
        more.Describe("Layer actions", layer.Name);
        SemanticProperties.SetDescription(DragHandle, "Reorder " + layer.Name);
        SemanticProperties.SetDescription(visible, "Show " + layer.Name);
    }
    public void SetReordering(bool value)
    {
        reordering = value; tap.IsEnabled = visible.IsEnabled = more.IsEnabled = placementLock.IsEnabled = !value;
        if (!value) { updating = true; visible.IsToggled = layerVisible; updating = false; }
    }
    public void ReleaseSource() => photo.Source = null;
}
