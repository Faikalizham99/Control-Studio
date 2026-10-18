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
        Assert.Equal(0, bridge.Increments);
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
        public string Read() => throw new NotSupportedException();
        public string Increment()
        {
            Increments++;
            return "{\"schemaVersion\":1,\"value\":7,\"updatedAtUnixMilliseconds\":1000,\"storage\":\"shared\",\"refreshRequested\":true}";
        }
    }
}
