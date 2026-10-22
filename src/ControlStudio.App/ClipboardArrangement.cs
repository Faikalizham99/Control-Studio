using System.Collections.ObjectModel;
using ControlStudio.Core.Actions;

namespace ControlStudio.App;

internal sealed record ClipboardArrangeAvailability(bool CanArrange, string Reason)
{
    public static ClipboardArrangeAvailability For(IEnumerable<ActionDefinition> items, ClipboardKind kind)
    {
        var selected = StudioContent.ClipboardItems(items, kind);
        var singular = kind switch { ClipboardKind.Photo => "photo", ClipboardKind.Link => "link", _ => "text item" };
        if (selected.Count < 2) return new(false, selected.Count == 1 ? $"Add another {singular} to arrange." : $"Add at least two {singular}s to arrange.");
        var pinned = selected.Count(item => item.IsFavorite);
        return pinned > 1 || selected.Count - pinned > 1
            ? new(true, "")
            : new(false, "Pin or unpin an item to arrange within a group.");
    }
}

/// <summary>A private arrangement draft: moving a title never mutates saved content.</summary>
internal sealed class ClipboardArrangement
{
    private readonly List<ActionDefinition>[] groups;
    private readonly Guid[] original;
    public event EventHandler? Changed;
    public IReadOnlyList<ActionDefinition> Pinned { get; }
    public IReadOnlyList<ActionDefinition> Others { get; }
    public Guid[] OrderedIds => groups.SelectMany(group => group).Select(item => item.Id).ToArray();
    public bool HasChanges => !original.SequenceEqual(OrderedIds);

    public ClipboardArrangement(IEnumerable<ActionDefinition> items)
    {
        var saved = items.OrderByDescending(item => item.IsFavorite).ThenBy(item => item.SortOrder).ToArray();
        if (saved.Select(item => item.Id).Distinct().Count() != saved.Length ||
            saved.Any(item => StudioContent.Kind(item.Parameters) is null) ||
            saved.Select(item => item.Parameters.GetType()).Distinct().Count() > 1)
            throw new ArgumentException("Arrange requires unique items from one Clipboard tab.", nameof(items));
        groups = [saved.Where(item => item.IsFavorite).ToList(), saved.Where(item => !item.IsFavorite).ToList()];
        Pinned = groups[0].AsReadOnly(); Others = groups[1].AsReadOnly(); original = OrderedIds;
    }

    public IReadOnlyList<ActionDefinition> Group(int section) => section switch { 0 => Pinned, 1 => Others, _ => throw new ArgumentOutOfRangeException(nameof(section)) };
    public bool Move(int section, int from, int to)
    {
        if (section is < 0 or > 1) return false;
        var group = groups[section];
        if (from < 0 || to < 0 || from >= group.Count || to >= group.Count || from == to) return false;
        var item = group[from]; group.RemoveAt(from); group.Insert(to, item); Changed?.Invoke(this, EventArgs.Empty); return true;
    }
}

internal static class ClipboardCollection
{
    /// <summary>Retain the ItemsSource and emit moves, rather than resetting the whole library.</summary>
    public static void Update(ObservableCollection<ActionDefinition> visible, IReadOnlyList<ActionDefinition> desired)
    {
        var retained = desired.Select(item => item.Id).ToHashSet();
        for (var i = visible.Count - 1; i >= 0; i--) if (!retained.Contains(visible[i].Id)) visible.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            var index = -1;
            for (var j = i; j < visible.Count; j++) if (visible[j].Id == desired[i].Id) { index = j; break; }
            if (index < 0) visible.Insert(i, desired[i]);
            else
            {
                if (index != i) visible.Move(index, i);
                if (visible[i] != desired[i]) visible[i] = desired[i];
            }
        }
    }
}
