using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

/// <summary>A fixed slot address with a draft selection; choosing never writes storage.</summary>
internal sealed class WidgetSlotSelection
{
    public WidgetSize Size { get; }
    public int Number { get; }
    public Guid? SelectedId { get; private set; }
    public WidgetDesign? Selected => matching.FirstOrDefault(x => x.Id == SelectedId);
    private WidgetDesign[] matching = [];
    private bool initialized;
    public WidgetSlotSelection(WidgetSize size, int number) { WidgetSlotRules.ValidateAddress(size, number); Size = size; Number = number; }
    public void Reload(WidgetLibraryCatalog library)
    {
        matching = library.Designs.Where(x => x.Size == Size).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (!initialized) { SelectedId = library.Slots.FirstOrDefault(x => x.Size == Size && x.Number == Number)?.DesignId; initialized = true; }
        if (Selected is null) SelectedId = null;
    }
    public bool Choose(Guid id)
    {
        if (!matching.Any(x => x.Id == id)) return false;
        SelectedId = id; return true;
    }
    public IReadOnlyList<WidgetDesign> Search(string? text) => matching.Where(x => string.IsNullOrWhiteSpace(text)
        || x.Name.Contains(text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();
    public static int Columns(WidgetSize size, double width) => size is WidgetSize.Small or WidgetSize.Medium && width >= 280 ? 2 : 1;
}
