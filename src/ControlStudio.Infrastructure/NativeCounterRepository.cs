using System.Text.Json;
using ControlStudio.Core;

namespace ControlStudio.Infrastructure;

public interface INativeCounterBridge
{
    string Read();
    string Increment();
}

/// <summary>The native store owns transactions, persistence and widget refresh requests.</summary>
public sealed class NativeCounterRepository(INativeCounterBridge bridge) : ICounterRepository
{
    // Cancellation is honored before starting. A committed increment is never reported as cancelled.
    public Task<CounterSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(bridge.Read, cancellationToken);

    public Task<CounterSnapshot> IncrementAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(bridge.Increment, cancellationToken);

    private static Task<CounterSnapshot> ExecuteAsync(Func<string> operation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.Run(() => Parse(operation()), token);
    }

    public static CounterSnapshot Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("The native counter response must be an object.");
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException("Invalid native error response.");
                throw new CounterStorageException(error.GetString() ?? "unknown");
            }
            var snapshot = JsonSerializer.Deserialize(json, CounterJsonContext.Default.CounterSnapshot)
                ?? throw new InvalidDataException("Empty counter response.");
            snapshot.Validate();
            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The native counter response is invalid.", ex);
        }
    }
}
