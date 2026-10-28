using Microsoft.Maui.Storage;

namespace ControlStudio.App.Tests;

/// <summary>Retained preferences backing across simulated store/page/application recreation.</summary>
internal sealed class TestPreferences(Dictionary<string, object>? values = null) : IPreferences
{
    public Dictionary<string, object> Values { get; } = values ?? [];
    public bool FailReads { get; set; }
    public bool FailWrites { get; set; }
    public string? FailWriteKey { get; set; }
    public bool ContainsKey(string key, string? sharedName = null) => Values.ContainsKey(key);
    public void Remove(string key, string? sharedName = null) { if (FailWrites) throw new IOException("Test write failure."); Values.Remove(key); }
    public void Clear(string? sharedName = null) => Values.Clear();
    public void Set<T>(string key, T value, string? sharedName = null)
    {
        if (FailWrites || key == FailWriteKey) throw new IOException("Test write failure.");
        Values[key] = value!;
    }
    public T Get<T>(string key, T defaultValue, string? sharedName = null)
    {
        if (FailReads) throw new IOException("Test read failure.");
        return Values.TryGetValue(key, out var value) ? (T)value : defaultValue;
    }
}
