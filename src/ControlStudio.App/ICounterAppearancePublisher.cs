namespace ControlStudio.App;

/// <summary>Publishes committed Settings values to the Counter widget only.</summary>
public interface ICounterAppearancePublisher
{
    void Publish(int themeIndex, string accentHex);
}
