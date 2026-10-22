using ControlStudio.Core;
using ControlStudio.Core.Actions;

namespace ControlStudio.App;

internal enum ClipboardKind { Text, Photo, Link }

internal static class StudioContent
{
    public static IReadOnlyList<ActionDefinition> ClipboardItems(IEnumerable<ActionDefinition> actions, ClipboardKind kind = ClipboardKind.Text) =>
        actions.Where(action => Kind(action.Parameters) == kind).OrderByDescending(action => action.IsFavorite).ThenBy(action => action.SortOrder).ToArray();

    public static ClipboardKind? Kind(ActionParameters parameters) => parameters switch
    {
        CopyTextParameters => ClipboardKind.Text, PhotoParameters => ClipboardKind.Photo,
        OpenUrlParameters => ClipboardKind.Link, _ => null
    };

    public static ActionDefinition Draft(ActionDefinition? original, ClipboardKind kind, string? name, string? content)
    {
        if (original is not null && Kind(original.Parameters) != kind)
            throw new ArgumentException("An item's type cannot change while editing.", nameof(original));
        ActionParameters parameters = kind switch
        {
            ClipboardKind.Text => new CopyTextParameters(content ?? ""),
            ClipboardKind.Link => new OpenUrlParameters((content ?? "").Trim()),
            ClipboardKind.Photo => new PhotoParameters(content ?? ""),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return original is null
            ? new ActionDefinition(Guid.NewGuid(), (name ?? "").Trim(), parameters, Icon: kind == ClipboardKind.Link ? ActionIcon.Link : ActionIcon.Copy)
            : original with { Name = (name ?? "").Trim(), Parameters = parameters };
    }

    public static ActionDefinition ClipboardDraft(ActionDefinition? original, string? name, string? text)
    {
        return Draft(original, ClipboardKind.Text, name, text);
    }

    public static CounterSnapshot? WidgetSnapshot(CounterSnapshot snapshot)
    {
        snapshot.Validate();
        // The widget reads shared storage; a private fallback is not its value.
        return snapshot.IsShared ? snapshot : null;
    }
}
