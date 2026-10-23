using ControlStudio.Core;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class NativeCounterRepositoryTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":1,\"value\":0,\"storage\":\"shared\",\"refreshRequested\":false}")]
    public void RejectsMalformedOrIncompleteJson(string json) =>
        Assert.Throws<InvalidDataException>(() => NativeCounterRepository.Parse(json));

    [Fact]
    public void NativeErrorsAreActionable()
    {
        var ex = Assert.Throws<CounterStorageException>(() => NativeCounterRepository.Parse("{\"error\":\"storageUnavailable\"}"));
        Assert.Equal("storageUnavailable", ex.Code);
        Assert.Contains("App Group", ex.Message);
    }

    [Fact]
    public async Task CancelledRequestNeverInvokesNativeMutation()
    {
        var bridge = new RecordingBridge();
        var repository = new NativeCounterRepository(bridge);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.IncrementAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ResetAsync(cancellation.Token));
        Assert.Equal(0, bridge.Increments);
        Assert.Equal(0, bridge.Resets);
    }

    [Fact]
    public async Task CancellationAfterNativeResetStartsDoesNotHideCommittedResult()
    {
        using var cancelled = new CancellationTokenSource();
        var bridge = new RecordingBridge { OnReset = cancelled.Cancel };
        var result = await new NativeCounterRepository(bridge).ResetAsync(cancelled.Token);
        Assert.True(cancelled.IsCancellationRequested); Assert.Equal(0, result.Value); Assert.Equal(1, bridge.Resets);
    }
    [Fact]
    public async Task ResetUsesItsNativeTransactionAndReturnsPersistedZero()
    {
        var bridge = new RecordingBridge();
        var result = await new NativeCounterRepository(bridge).ResetAsync();
        Assert.Equal(1, bridge.Resets); Assert.Equal(0, bridge.Increments);
        Assert.Equal(0, result.Value); Assert.Equal(2000, result.UpdatedAtUnixMilliseconds);
        Assert.True(result.IsShared); Assert.True(result.RefreshRequested);
    }

    [Fact]
    public async Task IncrementUsesNativeTransactionAndReturnsCommittedValue()
    {
        var bridge = new RecordingBridge();
        var snapshot = await new NativeCounterRepository(bridge).IncrementAsync();
        Assert.Equal(1, bridge.Increments);
        Assert.Equal(7, snapshot.Value);
        Assert.True(snapshot.RefreshRequested);
    }

    [Fact]
    public void LocalFallbackDoesNotClaimSharedAccess()
    {
        var snapshot = NativeCounterRepository.Parse("{\"schemaVersion\":1,\"value\":0,\"updatedAtUnixMilliseconds\":null,\"storage\":\"local\",\"refreshRequested\":false}");
        Assert.False(snapshot.IsShared);
    }

    private sealed class RecordingBridge : INativeCounterBridge
    {
        public int Increments { get; private set; }
        public int Resets { get; private set; }
        public Action? OnReset { get; init; }
        public string Read() => throw new NotSupportedException();
        public string Increment()
        {
            Increments++;
            return "{\"schemaVersion\":1,\"value\":7,\"updatedAtUnixMilliseconds\":1000,\"storage\":\"shared\",\"refreshRequested\":true}";
        }
        public string Reset()
        {
            Resets++;
            OnReset?.Invoke();
            return "{\"schemaVersion\":1,\"value\":0,\"updatedAtUnixMilliseconds\":2000,\"storage\":\"shared\",\"refreshRequested\":true}";
        }
    }
}
