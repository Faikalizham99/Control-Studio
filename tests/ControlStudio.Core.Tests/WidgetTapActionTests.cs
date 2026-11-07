using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetTapActionTests
{
    [Fact]
    public void TruncatedUnicodeNamesCannotBeSavedOrLaunchedAsADifferentName()
    {
        foreach (var name in new[] { new string('\uD800', 1), new string('\uDC00', 1), "Name" + '\uD800' + "X" })
        {
            Assert.False(WidgetTapActions.ValidName(name));
            Assert.Throws<IOException>(() => WidgetTapActions.ShortcutUri(name));
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { TapAction = WidgetTapAction.RunShortcut, ShortcutName = name }));
        }
        var emoji = new string('a', 198) + char.ConvertFromUtf32(0x1F319);
        Assert.True(WidgetTapActions.ValidName(emoji));
        Assert.Contains("%F0%9F%8C%99", WidgetTapActions.ShortcutUri(emoji).OriginalString);
    }
    [Fact]
    public void OldDesignsDefaultToEditAndNewSettingsRoundTripInEverySize()
    {
        var original = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-designs-v1.example.json")), WidgetDesignRules.JsonContext.WidgetCatalog)!;
        Assert.All(original.Designs, design => { Assert.Equal(WidgetTapAction.EditWidget, design.TapAction); Assert.Equal("", design.ShortcutName); });
        foreach (var size in WidgetDesignRules.SupportedSizes)
        {
            var design = WidgetDesignRules.New() with { Size = size, TapAction = WidgetTapAction.RunShortcut, ShortcutName = "支払い & Notes + 100% #1" };
            WidgetDesignRules.Validate(design);
            var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
            Assert.Contains("\"tapAction\":\"runShortcut\"", json);
            var saved = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
            Assert.Equal(design.TapAction, saved.TapAction); Assert.Equal(design.ShortcutName, saved.ShortcutName);
            WidgetDesignRules.Validate(saved);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.Replace("runShortcut", "unknownAction"), WidgetDesignRules.JsonContext.WidgetDesign));
        }
    }
    [Theory]
    [InlineData(" Pay & Go?input=clipboard#now ", "Pay%20%26%20Go%3Finput%3Dclipboard%23now")]
    [InlineData("A+B%20C", "A%2BB%2520C")]
    [InlineData("日本語", "%E6%97%A5%E6%9C%AC%E8%AA%9E")]
    public void ShortcutNamesAreEncodedAsOneParameterWithoutExecutingInputOrCallbacks(string name, string encoded)
    {
        var uri = WidgetTapActions.ShortcutUri(name);
        Assert.Equal("shortcuts://run-shortcut?name=" + encoded, uri.OriginalString);
        Assert.Equal("?name=" + encoded, uri.Query);
        Assert.Equal("", uri.Fragment); Assert.Equal("", uri.UserInfo);
    }
    [Theory]
    [InlineData("")] [InlineData("  ")] [InlineData("Bad\nName")] [InlineData("Bad\tName")]
    public void MissingOrControlCharacterNamesAreRejected(string name)
    {
        Assert.False(WidgetTapActions.ValidName(name));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { TapAction = WidgetTapAction.RunShortcut, ShortcutName = name }));
        Assert.Throws<IOException>(() => WidgetTapActions.ShortcutUri(name));
    }
    [Fact]
    public void NamesAreBoundedAndUnknownActionsCannotBePersisted()
    {
        Assert.False(WidgetTapActions.ValidName(new string('a', 201)));
        Assert.True(WidgetTapActions.ValidName(new string('a', 200)));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { TapAction = (WidgetTapAction)99 }));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { ShortcutName = null! }));
    }
}
