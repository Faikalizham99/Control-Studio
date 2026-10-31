using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetTapLinkTests
{
    [Fact]
    public void NativeContractParsesAllFamiliesAndSlotBoundaries()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-tap-links-v1.example.json")));
        Assert.True(WidgetTapLink.TryParse(fixture.RootElement.GetProperty("counter").GetString(), out var counter));
        Assert.True(counter!.IsCounter);
        var slots = fixture.RootElement.GetProperty("slots").EnumerateArray().ToArray(); Assert.Equal(8, slots.Length);
        foreach (var slot in slots)
        {
            Assert.True(WidgetTapLink.TryParse(slot.GetProperty("url").GetString(), out var link));
            Assert.False(link!.IsCounter);
            var size = Enum.Parse<WidgetSize>(slot.GetProperty("size").GetString()!, ignoreCase: true);
            Assert.Equal(size, link.Size); Assert.Equal(slot.GetProperty("number").GetInt32(), link.Number);
        }
        foreach (var size in WidgetDesignRules.SupportedSizes)
        {
            var token = JsonSerializer.Serialize(size, WidgetDesignRules.JsonContext.WidgetSize).Trim('"');
            for (var number = 1; number <= 20; number++)
                Assert.True(WidgetTapLink.TryParse($"controlstudio://widget/{token}/{number}", out _));
        }
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("https://widget/small/1")]
    [InlineData("controlstudio://widget/extraLarge/1")] [InlineData("controlstudio://widget/Small/1")]
    [InlineData("controlstudio://widget/small/0")] [InlineData("controlstudio://widget/small/21")]
    [InlineData("controlstudio://widget/small/-1")] [InlineData("controlstudio://widget/small/+1")]
    [InlineData("controlstudio://widget/small/01")] [InlineData("controlstudio://widget/small/2147483648")]
    [InlineData("controlstudio://widget/small/1/")] [InlineData("controlstudio://widget/small/1?delete=true")]
    [InlineData("controlstudio://widget/small/1#edit")] [InlineData("controlstudio://counter/anything")]
    [InlineData("controlstudio://widget/small/%31")] [InlineData("controlstudio://user@widget/small/1")]
    [InlineData("controlstudio://widget:80/small/1")] [InlineData("controlstudio://widget/small/ 1")]
    [InlineData("controlstudio://widget/medium/../small/1")] [InlineData("controlstudio://widget/small\\1")]
    [InlineData("controlstudio://widget/small/1?path=/private/photos")]
    public void InvalidOrUnexpectedCommandsCannotNavigate(string? value)
    { Assert.False(WidgetTapLink.TryParse(value, out var link)); Assert.Null(link); }
    [Fact]
    public void LargeInputIsRejectedAndSchemeAndHostFollowUriCaseRules()
    {
        Assert.False(WidgetTapLink.TryParse("controlstudio://widget/" + new string('a', 1000), out _));
        Assert.True(WidgetTapLink.TryParse("CONTROLSTUDIO://WIDGET/small/1", out _));
    }
    [Fact]
    public void ResolutionUsesCurrentAddressAndNeverSubstitutesAnotherSizeOrDesign()
    {
        var small = WidgetDesignRules.New(); var medium = WidgetDesignRules.New() with { Size = WidgetSize.Medium };
        var link = new WidgetTapLink(WidgetSize.Small, 20);
        var library = new WidgetLibraryCatalog(2, [small, medium], [new(WidgetSize.Small, 20, small.Id), new(WidgetSize.Medium, 20, medium.Id)]);
        Assert.Equal(small.Id, link.Resolve(library)!.Id);
        var replacement = WidgetDesignRules.New(); library = library with { Designs = [small, medium, replacement],
            Slots = [new(WidgetSize.Small, 20, replacement.Id), new(WidgetSize.Medium, 20, medium.Id)] };
        Assert.Equal(replacement.Id, link.Resolve(library)!.Id);
        Assert.Null(link.Resolve(library with { Slots = [new(WidgetSize.Medium, 20, medium.Id)] }));
        Assert.Throws<IOException>(() => link.Resolve(library with { SchemaVersion = 99 }));
        Assert.Throws<IOException>(() => link.Resolve(library with { Slots = [new(WidgetSize.Small, 20, medium.Id)] }));
    }
}
