using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

/// <summary>One bounded cold-launch address, retained only until its own scene has a window.</summary>
internal sealed class WidgetTapLaunchInbox
{
    private (string Scene, string Url)? pending;
    public bool HasPending => pending is not null;

    public bool Capture(string scene, IEnumerable<string?>? urls)
    {
        if (string.IsNullOrEmpty(scene) || urls is null) return false;
        string? last = null;
        foreach (var url in urls) if (WidgetTapLink.TryParse(url, out _)) last = url;
        // No partial update if reading native launch data throws during enumeration.
        if (last is null) return false;
        pending = (scene, last); return true;
    }
    public bool Deliver(string scene, Func<string, bool> send)
    {
        if (pending is not { } current || current.Scene != scene) return false;
        if (!send(current.Url)) return false;
        if (pending == current) pending = null;
        return true;
    }
    public void Disconnect(string scene) { if (pending?.Scene == scene) pending = null; }
}
