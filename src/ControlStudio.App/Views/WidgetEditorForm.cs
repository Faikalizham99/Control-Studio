using Microsoft.Maui.Layouts;

namespace ControlStudio.App.Views;

/// <summary>Notify the editor after its children have received their new layout.</summary>
internal sealed class WidgetEditorForm : VerticalStackLayout
{
    public event EventHandler? Arranged;
    protected override ILayoutManager CreateLayoutManager() => new Manager(base.CreateLayoutManager(), this);
    private sealed class Manager(ILayoutManager inner, WidgetEditorForm form) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint) => inner.Measure(widthConstraint, heightConstraint);
        public Size ArrangeChildren(Rect bounds)
        {
            var result = inner.ArrangeChildren(bounds);
            form.Arranged?.Invoke(form, EventArgs.Empty);
            return result;
        }
    }
}
