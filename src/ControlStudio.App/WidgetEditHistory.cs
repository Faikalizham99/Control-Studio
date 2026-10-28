using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

internal sealed record WidgetEditorState(WidgetDesign Design, Guid? Selection, string NameText);

/// <summary>Bounded, session-only draft snapshots. Images remain immutable file references.</summary>
internal sealed class WidgetEditHistory
{
    public const int MaximumSteps = 100;
    private readonly List<WidgetEditorState> undo = [], redo = [];
    private readonly HashSet<object> interactions = new(ReferenceEqualityComparer.Instance);
    private WidgetEditorState? origin;
    private WidgetEditorState? typingOverflow;
    private string? mergeKey;
    public WidgetEditorState Current { get; private set; }
    public bool IsInteracting => interactions.Count != 0;
    public int UndoCount => undo.Count;
    public int RedoCount => redo.Count;
    public bool CanUndo => !IsInteracting && undo.Count > 0;
    public bool CanRedo => !IsInteracting && redo.Count > 0;

    public WidgetEditHistory(WidgetEditorState initial) => Current = Freeze(initial);
    public void Begin(object source)
    {
        if (interactions.Contains(source)) return;
        if (!IsInteracting) { origin = Current; BreakTyping(); }
        interactions.Add(source);
    }
    public bool End(object source)
    {
        if (!interactions.Remove(source)) return false;
        if (IsInteracting) return true;
        if (origin is { } before && !SameEdit(before, Current)) { Push(undo, before); redo.Clear(); }
        origin = null; BreakTyping(); return true;
    }
    public void BreakTyping() { mergeKey = null; typingOverflow = null; }
    public void Record(WidgetEditorState next, string? typingKey = null)
    {
        if (SameEdit(Current, next))
        {
            if (Current.Selection != next.Selection) BreakTyping();
            Current = Current with { Selection = next.Selection }; return;
        }
        var merging = typingKey is not null && mergeKey == typingKey && undo.Count > 0;
        var before = Current; Current = Freeze(next);
        if (IsInteracting) return;
        if (!merging)
        {
            BreakTyping(); var overflow = Push(undo, before);
            if (typingKey is not null) typingOverflow = overflow;
        }
        redo.Clear(); mergeKey = typingKey;
        if (merging && SameEdit(undo[^1], Current))
        {
            undo.RemoveAt(undo.Count - 1);
            if (typingOverflow is not null) undo.Insert(0, typingOverflow);
            BreakTyping();
        }
    }
    // A refreshed device measurement changes no user edit and must not clear Redo.
    public void Rebase(WidgetEditorState state)
    {
        Current = Freeze(state);
        if (origin is { } before && before.Design.Size == state.Design.Size)
            origin = before with { Design = before.Design with { CanvasDimensions = state.Design.CanvasDimensions } };
    }
    public WidgetEditorState? Undo()
    {
        if (!CanUndo) return null;
        Push(redo, Current); Current = Pop(undo); BreakTyping(); return Current;
    }
    public WidgetEditorState? Redo()
    {
        if (!CanRedo) return null;
        Push(undo, Current); Current = Pop(redo); BreakTyping(); return Current;
    }
    public IReadOnlySet<string> PhotoKeys()
    {
        var states = undo.Concat(redo).Append(Current);
        if (origin is not null) states = states.Append(origin);
        if (typingOverflow is not null) states = states.Append(typingOverflow);
        return states.SelectMany(x => x.Design.Layers).Where(x => x.Kind == WidgetLayerKind.Photo).Select(x => x.AssetKey).ToHashSet();
    }
    public void Clear(WidgetEditorState state)
    {
        undo.Clear(); redo.Clear(); interactions.Clear(); origin = null; BreakTyping(); Current = Freeze(state);
    }
    private static WidgetEditorState? Push(List<WidgetEditorState> stack, WidgetEditorState state)
    {
        stack.Add(state); if (stack.Count <= MaximumSteps) return null;
        var overflow = stack[0]; stack.RemoveAt(0); return overflow;
    }
    private static WidgetEditorState Pop(List<WidgetEditorState> stack)
    { var result = stack[^1]; stack.RemoveAt(stack.Count - 1); return result; }
    private static WidgetEditorState Freeze(WidgetEditorState state) => state with
    {
        Design = state.Design with { Layers = Array.AsReadOnly(state.Design.Layers.Select(x => x.Battery is { } battery
            ? x with { Battery = battery with { Stops = Array.AsReadOnly(battery.Stops.ToArray()) } } : x).ToArray()) }
    };
    private static bool SameEdit(WidgetEditorState a, WidgetEditorState b)
    {
        if (a.NameText != b.NameText || a.Design != (b.Design with { Layers = a.Design.Layers }) || a.Design.Layers.Count != b.Design.Layers.Count) return false;
        return a.Design.Layers.Zip(b.Design.Layers).All(pair => SameLayer(pair.First, pair.Second));
    }
    private static bool SameLayer(WidgetLayer a, WidgetLayer b)
    {
        if (a.Battery is not { } first || b.Battery is not { } second) return a == b;
        return a == (b with { Battery = first }) && first == (second with { Stops = first.Stops }) && first.Stops.SequenceEqual(second.Stops);
    }
}
