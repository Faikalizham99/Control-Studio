namespace ControlStudio.App;

public interface IColourPicker
{
    Task<string> PickAsync(string initialHex, Action<string> preview);
}
