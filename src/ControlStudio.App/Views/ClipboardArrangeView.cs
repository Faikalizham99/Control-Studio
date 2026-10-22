namespace ControlStudio.App.Views;

/// <summary>Native title-only reorder list; card content is never exposed here.</summary>
internal sealed class ClipboardArrangeView(ClipboardArrangement arrangement) : View
{
    public ClipboardArrangement Arrangement { get; } = arrangement;
}
