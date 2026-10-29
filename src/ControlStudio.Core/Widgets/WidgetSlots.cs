namespace ControlStudio.Core.Widgets;

public sealed record WidgetSlotAssignment(WidgetSize Size, int Number, Guid DesignId);
public sealed record WidgetSlotCatalog(int SchemaVersion, IReadOnlyList<WidgetSlotAssignment> Slots);
public sealed record WidgetLibraryCatalog(int SchemaVersion, IReadOnlyList<WidgetDesign> Designs, IReadOnlyList<WidgetSlotAssignment> Slots);

public static class WidgetSlotRules
{
// BEGIN GENERATED SLOTS limits (scripts/widget_slot_config.py)
    public const int SlotsPerSize = 20, MaximumAssignments = 100, MaximumCatalogBytes = 16384;
// END GENERATED SLOTS limits
    public static void ValidateAddress(WidgetSize size, int number)
    {
        if (!WidgetDesignRules.IsSupported(size)) throw new IOException("Choose a supported iPhone widget size.");
        ValidateStoredAddress(size, number);
    }
    private static void ValidateStoredAddress(WidgetSize size, int number)
    {
        if (!Enum.IsDefined(size) || number is < 1 or > SlotsPerSize)
            throw new IOException($"Choose a supported widget size and a slot from 1 to {SlotsPerSize}.");
    }
    public static void Validate(WidgetSlotCatalog catalog)
    {
        if (catalog.SchemaVersion != 1 || catalog.Slots is null || catalog.Slots.Count > MaximumAssignments)
            throw new IOException("The widget slots are invalid or require a newer app. Data was not changed.");
        var addresses = new HashSet<(WidgetSize, int)>();
        foreach (var slot in catalog.Slots)
        {
            if (slot is null || slot.DesignId == Guid.Empty) throw new IOException("A widget slot is invalid. Data was not changed.");
            // Retired addresses stay readable until their design is resized/deleted.
            ValidateStoredAddress(slot.Size, slot.Number);
            if (!addresses.Add((slot.Size, slot.Number))) throw new IOException("A widget slot is duplicated. Data was not changed.");
        }
    }
    public static void Validate(WidgetLibraryCatalog library)
    {
        if (library.SchemaVersion != 2) throw new IOException("This widget library requires a newer app. Data was not changed.");
        WidgetDesignRules.Validate(new WidgetCatalog(1, library.Designs));
        Validate(new WidgetSlotCatalog(1, library.Slots));
        foreach (var slot in library.Slots)
            if (!library.Designs.Any(x => x.Id == slot.DesignId && x.Size == slot.Size))
                throw new IOException("A widget slot references an unavailable design. Data was not changed.");
    }
    public static string Title(WidgetSize size, int number)
    {
        return $"{WidgetDesignRules.SizeName(size)} {number}";
    }
}
