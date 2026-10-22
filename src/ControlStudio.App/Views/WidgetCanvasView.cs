using ControlStudio.Core.Widgets;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

/// <summary>Live, normalized photo layout. Reuses image views while sliders change geometry.</summary>
internal sealed class WidgetCanvasView : ContentView
{
    private readonly AbsoluteLayout canvas = new() { IsClippedToBounds = true };
    private readonly Border border;
    private readonly Dictionary<Guid, PhotoView> photos = [];
    private readonly Func<string, string?> path;
    private readonly Action<Guid> select;
    private WidgetDesign? design;
    private Guid? selected;
    public WidgetCanvasView(Func<string, string?> path, Action<Guid> select)
    {
        this.path = path; this.select = select;
        HeightRequest = 200; MaximumWidthRequest = 420; AutomationId = "WidgetCanvas";
        border = new Border { Content = canvas, StrokeShape = new RoundRectangle { CornerRadius = 24 }, StrokeThickness = 1 };
        SettingsControls.SurfaceStroke(border); Content = border;
        SizeChanged += (_, _) => ArrangePhotos(); canvas.SizeChanged += (_, _) => ArrangePhotos();
    }
    public void Update(WidgetDesign value, Guid? selectedId)
    {
        design = value; selected = selectedId; border.BackgroundColor = Color.FromArgb(value.BackgroundHex);
        foreach (var id in photos.Keys.Where(id => !value.Layers.Any(x => x.Id == id)).ToArray())
        { canvas.Children.Remove(photos[id].Frame); photos[id].Image.Source = null; photos.Remove(id); }
        for (var i = 0; i < value.Layers.Count; i++)
        {
            var layer = value.Layers[i];
            if (!photos.TryGetValue(layer.Id, out var photo))
            {
                photo = new PhotoView(path(layer.AssetKey)); photos.Add(layer.Id, photo);
                var tap = new TapGestureRecognizer(); var id = layer.Id;
                tap.Tapped += (_, _) => select(id); photo.Frame.GestureRecognizers.Add(tap);
                SemanticProperties.SetDescription(photo.Frame, $"Photo layer: {layer.Name}. Tap to select.");
                canvas.Children.Add(photo.Frame);
            }
            var current = canvas.Children.IndexOf(photo.Frame);
            if (current != i) { canvas.Children.RemoveAt(current); canvas.Children.Insert(i, photo.Frame); }
            photo.Frame.IsVisible = layer.Visible; photo.Selection.IsVisible = selectedId == layer.Id;
        }
        ArrangePhotos();
    }
    private void ArrangePhotos()
    {
        if (design is null || Width <= 0) return;
        var size = WidgetDesignRules.Canvas(design.Size); var height = Width * size.Height / size.Width;
        if (Math.Abs(HeightRequest - height) > .5) HeightRequest = height;
        if (canvas.Width <= 0 || canvas.Height <= 0) return;
        foreach (var layer in design.Layers)
        {
            var photo = photos[layer.Id];
            var width = layer.Frame.Width * canvas.Width; var frameHeight = layer.Frame.Height * canvas.Height;
            AbsoluteLayout.SetLayoutBounds(photo.Frame, new Rect(layer.Frame.X * canvas.Width, layer.Frame.Y * canvas.Height, width, frameHeight));
            var placement = WidgetPhotoGeometry.Image(layer, width, frameHeight);
            AbsoluteLayout.SetLayoutBounds(photo.Image, new Rect(placement.X, placement.Y, placement.Width, placement.Height));
            AbsoluteLayout.SetLayoutBounds(photo.Selection, new Rect(0, 0, width, frameHeight));
        }
        SemanticProperties.SetDescription(this, $"{WidgetDesignRules.SizeName(design.Size)} widget preview. {design.Layers.Count(x => x.Visible)} visible photo layers.");
    }
    private sealed class PhotoView
    {
        public AbsoluteLayout Frame { get; } = new() { IsClippedToBounds = true };
        public Image Image { get; } = new() { Aspect = Aspect.Fill, InputTransparent = true };
        public Border Selection { get; } = new() { StrokeThickness = 2, BackgroundColor = Colors.Transparent, InputTransparent = true };
        public PhotoView(string? path)
        {
            Image.Source = path; Selection.SetDynamicResource(Border.StrokeProperty, "AccentText");
            Frame.Children.Add(Image); Frame.Children.Add(Selection);
            AutomationProperties.SetIsInAccessibleTree(Selection, false);
        }
    }
}
