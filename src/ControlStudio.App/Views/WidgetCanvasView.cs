using ControlStudio.Core.Widgets;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

/// <summary>Live photo composition, using the same oriented geometry as the PNG renderer.</summary>
internal sealed class WidgetCanvasView : ContentView
{
    private readonly AbsoluteLayout canvas = new() { IsClippedToBounds = true, InputTransparent = true };
    private readonly WidgetCheckerboard checker = new();
    private readonly SelectionOverlay handles = new();
    private readonly Dictionary<Guid, PhotoView> photos = [];
    private readonly Func<string, string?> path;
    private readonly Action<Guid> select;
    private readonly Action<WidgetLayer>? change;
    private WidgetDesign? design;
    private Guid? selected;
    private WidgetLayer? gestureStart;
    private Point anchor;
    private int corner = -1;
    private VisualElement? measuredParent;
    public bool IsInteracting => gestureStart is not null;
    public event EventHandler? InteractionChanged;

    public WidgetCanvasView(Func<string, string?> path, Action<Guid> select, Action<WidgetLayer>? change = null)
    {
        this.path = path; this.select = select; this.change = change;
        HeightRequest = WidthRequest = 170; HorizontalOptions = LayoutOptions.Center; AutomationId = "WidgetCanvas";
        var border = new Border { Content = new Grid { Children = { checker, canvas, handles } },
            StrokeShape = new RoundRectangle { CornerRadius = 22 }, StrokeThickness = 1 };
        SettingsControls.SurfaceStroke(border); Content = border;
        canvas.SizeChanged += (_, _) => ArrangePhotos();
        SemanticProperties.SetHint(this, "Tap a photo to select it. Drag to move, pinch to resize, or drag a corner to change width and height. Precise controls are available in each layer's menu.");
    }
    protected override void OnParentSet()
    {
        if (measuredParent is not null) measuredParent.SizeChanged -= ParentSized;
        base.OnParentSet(); measuredParent = Parent as VisualElement;
        if (measuredParent is not null) measuredParent.SizeChanged += ParentSized;
        FitCanvas();
    }
    private void ParentSized(object? sender, EventArgs args) => FitCanvas();
    private void FitCanvas()
    {
        if (design is null) return;
        var size = WidgetPreviewFrame.Fit(design.Size, measuredParent?.Width > 0 ? measuredParent.Width : 300);
        if (Math.Abs(WidthRequest - size.Width) > .5) WidthRequest = size.Width;
        if (Math.Abs(HeightRequest - size.Height) > .5) HeightRequest = size.Height;
        ArrangePhotos();
    }
    public void Update(WidgetDesign value, Guid? selectedId)
    {
        if (gestureStart is not null && (design?.Size != value.Size || selected != selectedId || !value.Layers.Any(x => x.Id == gestureStart.Id && x.Visible))) EndInteraction(false);
        design = value; selected = selectedId;
        checker.IsVisible = value.TransparentBackground;
        canvas.BackgroundColor = value.TransparentBackground ? Colors.Transparent : Color.FromArgb(value.BackgroundHex);
        foreach (var id in photos.Keys.Where(id => !value.Layers.Any(x => x.Id == id)).ToArray())
        { canvas.Children.Remove(photos[id].Frame); photos[id].Image.Source = null; photos.Remove(id); }
        for (var i = 0; i < value.Layers.Count; i++)
        {
            var layer = value.Layers[i];
            if (!photos.TryGetValue(layer.Id, out var photo))
            {
                photo = new PhotoView(path(layer.AssetKey)); photos.Add(layer.Id, photo); canvas.Children.Add(photo.Frame);
            }
            var current = canvas.Children.IndexOf(photo.Frame);
            if (current != i) { canvas.Children.RemoveAt(current); canvas.Children.Insert(i, photo.Frame); }
            photo.Frame.IsVisible = layer.Visible; photo.Selection.IsVisible = selectedId == layer.Id;
            photo.Image.Opacity = layer.Opacity; photo.Image.Rotation = layer.QuarterTurns * 90;
            var odd = layer.QuarterTurns % 2 != 0;
            photo.Image.ScaleX = (odd ? layer.FlipVertical : layer.FlipHorizontal) ? -1 : 1;
            photo.Image.ScaleY = (odd ? layer.FlipHorizontal : layer.FlipVertical) ? -1 : 1;
        }
        FitCanvas();
    }
    private void ArrangePhotos()
    {
        if (design is null || canvas.Width <= 0 || canvas.Height <= 0) return;
        foreach (var layer in design.Layers)
        {
            var photo = photos[layer.Id];
            var width = layer.Frame.Width * canvas.Width; var height = layer.Frame.Height * canvas.Height;
            AbsoluteLayout.SetLayoutBounds(photo.Frame, new Rect(layer.Frame.X * canvas.Width, layer.Frame.Y * canvas.Height, width, height));
            var placement = WidgetPhotoGeometry.Image(layer, width, height); var odd = layer.QuarterTurns % 2 != 0;
            var imageWidth = odd ? placement.Height : placement.Width; var imageHeight = odd ? placement.Width : placement.Height;
            AbsoluteLayout.SetLayoutBounds(photo.Image, new Rect(placement.X + (placement.Width - imageWidth) / 2,
                placement.Y + (placement.Height - imageHeight) / 2, imageWidth, imageHeight));
            AbsoluteLayout.SetLayoutBounds(photo.Selection, new Rect(0, 0, width, height));
        }
        var active = design.Layers.FirstOrDefault(x => x.Id == selected && x.Visible);
        handles.SelectionFrame = active is null ? Rect.Zero : new Rect(active.Frame.X * canvas.Width, active.Frame.Y * canvas.Height,
            active.Frame.Width * canvas.Width, active.Frame.Height * canvas.Height);
        SemanticProperties.SetDescription(this, $"{WidgetDesignRules.SizeName(design.Size)} widget preview. {design.Layers.Count(x => x.Visible)} visible photo layers.");
    }
    private Point Normalize(double x, double y)
    {
        VisualElement? element = canvas;
        while (element is not null && element != this) { x -= element.X; y -= element.Y; element = element.Parent as VisualElement; }
        return new(x / canvas.Width, y / canvas.Height);
    }
    public void SelectAt(double x, double y)
    {
        if (!IsEnabled || IsInteracting || design is null || canvas.Width <= 0 || canvas.Height <= 0) return;
        var point = Normalize(x, y);
        var layer = design.Layers.LastOrDefault(layer => WidgetLayerEdits.Contains(layer, point.X, point.Y));
        if (layer is not null) select(layer.Id);
    }
    public bool BeginInteraction(double x, double y, bool pinch)
    {
        if (!IsEnabled || IsInteracting || change is null || design is null || canvas.Width <= 0 || canvas.Height <= 0) return false;
        var point = Normalize(x, y); corner = -1;
        var layer = design.Layers.FirstOrDefault(layer => layer.Id == selected && layer.Visible);
        if (layer is not null && !pinch)
        {
            var frame = layer.Frame;
            var corners = new[] { new Point(frame.X, frame.Y), new Point(frame.X + frame.Width, frame.Y),
                new Point(frame.X, frame.Y + frame.Height), new Point(frame.X + frame.Width, frame.Y + frame.Height) };
            corner = Array.FindIndex(corners, p => Math.Abs(p.X - point.X) * canvas.Width <= 22 && Math.Abs(p.Y - point.Y) * canvas.Height <= 22);
        }
        if (corner < 0) layer = design.Layers.LastOrDefault(layer => WidgetLayerEdits.Contains(layer, point.X, point.Y));
        if (layer is null) return false;
        select(layer.Id); gestureStart = layer;
        anchor = new(Math.Clamp((point.X - layer.Frame.X) / layer.Frame.Width, 0, 1), Math.Clamp((point.Y - layer.Frame.Y) / layer.Frame.Height, 0, 1));
        InteractionChanged?.Invoke(this, EventArgs.Empty); return true;
    }
    public void Pan(double x, double y)
    {
        if (gestureStart is null || canvas.Width <= 0 || canvas.Height <= 0) return;
        change?.Invoke(corner < 0 ? WidgetLayerEdits.Move(gestureStart, x / canvas.Width, y / canvas.Height)
            : WidgetLayerEdits.Resize(gestureStart, corner, x / canvas.Width, y / canvas.Height));
    }
    public void Pinch(double scale)
    {
        if (gestureStart is not null) change?.Invoke(WidgetLayerEdits.Scale(gestureStart, scale, anchor.X, anchor.Y));
    }
    public void EndInteraction(bool cancel)
    {
        var previous = gestureStart; gestureStart = null;
        if (previous is null) return;
        if (cancel) change?.Invoke(previous);
        InteractionChanged?.Invoke(this, EventArgs.Empty);
    }
    public void ReleaseSources()
    {
        EndInteraction(false);
        foreach (var photo in photos.Values) photo.Image.Source = null;
    }
    private sealed class PhotoView
    {
        public AbsoluteLayout Frame { get; } = new() { IsClippedToBounds = true, InputTransparent = true };
        public Image Image { get; } = new() { Aspect = Aspect.Fill, InputTransparent = true };
        public Border Selection { get; } = new() { StrokeThickness = 2, BackgroundColor = Colors.Transparent, InputTransparent = true };
        public PhotoView(string? path)
        {
            Image.Source = path; Selection.SetDynamicResource(Border.StrokeProperty, "AccentText");
            Frame.Children.Add(Image); Frame.Children.Add(Selection); AutomationProperties.SetIsInAccessibleTree(Frame, false);
        }
    }
    private sealed class SelectionOverlay : GraphicsView, IDrawable
    {
        public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(SelectionOverlay), Colors.Blue,
            propertyChanged: (view, _, _) => ((SelectionOverlay)view).Invalidate());
        public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
        private Rect frame;
        public Rect SelectionFrame { get => frame; set { frame = value; Invalidate(); } }
        public SelectionOverlay()
        {
            Drawable = this; InputTransparent = true; this.SetDynamicResource(TintProperty, "AccentText");
            AutomationProperties.SetIsInAccessibleTree(this, false);
        }
        public void Draw(ICanvas canvas, RectF dirty)
        {
            if (frame.Width <= 0 || frame.Height <= 0) return;
            canvas.StrokeColor = Tint; canvas.StrokeSize = 2; canvas.FillColor = Colors.White;
            foreach (var point in new[] { new Point(frame.Left, frame.Top), new Point(frame.Right, frame.Top), new Point(frame.Left, frame.Bottom), new Point(frame.Right, frame.Bottom) })
            {
                var x = (float)Math.Clamp(point.X, 6, Math.Max(6, dirty.Width - 6)); var y = (float)Math.Clamp(point.Y, 6, Math.Max(6, dirty.Height - 6));
                canvas.FillCircle(x, y, 5); canvas.DrawCircle(x, y, 5);
            }
        }
    }
}
