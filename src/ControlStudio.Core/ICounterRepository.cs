namespace ControlStudio.Core;

public interface ICounterRepository
{
    Task<CounterSnapshot> ReadAsync(CancellationToken cancellationToken = default);
    Task<CounterSnapshot> IncrementAsync(CancellationToken cancellationToken = default);
}

public sealed class CounterStorageException(string code)
    : IOException(MessageFor(code))
{
    public string Code { get; } = code;

    private static string MessageFor(string code) => code switch
    {
        "storageUnavailable" => "Shared storage is unavailable. Check App Group signing on the app and extensions.",
        "unsupportedSchema" => "The counter database was created by a newer version of Control Studio.",
        "counterOverflow" => "The counter has reached its supported maximum.",
        "databaseBusy" => "The counter is busy. Please try again.",
        _ => "The counter could not be read or saved. Please try again."
    };
}
