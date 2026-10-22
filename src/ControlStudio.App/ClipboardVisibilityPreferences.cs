using Microsoft.Maui.Storage;

namespace ControlStudio.App;

/// <summary>Private, persistent presentation state, independent of item content and ordering.</summary>
internal sealed class ClipboardVisibilityPreferences(IPreferences preferences)
{
    public bool IsConcealed(Guid id) => preferences.Get(Key(id), false);
    public void SetConcealed(Guid id, bool concealed)
    {
        if (concealed) preferences.Set(Key(id), true);
        else Remove(id);
    }
    public void Remove(Guid id) => preferences.Remove(Key(id));
    private static string Key(Guid id) => id != Guid.Empty ? $"clipboard.concealed.{id:N}" : throw new ArgumentException("A saved item ID is required.", nameof(id));
}
