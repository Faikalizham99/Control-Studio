namespace ControlStudio.App.Rendering;

/// <summary>Coalesces main-thread requests into one next-turn render of the latest draft.</summary>
internal sealed class WidgetRenderQueue(Action<Action> post, Action render)
{
    private bool pending;
    private int generation;
    public void Request()
    {
        if (pending) return;
        pending = true; var current = generation;
        try
        {
            post(() =>
            {
                if (current != generation) return;
                pending = false; render();
            });
        }
        catch { pending = false; throw; }
    }
    public void Cancel() { generation++; pending = false; }
}
