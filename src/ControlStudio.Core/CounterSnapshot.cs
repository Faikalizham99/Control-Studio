using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlStudio.Core;

public sealed record CounterSnapshot(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] int Value,
    [property: JsonRequired] long? UpdatedAtUnixMilliseconds,
    [property: JsonRequired] string Storage,
    [property: JsonRequired] bool RefreshRequested)
{
    public bool IsShared => Storage == "shared";
    public DateTimeOffset? UpdatedAt => UpdatedAtUnixMilliseconds is { } time
        ? DateTimeOffset.FromUnixTimeMilliseconds(time) : null;

    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("Unsupported counter schema.");
        if (Value < 0) throw new InvalidDataException("Counter cannot be negative.");
        if (Storage is not ("shared" or "local")) throw new InvalidDataException("Invalid storage scope.");
        if (UpdatedAtUnixMilliseconds is < 0 or > 253402300799999)
            throw new InvalidDataException("Invalid update timestamp.");
        if (Value > 0 && UpdatedAtUnixMilliseconds is null)
            throw new InvalidDataException("Updated counters require a timestamp.");
        if (RefreshRequested && !IsShared)
            throw new InvalidDataException("Local storage cannot refresh a shared widget.");
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CounterSnapshot))]
public partial class CounterJsonContext : JsonSerializerContext;
