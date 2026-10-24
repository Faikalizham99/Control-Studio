using Microsoft.Maui.Layouts;
using CoreAlignment = Microsoft.Maui.Primitives.LayoutAlignment;

namespace ControlStudio.App.Views;

internal static class ActionSpacing
{
    public const double Gap = 8;
}

/// <summary>Natural-size actions, aligned to the trailing edge and wrapped without shrinking tap targets.</summary>
internal sealed class ActionButtonRow : Layout
{
    public static readonly BindableProperty AlignmentProperty = BindableProperty.Create(nameof(Alignment), typeof(TextAlignment), typeof(ActionButtonRow), TextAlignment.End,
        propertyChanged: (view, _, _) => ((ActionButtonRow)view).InvalidateMeasure());
    public TextAlignment Alignment { get => (TextAlignment)GetValue(AlignmentProperty); set => SetValue(AlignmentProperty, value); }
    public ActionButtonRow(params View[] buttons)
    {
        foreach (var button in buttons) Children.Add(button);
    }
    protected override ILayoutManager CreateLayoutManager() => new Manager(this);
    private sealed record Line(List<IView> Items, double Width, double Height);
    private sealed class Manager(ActionButtonRow row) : LayoutManager(row)
    {
        private List<Line> Lines(double width)
        {
            var result = new List<Line>(); var items = new List<IView>(); double used = 0, height = 0;
            foreach (var child in Layout.Where(x => x.Visibility != Visibility.Collapsed))
            {
                var size = child.DesiredSize;
                var next = used + (items.Count > 0 ? ActionSpacing.Gap : 0) + size.Width;
                if (items.Count > 0 && next > width + .01)
                { result.Add(new(items, used, height)); items = []; used = 0; height = 0; }
                used += (items.Count > 0 ? ActionSpacing.Gap : 0) + size.Width;
                height = Math.Max(height, size.Height); items.Add(child);
            }
            if (items.Count > 0) result.Add(new(items, used, height));
            return result;
        }
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            foreach (var child in Layout.Where(x => x.Visibility != Visibility.Collapsed)) child.Measure(double.PositiveInfinity, double.PositiveInfinity);
            var padding = Layout.Padding;
            var lines = Lines(Math.Max(0, widthConstraint - padding.HorizontalThickness));
            var width = lines.Count == 0 ? 0 : lines.Max(x => x.Width);
            var height = lines.Sum(x => x.Height) + Math.Max(0, lines.Count - 1) * ActionSpacing.Gap;
            return new(ResolveConstraints(widthConstraint, Layout.Width, width + padding.HorizontalThickness, Layout.MinimumWidth, Layout.MaximumWidth),
                ResolveConstraints(heightConstraint, Layout.Height, height + padding.VerticalThickness, Layout.MinimumHeight, Layout.MaximumHeight));
        }
        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = Layout.Padding; var width = Math.Max(0, bounds.Width - padding.HorizontalThickness);
            var rtl = Layout.FlowDirection == Microsoft.Maui.FlowDirection.RightToLeft;
            var y = bounds.Y + padding.Top;
            foreach (var line in Lines(width))
            {
                var spare = Math.Max(0, width - line.Width);
                var offset = row.Alignment == TextAlignment.Center ? spare / 2 : row.Alignment == TextAlignment.Start ? (rtl ? spare : 0) : (rtl ? 0 : spare);
                var x = bounds.X + padding.Left + offset + (rtl ? line.Width : 0);
                foreach (var child in line.Items)
                {
                    var size = child.DesiredSize;
                    if (rtl) x -= size.Width;
                    var dy = child.VerticalLayoutAlignment == CoreAlignment.Start ? 0 : child.VerticalLayoutAlignment == CoreAlignment.End
                        ? line.Height - size.Height : (line.Height - size.Height) / 2;
                    child.Arrange(new Rect(x, y + dy, size.Width, size.Height));
                    x += rtl ? -ActionSpacing.Gap : size.Width + ActionSpacing.Gap;
                }
                y += line.Height + ActionSpacing.Gap;
            }
            return bounds.Size;
        }
    }
}

/// <summary>Moves actions below the title when native text measurement needs more room.</summary>
internal sealed class CardActionHeader : Layout
{
    public CardActionHeader(View title, View actions) { Children.Add(title); Children.Add(actions); }
    protected override ILayoutManager CreateLayoutManager() => new Manager(this);
    private sealed class Manager(CardActionHeader header) : LayoutManager(header)
    {
        private IView Title => Layout[0];
        private IView Actions => Layout[1];
        private bool stacked;
        private double measuredWidth = double.NaN;
        private Size MeasureContent(double width)
        {
            measuredWidth = width;
            Title.Measure(width, double.PositiveInfinity); Actions.Measure(width, double.PositiveInfinity);
            stacked = Title.DesiredSize.Width + Actions.DesiredSize.Width + ActionSpacing.Gap > width + .01;
            if (!stacked)
            {
                Title.Measure(Math.Max(0, width - Actions.DesiredSize.Width - ActionSpacing.Gap), double.PositiveInfinity);
                return new(Title.DesiredSize.Width + Actions.DesiredSize.Width + ActionSpacing.Gap, Math.Max(Title.DesiredSize.Height, Actions.DesiredSize.Height));
            }
            return new(Math.Max(Title.DesiredSize.Width, Actions.DesiredSize.Width), Title.DesiredSize.Height + ActionSpacing.Gap + Actions.DesiredSize.Height);
        }
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = Layout.Padding; var size = MeasureContent(Math.Max(0, widthConstraint - padding.HorizontalThickness));
            return new(ResolveConstraints(widthConstraint, Layout.Width, size.Width + padding.HorizontalThickness, Layout.MinimumWidth, Layout.MaximumWidth),
                ResolveConstraints(heightConstraint, Layout.Height, size.Height + padding.VerticalThickness, Layout.MinimumHeight, Layout.MaximumHeight));
        }
        public override Size ArrangeChildren(Rect bounds)
        {
            var padding = Layout.Padding; var width = Math.Max(0, bounds.Width - padding.HorizontalThickness);
            if (width != measuredWidth) MeasureContent(width);
            var x = bounds.X + padding.Left; var y = bounds.Y + padding.Top;
            if (stacked)
            {
                Title.Arrange(new Rect(x, y, width, Title.DesiredSize.Height));
                Actions.Arrange(new Rect(x, y + Title.DesiredSize.Height + ActionSpacing.Gap, width, Actions.DesiredSize.Height));
            }
            else
            {
                var actionWidth = Actions.DesiredSize.Width; var titleWidth = Math.Max(0, width - actionWidth - ActionSpacing.Gap);
                var height = Math.Max(Title.DesiredSize.Height, Actions.DesiredSize.Height);
                var rtl = Layout.FlowDirection == Microsoft.Maui.FlowDirection.RightToLeft;
                Title.Arrange(new Rect(x + (rtl ? actionWidth + ActionSpacing.Gap : 0), y + (height - Title.DesiredSize.Height) / 2, titleWidth, Title.DesiredSize.Height));
                Actions.Arrange(new Rect(x + (rtl ? 0 : titleWidth + ActionSpacing.Gap), y + (height - Actions.DesiredSize.Height) / 2, actionWidth, Actions.DesiredSize.Height));
            }
            return bounds.Size;
        }
    }
}
