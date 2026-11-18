using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetSlotTests
{
    [Fact]
    public void SharedContractUsesStableIdsAndIndependentSizeAddressesWithoutReflection()
    {
        var catalog = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-slots-v1.example.json")), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        WidgetSlotRules.Validate(catalog);
        Assert.Equal(4, catalog.Slots.Count);
        Assert.Equal(2, catalog.Slots.Count(x => x.Size == WidgetSize.Small));
        Assert.Contains(catalog.Slots, x => x.Size == WidgetSize.ExtraLargePortrait && x.Number == 10);
        var roundtrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(catalog, WidgetDesignRules.JsonContext.WidgetSlotCatalog), WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        Assert.Equal(catalog.Slots, roundtrip.Slots);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    [InlineData(-1)]
    public void OutOfRangeSlotsCannotBeAssigned(int number) => Assert.Throws<IOException>(() => WidgetSlotRules.ValidateAddress(WidgetSize.Small, number));
    [Fact]
    public void ExtraLargeHasThirtySlotsAndOtherFamiliesKeepTwentyWithinThePayloadBound()
    {
        var slots = WidgetDesignRules.SupportedSizes.SelectMany(size => Enumerable.Range(1, WidgetSlotRules.SlotsFor(size)).Select(number => new WidgetSlotAssignment(size, number, Guid.NewGuid()))).ToArray();
        var catalog = new WidgetSlotCatalog(1, slots); WidgetSlotRules.Validate(catalog);
        Assert.Equal(90, slots.Length);
        Assert.Equal("Extra Large 30", WidgetSlotRules.Title(WidgetSize.ExtraLargePortrait, 30));
        foreach (var size in WidgetDesignRules.SupportedSizes)
        {
            var count = size == WidgetSize.ExtraLargePortrait ? 30 : 20;
            Assert.Equal(count, WidgetSlotRules.SlotsFor(size)); WidgetSlotRules.ValidateAddress(size, count);
            Assert.Throws<IOException>(() => WidgetSlotRules.ValidateAddress(size, count + 1));
        }
        Assert.Throws<IOException>(() => WidgetSlotRules.ValidateAddress(WidgetSize.ExtraLarge, 1));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetSlotCatalog).Length < WidgetSlotRules.MaximumCatalogBytes);
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, [.. slots, slots[0]])));
    }
    [Fact]
    public void RetiredTwentyAndNinetyCurrentAssignmentsFitTheExistingByteBound()
    {
        var slots = Enum.GetValues<WidgetSize>().SelectMany(size => Enumerable.Range(1, WidgetSlotRules.SlotsFor(size))
            .Select(number => new WidgetSlotAssignment(size, number, Guid.NewGuid()))).ToArray();
        var catalog = new WidgetSlotCatalog(1, slots);
        WidgetSlotRules.Validate(catalog);
        Assert.Equal(110, slots.Length); Assert.Equal(110, WidgetSlotRules.MaximumAssignments);
        Assert.Equal(20, WidgetSlotRules.SlotsFor(WidgetSize.ExtraLarge));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetSlotCatalog);
        Assert.True(bytes.Length < WidgetSlotRules.MaximumCatalogBytes);
        var restored = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetSlotCatalog)!;
        WidgetSlotRules.Validate(restored);
        Assert.Equal(catalog.Slots, restored.Slots);
    }
    [Fact]
    public void DuplicateEmptyUnknownAndFutureValuesAreRejected()
    {
        var slot = new WidgetSlotAssignment(WidgetSize.Small, 1, Guid.NewGuid());
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, [slot, slot])));
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, [slot with { DesignId = Guid.Empty }])));
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, [slot with { Size = (WidgetSize)99 }])));
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(2, [slot])));
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, null!)));
        var design = WidgetDesignRules.New();
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetLibraryCatalog(2, [design], [slot])));
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetLibraryCatalog(2, [design], [slot with { DesignId = design.Id, Size = WidgetSize.Medium }])));
    }
}
