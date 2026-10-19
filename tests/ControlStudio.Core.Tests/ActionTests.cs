using System.Text.Json;
using ControlStudio.Core.Actions;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class ActionTests
{
    private static ActionDefinition Action(ActionParameters parameters) => new(Guid.NewGuid(), "My action", parameters);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/plain,test")]
    [InlineData("shortcuts://run-shortcut?name=Test")]
    [InlineData("https://user:password@example.com")]
    [InlineData("example.com")]
    [InlineData("https://example.com/has space")]
    [InlineData("https://example.com/\n")]
    [InlineData("https:\\example.com")]
    public void UnsafeOrUnsupportedUrlsAreRejected(string url) =>
        Assert.Throws<ActionValidationException>(() => new ActionValidator().Validate(Action(new OpenUrlParameters(url))));

    [Theory]
    [InlineData("https://example.com/a?q=hello%20world#top")]
    [InlineData("http://192.168.1.2:8080/")]
    [InlineData("https://例え.テスト/")]
    public void WebLinksAreAccepted(string url) => new ActionValidator().Validate(Action(new OpenUrlParameters(url)));

    [Fact]
    public void TypedParametersRoundTripWithoutReflection()
    {
        var actions = new List<ActionDefinition> { Action(new OpenUrlParameters("https://example.com")),
            Action(new CopyTextParameters("Line 1\nUnicode 🐱")), Action(new IncrementCounterParameters()) };
        var json = JsonSerializer.Serialize(new DashboardDocument(1, actions), ActionJsonContext.Default.DashboardDocument);
        var result = JsonSerializer.Deserialize(json, ActionJsonContext.Default.DashboardDocument)!;
        Assert.Equal(actions, result.Actions);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.Replace("openUrl", "runScript"), ActionJsonContext.Default.DashboardDocument));
    }

    [Fact]
    public void NamesAndParametersAreBounded()
    {
        var validator = new ActionValidator();
        var action = Action(new IncrementCounterParameters());
        foreach (var invalid in new[] { action with { Name = " " }, action with { Name = new string('a', 61) },
            action with { Id = Guid.Empty }, action with { Color = (ActionColor)200 }, action with { Category = (ActionCategory)200 },
            action with { Parameters = new CopyTextParameters("a\0b") }, action with { Parameters = new CopyTextParameters(new string('x', 10_001)) },
            action with { Parameters = null! }, action with { Parameters = new OpenUrlParameters(null!) } })
            Assert.Throws<ActionValidationException>(() => validator.Validate(invalid));
    }

    [Fact]
    public async Task InvalidOrCancelledActionsHaveNoSideEffects()
    {
        var platform = new Platform(); var counter = new Counter(); var executor = new ActionExecutor(new ActionValidator(), platform, counter);
        await Assert.ThrowsAsync<ActionValidationException>(() => executor.ExecuteAsync(Action(new OpenUrlParameters("file:///tmp"))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(Action(new IncrementCounterParameters()), new CancellationToken(true)));
        Assert.Equal(0, platform.Calls); Assert.Equal(0, counter.Value);
    }

    [Fact]
    public async Task ExecutesOnlyTheTypedOperation()
    {
        var platform = new Platform(); var counter = new Counter(); var executor = new ActionExecutor(new ActionValidator(), platform, counter);
        await executor.ExecuteAsync(Action(new CopyTextParameters("exact\ntext")));
        Assert.Equal("exact\ntext", platform.Copied); Assert.Equal(0, counter.Value);
        await executor.ExecuteAsync(Action(new OpenUrlParameters("https://example.com")));
        Assert.Equal("example.com", platform.Opened!.Host);
        var result = await executor.ExecuteAsync(Action(new IncrementCounterParameters()));
        Assert.Equal(1, counter.Value); Assert.Contains("Widget refresh requested", result.Message);
    }

    [Fact]
    public async Task PlatformFailureDoesNotExposeSensitiveException()
    {
        var executor = new ActionExecutor(new ActionValidator(), new Platform { Fail = true }, new Counter());
        var error = await Assert.ThrowsAsync<ActionExecutionException>(() => executor.ExecuteAsync(Action(new CopyTextParameters("secret"))));
        Assert.DoesNotContain("secret", error.Message);
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task RefusedUrlIsReportedAsFailure()
    {
        var executor = new ActionExecutor(new ActionValidator(), new Platform { CanOpen = false }, new Counter());
        await Assert.ThrowsAsync<ActionExecutionException>(() => executor.ExecuteAsync(Action(new OpenUrlParameters("https://example.com"))));
    }

    private sealed class Platform : IActionPlatform
    {
        public int Calls; public string? Copied; public Uri? Opened; public bool Fail; public bool CanOpen = true;
        public Task CopyTextAsync(string text, CancellationToken cancellationToken) { Calls++; if (Fail) throw new Exception("secret"); Copied = text; return Task.CompletedTask; }
        public Task<bool> OpenUrlAsync(Uri uri, CancellationToken cancellationToken) { Calls++; Opened = uri; return Task.FromResult(CanOpen); }
    }
    private sealed class Counter : ICounterRepository
    {
        public int Value;
        public Task<CounterSnapshot> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new CounterSnapshot(1, Value, 1, "shared", true));
        public Task<CounterSnapshot> IncrementAsync(CancellationToken cancellationToken = default) { Value++; return ReadAsync(cancellationToken); }
    }
}
