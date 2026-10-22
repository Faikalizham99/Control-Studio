using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetDiagnosticTests
{
    private static WidgetDiagnosticSettings Settings => new(1, Guid.NewGuid().ToString("N"), true, true, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    [Fact]
    public void SharedNativeFixturesDeserializeAndValidate()
    {
        var settings = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-diagnostics-v1.settings.json")), WidgetDesignRules.JsonContext.WidgetDiagnosticSettings)!;
        var item = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-diagnostics-v1.event.json")), WidgetDesignRules.JsonContext.WidgetDiagnosticEvent)!;
        WidgetDiagnosticRules.Validate(settings); WidgetDiagnosticRules.Validate(item);
        Assert.Equal(settings.Session, item.Session); Assert.True(settings.TextOnly);
        Assert.Equal("image.bypassed", item.Stage); Assert.Equal(2, item.Sequence);
    }
    [Fact]
    public void SettingsAndEventsUseTheNativeContractWithoutReflection()
    {
        var settings = Settings;
        var json = JsonSerializer.Serialize(settings, WidgetDesignRules.JsonContext.WidgetDiagnosticSettings);
        Assert.Contains("\"textOnly\":true", json);
        Assert.Equal(settings, JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDiagnosticSettings));
        var item = new WidgetDiagnosticEvent(1, settings.Session, Guid.NewGuid().ToString("N"), 1, settings.StartedAtUnixMilliseconds, "query", "resolve.ready", "matched=1");
        WidgetDiagnosticRules.Validate(item);
        Assert.Equal(item, JsonSerializer.Deserialize(JsonSerializer.Serialize(item, WidgetDesignRules.JsonContext.WidgetDiagnosticEvent), WidgetDesignRules.JsonContext.WidgetDiagnosticEvent));
    }
    [Fact]
    public void InvalidSettingsCannotEnableAnUnsupportedOrAmbiguousTest()
    {
        var value = Settings;
        foreach (var bad in new[] { value with { SchemaVersion = 2 }, value with { Session = "../data" }, value with { StartedAtUnixMilliseconds = long.MaxValue }, value with { Enabled = false } })
            Assert.Throws<IOException>(() => WidgetDiagnosticRules.Validate(bad));
    }
    [Theory]
    [InlineData("/private/photo.png")] [InlineData("user name")] [InlineData("line\nbreak")] [InlineData("é")]
    public void DiagnosticFieldsRejectPathsAndNonTokenContent(string detail)
    {
        var value = Settings;
        Assert.Throws<IOException>(() => WidgetDiagnosticRules.Validate(new WidgetDiagnosticEvent(1, value.Session, Guid.NewGuid().ToString("N"), 1, value.StartedAtUnixMilliseconds, "timeline", "failure", detail)));
    }
}
