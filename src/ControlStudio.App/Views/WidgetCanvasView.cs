using ControlStudio.Core.Widgets;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace ControlStudio.App.Views;

/// <summary>Live widget composition, using the same oriented geometry as the PNG renderer.</summary>
internal sealed class WidgetCanvasView : ContentView
{
    private const double WorkspaceInset = 32;
    private const double GuideStroke = 1;
    private readonly AbsoluteLayout workspace = new() { IsClippedToBounds = true, InputTransparent = true };
    private readonly AbsoluteLayout canvas = new() { IsClippedToBounds = true, InputTransparent = true };
    private readonly PhotoView ghost = new(null);
    private readonly Border widgetBorder;
    private readonly WidgetCompositeView composite;
    private readonly Label renderError = SettingsControls.Text("", 13);
#if IOS
    private string? ghostAsset;
#endif
    private readonly WidgetCheckerboard checker = new();
    private readonly SelectionOverlay handles = new();
    private readonly Dictionary<Guid, PhotoView> photos = [];
    private readonly Func<string, string?> path;
    private readonly Action<Guid> select;
    private readonly Action? deselect;
    private readonly Action<WidgetLayer>? change;
    private WidgetDesign? design;
    private Guid? selected;
    private WidgetLayer? gestureStart;
    private Point anchor;
    private int corner = -1;
    private VisualElement? measuredParent;
    public bool IsInteracting => gestureStart is not null;
    internal AbsoluteLayout PhotoSurface => canvas;
    internal AbsoluteLayout Workspace => workspace;
    internal Rect WidgetBounds
    {
        get
        {
            var origin = Origin(canvas, this);
            return new(origin.X, origin.Y, canvas.Width, canvas.Height);
        }
    }
    private static Point Origin(VisualElement child, VisualElement ancestor)
    {
        double x = 0, y = 0; VisualElement? element = child;
        while (element is not null && element != ancestor) { x += element.X; y += element.Y; element = element.Parent as VisualElement; }
        return new(x, y);
    }
    public event EventHandler? InteractionChanged;
    public event EventHandler<string>? RenderFailed;

    public WidgetCanvasView(Func<string, string?> path, Action<Guid> select, Action<WidgetLayer>? change = null, Action? deselect = null)
    {
        this.path = path; this.select = select; this.change = change; this.deselect = deselect;
        HeightRequest = WidthRequest = 170; HorizontalOptions = LayoutOptions.Center; AutomationId = "WidgetCanvas";
        composite = new WidgetCompositeView(path) { InputTransparent = true };
        composite.ClockTicked += (_, time) =>
        {
            if (design is null) return;
            foreach (var layer in design.Layers.Where(x => x.Kind == WidgetLayerKind.Clock))
                if (photos.TryGetValue(layer.Id, out var view)) view.Clock.Text = WidgetClocks.Text(layer.Clock!, time);
            if (design.Layers.FirstOrDefault(x => x.Id == selected) is { Kind: WidgetLayerKind.Clock } active) ghost.Clock.Text = WidgetClocks.Text(active.Clock!, time);
        };
        renderError.IsVisible = false; renderError.Margin = 12; renderError.InputTransparent = true;
        renderError.HorizontalTextAlignment = renderError.VerticalTextAlignment = TextAlignment.Center;
        composite.RenderFailed += (_, message) => { renderError.Text = message; renderError.IsVisible = true; RenderFailed?.Invoke(this, message); };
        AutomationProperties.SetIsInAccessibleTree(composite, false);
#if IOS
        // Keep the geometry surface arranged for gestures, but draw every layer through
        // the same native compositor used by export. Only the outside ghost is separate.
        canvas.Opacity = 0;
#else
        composite.IsVisible = false;
#endif
        widgetBorder = new Border { Content = new Grid { Children = { checker, canvas, composite, renderError } },
            StrokeShape = new RoundRectangle { CornerRadius = 22 }, StrokeThickness = GuideStroke };
        SettingsControls.SurfaceStroke(widgetBorder);
        ghost.Frame.Opacity = .22; ghost.Frame.IsVisible = false;
        workspace.Children.Add(ghost.Frame); workspace.Children.Add(widgetBorder); workspace.Children.Add(handles);
        AbsoluteLayout.SetLayoutFlags(handles, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(handles, new Rect(0, 0, 1, 1));
        var border = new Border { Content = workspace, StrokeShape = new RoundRectangle { CornerRadius = 16 }, StrokeThickness = GuideStroke };
        border.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#E5E8EF"), Color.FromArgb("#101319"));
        SettingsControls.SurfaceStroke(border); Content = border;
        canvas.SizeChanged += (_, _) => { EndInteraction(true); ArrangePhotos(); };
        widgetBorder.SizeChanged += (_, _) => ArrangePhotos();
        SemanticProperties.SetHint(this, "Tap a layer to select it, or empty space to deselect. Locked layers cannot be moved or resized; unlock them in Layers. Drag past the widget edges to hide part of an unlocked layer. The faint outside area is not saved.");
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
        var available = measuredParent?.Width > 0 ? measuredParent.Width : 300;
        // Border.CrossPlatformArrange subtracts its stroke on each side. Reserve both
        // guides outside the photo area so the actual canvas keeps the exported aspect ratio.
        const double inset = WorkspaceInset;
        var extra = inset * 2 + GuideStroke * 4;
        // Fit the widget itself first. The surrounding workspace may scroll;
        // its margin must never be deducted from the widget's allotted width.
        var size = WidgetPreviewFrame.Fit(design, Math.Max(1, available - GuideStroke * 4));
        AbsoluteLayout.SetLayoutBounds(widgetBorder, new Rect(inset, inset, size.Width + GuideStroke * 2, size.Height + GuideStroke * 2));
        if (Math.Abs(WidthRequest - size.Width - extra) > .5) WidthRequest = size.Width + extra;
        if (Math.Abs(HeightRequest - size.Height - extra) > .5) HeightRequest = size.Height + extra;
        ArrangePhotos();
    }
    public void Update(WidgetDesign value, Guid? selectedId)
    {
        if (gestureStart is not null && (design?.Size != value.Size || selected != selectedId || !value.Layers.Any(x => x.Id == gestureStart.Id && x.Visible && !x.Locked))) EndInteraction(false);
        design = value; selected = selectedId;
        if (!Equals(composite.Design, value)) renderError.IsVisible = false;
        composite.Design = value;
        if (!value.Layers.Any(x => x.Id == selectedId && x.Visible)) { ghost.Image.Source = null; ghost.Frame.IsVisible = false; }
        checker.IsVisible = value.TransparentBackground;
        canvas.BackgroundColor = value.TransparentBackground ? Colors.Transparent : Color.FromArgb(value.BackgroundHex);
        foreach (var id in photos.Keys.Where(id => !value.Layers.Any(x => x.Id == id)).ToArray())
        { canvas.Children.Remove(photos[id].Frame); photos[id].Image.Source = null; photos.Remove(id); }
        for (var i = 0; i < value.Layers.Count; i++)
        {
            var layer = value.Layers[i];
            if (!photos.TryGetValue(layer.Id, out var photo))
            {
#if IOS
                photo = new PhotoView(null);
#else
                photo = new PhotoView(layer.Kind == WidgetLayerKind.Photo ? path(layer.AssetKey) : null);
#endif
                photos.Add(layer.Id, photo); canvas.Children.Add(photo.Frame);
            }
            var current = canvas.Children.IndexOf(photo.Frame);
            if (current != i) { canvas.Children.RemoveAt(current); canvas.Children.Insert(i, photo.Frame); }
            photo.Frame.IsVisible = layer.Visible; Configure(photo, layer);
        }
        FitCanvas();
        var active = value.Layers.FirstOrDefault(x => x.Id == selectedId && x.Visible);
        var mixed = value.Layers.Any(x => x.Kind == WidgetLayerKind.Clock);
        SemanticProperties.SetDescription(this, $"{WidgetDesignRules.SizeName(value.Size)} widget preview. {value.Layers.Count(x => x.Visible)} visible {(mixed ? "layers" : "photo layers")}."
            + (active is null ? mixed ? " No layer selected." : " No photo selected." : $" Selected {active.Name}." + (active.Locked ? " Locked. Unlock in Layers to move or resize." : "")));
    }
    private void ArrangePhotos()
    {
        if (design is null || canvas.Width <= 0 || canvas.Height <= 0) return;
        foreach (var layer in design.Layers)
        {
            Place(photos[layer.Id], layer, 0, 0);
        }
        var active = design.Layers.FirstOrDefault(x => x.Id == selected && x.Visible);
        var origin = Origin(canvas, workspace);
        ghost.Frame.IsVisible = active is not null;
        if (active is null)
        {
            ghost.Image.Source = null;
#if IOS
            ghostAsset = null;
#endif
        }
        else
        {
#if IOS
            if (active.Kind == WidgetLayerKind.Photo && (ghostAsset != active.AssetKey || ghost.Image.Source is null)) ghost.Image.Source = path(active.AssetKey);
            ghostAsset = active.AssetKey;
#else
            // One selected-layer ghost reuses the existing source, rather than duplicating every layer.
            if (!ReferenceEquals(ghost.Image.Source, photos[active.Id].Image.Source)) ghost.Image.Source = photos[active.Id].Image.Source;
#endif
            Configure(ghost, active); Place(ghost, active, origin.X, origin.Y);
        }
        handles.SelectionFrame = active is null ? Rect.Zero : new Rect(origin.X + active.Frame.X * canvas.Width, origin.Y + active.Frame.Y * canvas.Height,
            active.Frame.Width * canvas.Width, active.Frame.Height * canvas.Height);
        handles.CanResize = active?.Locked != true;
    }
    private static void Configure(PhotoView photo, WidgetLayer layer)
    {
        photo.Image.IsVisible = layer.Kind == WidgetLayerKind.Photo; photo.Clock.IsVisible = layer.Kind == WidgetLayerKind.Clock;
        if (layer.Clock is { } clock)
        {
            photo.Clock.Text = WidgetClocks.Text(clock, DateTimeOffset.Now); photo.Clock.TextColor = Color.FromArgb(clock.ColourHex);
            photo.Clock.Opacity = layer.Opacity; photo.Clock.Typeface = clock;
            photo.Clock.HorizontalTextAlignment = clock.Alignment switch { WidgetClockAlignment.Left => TextAlignment.Start, WidgetClockAlignment.Right => TextAlignment.End, _ => TextAlignment.Center };
        }
        photo.Image.Opacity = layer.Opacity; photo.Image.Rotation = layer.QuarterTurns * 90;
        var odd = layer.QuarterTurns % 2 != 0;
        photo.Image.ScaleX = (odd ? layer.FlipVertical : layer.FlipHorizontal) ? -1 : 1;
        photo.Image.ScaleY = (odd ? layer.FlipHorizontal : layer.FlipVertical) ? -1 : 1;
    }
    private void Place(PhotoView photo, WidgetLayer layer, double x, double y)
    {
        var width = layer.Frame.Width * canvas.Width; var height = layer.Frame.Height * canvas.Height;
        AbsoluteLayout.SetLayoutBounds(photo.Frame, new Rect(x + layer.Frame.X * canvas.Width, y + layer.Frame.Y * canvas.Height, width, height));
        if (layer.Clock is { } clock)
        {
            photo.Clock.FontSize = Math.Max(.5, Math.Min(height * clock.FontScale, width / Math.Max(1, photo.Clock.Text.Length * .65)));
            AbsoluteLayout.SetLayoutBounds(photo.Clock, new Rect(0, 0, width, height)); return;
        }
        var placement = WidgetPhotoGeometry.Image(layer, width, height); var odd = layer.QuarterTurns % 2 != 0;
        var imageWidth = odd ? placement.Height : placement.Width; var imageHeight = odd ? placement.Width : placement.Height;
        AbsoluteLayout.SetLayoutBounds(photo.Image, new Rect(placement.X + (placement.Width - imageWidth) / 2,
            placement.Y + (placement.Height - imageHeight) / 2, imageWidth, imageHeight));
    }
    private Point Normalize(double x, double y)
    {
        VisualElement? element = canvas;
        while (element is not null && element != this) { x -= element.X; y -= element.Y; element = element.Parent as VisualElement; }
        return new(x / canvas.Width, y / canvas.Height);
    }
    public void SelectAt(double x, double y)
    {
        if (!IsEnabled || IsInteracting || design is null || canvas.Width <= 0 || canvas.Height <= 0 || !double.IsFinite(x) || !double.IsFinite(y)) return;
        var point = Normalize(x, y);
        var layer = design.Layers.LastOrDefault(layer => WidgetLayerEdits.Contains(layer, point.X, point.Y));
        if (layer is not null) select(layer.Id); else deselect?.Invoke();
    }
    public bool BeginInteraction(double x, double y, bool pinch)
    {
        if (!IsEnabled || IsInteracting || change is null || design is null || canvas.Width <= 0 || canvas.Height <= 0 || !double.IsFinite(x) || !double.IsFinite(y)) return false;
        var point = Normalize(x, y); corner = -1;
        var layer = design.Layers.FirstOrDefault(layer => layer.Id == selected && layer.Visible);
        if (layer is not null && !layer.Locked && !pinch)
        {
            var frame = layer.Frame;
            var corners = new[] { new Point(frame.X, frame.Y), new Point(frame.X + frame.Width, frame.Y),
                new Point(frame.X, frame.Y + frame.Height), new Point(frame.X + frame.Width, frame.Y + frame.Height) };
            var radius = Math.Min(22, Math.Min(frame.Width * canvas.Width, frame.Height * canvas.Height) / 3);
            var nearest = corners.Select((p, index) => (Index: index, Distance: Math.Pow((p.X - point.X) * canvas.Width, 2) + Math.Pow((p.Y - point.Y) * canvas.Height, 2)))
                .OrderBy(x => x.Distance).First();
            if (nearest.Distance <= radius * radius) corner = nearest.Index;
        }
        if (corner < 0) layer = design.Layers.LastOrDefault(layer => WidgetLayerEdits.Contains(layer, point.X, point.Y))
            ?? (pinch ? design.Layers.FirstOrDefault(x => x.Id == selected && x.Visible && x.Opacity > 0 && !x.Locked) : null);
        if (layer is null || layer.Locked) return false;
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
    public void Pinch(double scale, double dx = 0, double dy = 0)
    {
        if (!double.IsFinite(scale) || scale <= 0 || !double.IsFinite(dx) || !double.IsFinite(dy)) return;
        if (gestureStart is not null && canvas.Width > 0 && canvas.Height > 0)
            change?.Invoke(gestureStart.Kind == WidgetLayerKind.Clock ? WidgetLayerEdits.ScaleCentered(gestureStart, scale)
                : WidgetLayerEdits.Move(WidgetLayerEdits.Scale(gestureStart, scale, anchor.X, anchor.Y), dx / canvas.Width, dy / canvas.Height));
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
        composite.Design = null;
        ghost.Image.Source = null; ghost.Frame.IsVisible = false;
#if IOS
        ghostAsset = null;
#endif
        foreach (var photo in photos.Values) photo.Image.Source = null;
    }
    public void SuspendComposition() => composite.Design = null;
    private sealed class PhotoView
    {
        public AbsoluteLayout Frame { get; } = new() { IsClippedToBounds = true, InputTransparent = true };
        public Image Image { get; } = new() { Aspect = Aspect.Fill, InputTransparent = true };
        public WidgetClockFontLabel Clock { get; } = new() { IsVisible = false, InputTransparent = true, MaxLines = 1, FontAutoScalingEnabled = false,
            VerticalTextAlignment = TextAlignment.Center };
        public PhotoView(string? path)
        {
            Image.Source = path;
            Frame.Children.Add(Image); Frame.Children.Add(Clock); AutomationProperties.SetIsInAccessibleTree(Frame, false);
        }
    }
    private sealed class SelectionOverlay : GraphicsView, IDrawable
    {
        public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(SelectionOverlay), Colors.Blue,
            propertyChanged: (view, _, _) => ((SelectionOverlay)view).Invalidate());
        public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
        private Rect frame;
        private bool canResize = true;
        public bool CanResize { get => canResize; set { canResize = value; Invalidate(); } }
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
            canvas.DrawRectangle(frame);
            if (!canResize) return;
            foreach (var point in new[] { new Point(frame.Left, frame.Top), new Point(frame.Right, frame.Top), new Point(frame.Left, frame.Bottom), new Point(frame.Right, frame.Bottom) })
            {
                canvas.FillCircle((float)point.X, (float)point.Y, 5); canvas.DrawCircle((float)point.X, (float)point.Y, 5);
            }
        }
    }
}
