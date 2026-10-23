namespace ControlStudio.App;

public interface IProfilePhotoStore
{
    string? GetPath(string key);
    Task<string?> PickAsync(bool fromLibrary);
    Task<string?> AdjustAsync(string key);
    void Remove(string key);
}
