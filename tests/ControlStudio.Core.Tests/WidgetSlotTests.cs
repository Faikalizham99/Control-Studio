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
    public void EveryFamilyHasTwentySlotsAndSerializedPayloadFitsItsBound()
    {
        var slots = WidgetDesignRules.SupportedSizes.SelectMany(size => Enumerable.Range(1, WidgetSlotRules.SlotsPerSize).Select(number => new WidgetSlotAssignment(size, number, Guid.NewGuid()))).ToArray();
        var catalog = new WidgetSlotCatalog(1, slots); WidgetSlotRules.Validate(catalog);
        Assert.Equal(80, slots.Length);
        Assert.Equal("Extra Large 20", WidgetSlotRules.Title(WidgetSize.ExtraLargePortrait, 20));
        Assert.Throws<IOException>(() => WidgetSlotRules.ValidateAddress(WidgetSize.ExtraLarge, 1));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(catalog, WidgetDesignRules.JsonContext.WidgetSlotCatalog).Length < WidgetSlotRules.MaximumCatalogBytes);
        Assert.Throws<IOException>(() => WidgetSlotRules.Validate(new WidgetSlotCatalog(1, [.. slots, slots[0]])));
    }
    [Fact]
    public void StoredRetiredAddressesAndAllEightyCurrentAssignmentsFitTheExistingByteBound()
    {
        var slots = Enum.GetValues<WidgetSize>().SelectMany(size => Enumerable.Range(1, WidgetSlotRules.SlotsPerSize)
            .Select(number => new WidgetSlotAssignment(size, number, Guid.NewGuid()))).ToArray();
        var catalog = new WidgetSlotCatalog(1, slots);
        WidgetSlotRules.Validate(catalog);
        Assert.Equal(100, slots.Length);
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
