using System.Text.Json;
using ControlStudio.Core;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class CounterSnapshotTests
{
    [Fact]
    public void SharedContractRoundTrips()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "counter-v1.example.json"));
        var snapshot = JsonSerializer.Deserialize(json, CounterJsonContext.Default.CounterSnapshot)!;
        snapshot.Validate();
        Assert.Equal(12, snapshot.Value);
        Assert.True(snapshot.IsShared);
        Assert.Equal(snapshot, JsonSerializer.Deserialize(JsonSerializer.Serialize(snapshot,
            CounterJsonContext.Default.CounterSnapshot), CounterJsonContext.Default.CounterSnapshot));
    }

    [Fact]
    public void InitialSnapshotHasNoTimestamp()
    {
        var snapshot = new CounterSnapshot(1, 0, null, "shared", false);
        snapshot.Validate();
        Assert.Null(snapshot.UpdatedAt);
    }

    [Theory]
    [InlineData(2, 0, null, "shared", false)]
    [InlineData(1, -1, 0L, "shared", false)]
    [InlineData(1, 1, null, "shared", false)]
    [InlineData(1, 1, -1L, "shared", false)]
    [InlineData(1, 1, 253402300800000L, "shared", false)]
    [InlineData(1, 1, 1L, "remote", false)]
    [InlineData(1, 1, 1L, "local", true)]
    public void RejectsInvalidSnapshots(int schema, int value, long? time, string storage, bool refresh) =>
        Assert.Throws<InvalidDataException>(() => new CounterSnapshot(schema, value, time, storage, refresh).Validate());
}
