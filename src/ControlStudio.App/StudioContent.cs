using ControlStudio.Core;
using ControlStudio.Core.Actions;

namespace ControlStudio.App;

internal static class StudioContent
{
    public static IReadOnlyList<ActionDefinition> ClipboardItems(IEnumerable<ActionDefinition> actions) =>
        actions.Where(action => action.Parameters is CopyTextParameters).OrderBy(action => action.SortOrder).ToArray();

    public static ActionDefinition ClipboardDraft(ActionDefinition? original, string? name, string? text)
    {
        if (original is not null && original.Parameters is not CopyTextParameters)
            throw new ArgumentException("Only clipboard items can be edited here.", nameof(original));
        var parameters = new CopyTextParameters(text ?? "");
        return original is null
            ? new ActionDefinition(Guid.NewGuid(), (name ?? "").Trim(), parameters, Icon: ActionIcon.Copy)
            : original with { Name = (name ?? "").Trim(), Parameters = parameters };
    }

    public static CounterSnapshot? WidgetSnapshot(CounterSnapshot snapshot)
    {
        snapshot.Validate();
        // The widget reads shared storage; a private fallback is not its value.
        return snapshot.IsShared ? snapshot : null;
    }
}
