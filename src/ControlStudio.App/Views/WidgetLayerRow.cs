using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Stable layer controls: gesture updates never reload thumbnails or native switches.</summary>
internal sealed class WidgetLayerRow : ContentView
{
    private readonly Label name = SettingsControls.Text("", 15, bold: true);
    private readonly Image photo = new() { WidthRequest = 48, HeightRequest = 48, Aspect = Aspect.AspectFill, InputTransparent = true };
    private readonly Switch visible = new();
    private readonly Border surface;
    private readonly Button tap;
    private readonly ClipboardIconButton more;
    private bool updating;
    public WidgetLayerRow(WidgetLayer layer, string? path, string dragScope, Action<Guid> select,
        Action<Guid, bool> show, Action<Guid> menu, Action<Guid, Guid> reorder)
    {
        photo.Source = path; name.MaxLines = 2; name.LineBreakMode = LineBreakMode.TailTruncation;
        var thumbnail = ClipboardEditorControls.Field(photo, 0); thumbnail.WidthRequest = thumbnail.HeightRequest = 48;
        var title = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center,
            InputTransparent = true, Children = { name, SettingsControls.Text("Image", 12, secondary: true) } };
        tap = SettingsControls.TapTarget("Select " + layer.Name, "SelectWidgetLayer" + layer.Id.ToString("N"));
        tap.Clicked += (_, _) => { if (IsEnabled) select(layer.Id); };
        var selection = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(48), new(GridLength.Star) } };
        var labels = new Grid { Children = { tap, title } }; selection.Add(thumbnail); selection.Add(labels, 1);
        AutomationProperties.SetIsInAccessibleTree(title, false); AutomationProperties.SetIsInAccessibleTree(thumbnail, false);
        var drag = new Grid { WidthRequest = 44, MinimumHeightRequest = 48, Children = { ClipboardEditorControls.Icon(StudioSymbol.Drag, size: 18) } };
        SemanticProperties.SetDescription(drag, "Reorder " + layer.Name); SemanticProperties.SetHint(drag, "Drag to another layer. Move forward and backward are also in the layer menu.");
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
        visible.SetDynamicResource(Switch.OnColorProperty, "Accent");
        SemanticProperties.SetDescription(visible, "Show " + layer.Name);
        visible.Toggled += (_, args) => { if (!updating && IsEnabled) show(layer.Id, args.Value); };
        more = new ClipboardIconButton(StudioSymbol.More, "Layer actions"); more.Describe("Layer actions", layer.Name);
        more.Clicked += (_, _) => { if (IsEnabled) menu(layer.Id); };
        var row = new Grid { ColumnSpacing = 4, ColumnDefinitions = { new(44), new(GridLength.Star), new(GridLength.Auto), new(48) } };
        row.Add(drag); row.Add(selection, 1); row.Add(visible, 2); row.Add(more, 3);
        surface = WidgetStudioStyle.Card(row, new Thickness(6, 8), 16); Content = surface; Update(layer, false);
    }
    private static bool ValidDrag(DataPackagePropertySet properties, string scope) => properties.TryGetValue("WidgetLayerScope", out var value) && value is string text && text == scope;
    private static bool ValidDrag(DataPackagePropertySetView properties, string scope) => properties.TryGetValue("WidgetLayerScope", out var value) && value is string text && text == scope;
    public void Update(WidgetLayer layer, bool selected)
    {
        updating = true; name.Text = layer.Name; visible.IsToggled = layer.Visible; updating = false;
        surface.StrokeThickness = selected ? 2 : 1;
        if (selected) surface.SetDynamicResource(Border.StrokeProperty, "AccentText");
        else { surface.RemoveDynamicResource(Border.StrokeProperty); SettingsControls.SurfaceStroke(surface); }
        SemanticProperties.SetDescription(tap, "Select " + layer.Name + (selected ? ", selected" : "") + (layer.Visible ? "" : ", hidden"));
        more.Describe("Layer actions", layer.Name);
        SemanticProperties.SetDescription(visible, "Show " + layer.Name);
    }
    public void ReleaseSource() => photo.Source = null;
}
