namespace ControlStudio.App.Views;

internal readonly record struct WidgetLayerPosition(Guid Id, double Top, double Height)
{
    public double Centre => Top + Height / 2;
}

/// <summary>Frozen layout for one drag. Only the final drop changes the editor draft.</summary>
internal sealed class WidgetLayerDragPlan
{
    public IReadOnlyList<WidgetLayerPosition> Rows { get; }
    public int Source { get; }
    public int Target { get; private set; }
    public Guid SourceId => Rows[Source].Id;
    public Guid TargetId => Rows[Target].Id;
    public double PreviewCentre { get; private set; }
    public WidgetLayerDragPlan(IReadOnlyList<WidgetLayerPosition> rows, int source)
    {
        if (rows.Count < 2 || source < 0 || source >= rows.Count || rows.Select(x => x.Id).Distinct().Count() != rows.Count ||
            rows.Any(x => x.Id == Guid.Empty || !double.IsFinite(x.Top) || !double.IsFinite(x.Height) || x.Height <= 0) ||
            rows.Zip(rows.Skip(1)).Any(pair => pair.Second.Top < pair.First.Top + pair.First.Height))
            throw new ArgumentException("Layer drag requires distinct, laid-out rows.", nameof(rows));
        Rows = rows.ToArray(); Source = Target = source; PreviewCentre = Rows[source].Centre;
    }
    public bool Track(double pointerY, double grabOffset)
    {
        if (!double.IsFinite(pointerY) || !double.IsFinite(grabOffset)) return false;
        var centre = pointerY - grabOffset + Rows[Source].Height / 2;
        if (!double.IsFinite(centre)) return false;
        // Select using the finger's unconstrained position. Keeping the entire lifted
        // row inside the list would make the first/last slots unreachable for tall rows.
        var changed = Move(centre);
        PreviewCentre = Math.Clamp(centre, Rows[0].Top, Rows[^1].Top + Rows[^1].Height);
        return changed;
    }
    public bool Move(double centre)
    {
        if (!double.IsFinite(centre)) return false;
        var next = Source;
        while (next > 0 && centre < Rows[next - 1].Centre) next--;
        while (next < Rows.Count - 1 && centre > Rows[next + 1].Centre) next++;
        if (Target == next) return false;
        Target = next; return true;
    }
    public double TopAt(int index)
    {
        // Keep each row's real height (including wrapped names and accessibility text).
        var order = Enumerable.Range(0, Rows.Count).ToList(); order.Remove(Source); order.Insert(Target, Source);
        var top = Rows[0].Top;
        var gap = Rows.Count > 1 ? Rows[1].Top - Rows[0].Top - Rows[0].Height : 0;
        foreach (var row in order) { if (row == index) return top; top += Rows[row].Height + gap; }
        throw new ArgumentOutOfRangeException(nameof(index));
    }
}

/// <summary>Stable MAUI rows with an iOS lift/slide/drop presentation.</summary>
internal sealed class WidgetLayerList : ContentView
{
    private readonly VerticalStackLayout stack = new() { Spacing = ActionSpacing.Gap };
    public IList<IView> Items => stack.Children;
    public IEnumerable<WidgetLayerRow> Rows => Items.OfType<WidgetLayerRow>();
    public Func<bool>? CanStart { get; set; }
    public WidgetLayerDragPlan? Drag { get; private set; }
    public bool IsReordering => Drag is not null;
    public event EventHandler? InteractionChanged;
    public event EventHandler? ReorderCancelled;
    public event Action<Guid, Guid>? Reordered;
    public WidgetLayerList()
    {
        AutomationId = "WidgetLayerList"; Content = stack;
        PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IsEnabled) && !IsEnabled) CancelReorder(); };
        Unloaded += (_, _) => CancelReorder();
    }
    public WidgetLayerDragPlan? BeginReorder(IReadOnlyList<WidgetLayerPosition> positions, Guid source)
    {
        if (IsReordering || !IsEnabled || CanStart?.Invoke() == false ||
            !Rows.Select(x => x.LayerId).SequenceEqual(positions.Select(x => x.Id))) return null;
        var index = positions.ToList().FindIndex(x => x.Id == source);
        if (index < 0 || positions.Count < 2) return null;
        Drag = new(positions, index);
        foreach (var row in Rows) row.SetReordering(true);
        InteractionChanged?.Invoke(this, EventArgs.Empty); return Drag;
    }
    public void CompleteReorder(WidgetLayerDragPlan expected)
    {
        if (!ReferenceEquals(Drag, expected)) return;
        var valid = IsEnabled && Rows.Select(x => x.LayerId).SequenceEqual(expected.Rows.Select(x => x.Id));
        Drag = null; foreach (var row in Rows) row.SetReordering(false); InteractionChanged?.Invoke(this, EventArgs.Empty);
        if (valid && expected.Source != expected.Target && CanStart?.Invoke() != false)
            Reordered?.Invoke(expected.SourceId, expected.TargetId);
    }
    public void CancelReorder()
    {
        if (!IsReordering) return;
        ReorderCancelled?.Invoke(this, EventArgs.Empty);
        Drag = null; foreach (var row in Rows) row.SetReordering(false); InteractionChanged?.Invoke(this, EventArgs.Empty);
    }
}
