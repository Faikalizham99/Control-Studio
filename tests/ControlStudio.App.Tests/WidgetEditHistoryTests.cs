using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetEditHistoryTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 100, 200, new());
    private static WidgetEditorState Initial(params WidgetLayer[] layers)
    { var design = WidgetDesignRules.New() with { Layers = layers }; return new(design, layers.LastOrDefault()?.Id, design.Name); }
    private static WidgetEditorState Rename(WidgetEditorState state, string name) => state with { NameText = name };
    [Fact]
    public void UndoRedoRestoresExactLayerDataOrderAndSelection()
    {
        var first = Photo(); var clock = WidgetClocks.New(); var initial = Initial(first, clock); var history = new WidgetEditHistory(initial);
        var changed = initial with { Selection = first.Id, Design = initial.Design with { Layers = [clock, first with { Locked = true, RotationDegrees = 37, Opacity = .4, BlendMode = WidgetBlendMode.SoftLight }] } };
        history.Record(changed);
        var undone = history.Undo()!; Assert.Equal(initial.Design.Layers, undone.Design.Layers); Assert.Equal(clock.Id, undone.Selection);
        var redone = history.Redo()!; Assert.Equal(changed.Design.Layers, redone.Design.Layers); Assert.Equal(first.Id, redone.Selection);
        Assert.Null(history.Redo()); Assert.Equal(1, history.UndoCount);
    }
    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(30)]
    public void HundredsOfInteractionUpdatesProduceOneStep(int layerCount)
    {
        var initial = Initial(Enumerable.Range(0, layerCount).Select(_ => Photo()).ToArray()); var history = new WidgetEditHistory(initial); var source = new object();
        history.Begin(source); history.Begin(source);
        for (var i = 1; i <= 300; i++) history.Record(initial with { Design = initial.Design with { Layers = initial.Design.Layers.Select((x, index) => index == 0 ? x with { Frame = x.Frame with { X = i / 100d } } : x).ToArray() } });
        Assert.False(history.CanUndo); Assert.Equal(0, history.UndoCount); history.End(source);
        Assert.Equal(1, history.UndoCount); Assert.Equal(3, history.Current.Design.Layers[0].Frame.X);
        Assert.Equal(initial.Design.Layers, history.Undo()!.Design.Layers); Assert.Equal(3, history.Redo()!.Design.Layers[0].Frame.X);
    }
    [Fact]
    public void CanceledOrUnchangedInteractionPreservesRedo()
    {
        var initial = Initial(Photo()); var history = new WidgetEditHistory(initial); history.Record(Rename(initial, "Changed")); history.Undo();
        var source = new object(); history.Begin(source); history.Record(Rename(initial, "Preview")); history.Record(initial); history.End(source);
        Assert.True(history.CanRedo); Assert.False(history.CanUndo); Assert.Equal("Changed", history.Redo()!.NameText);
    }
    [Fact]
    public void ANewEditClearsRedoButSelectionAndNoOpsDoNot()
    {
        var initial = Initial(Photo(), Photo()); var history = new WidgetEditHistory(initial); history.Record(Rename(initial, "First")); history.Undo();
        history.Record(initial with { Selection = initial.Design.Layers[0].Id }); Assert.Equal(1, history.RedoCount);
        history.Record(Rename(history.Current, "Different")); Assert.False(history.CanRedo); Assert.Equal(1, history.UndoCount);
    }
    [Fact]
    public void BatteryStopsAreComparedByValueAndSnapshotsProtectMutableInputArrays()
    {
        var stops = WidgetBatteries.Defaults().Stops.ToArray(); var layer = WidgetBatteries.New() with { Battery = WidgetBatteries.Defaults() with { Stops = stops } };
        var layers = new[] { layer }; var initial = Initial(layers); var history = new WidgetEditHistory(initial);
        history.Record(initial with { Design = initial.Design with { Layers = [layer with { Battery = layer.Battery! with { Stops = stops.ToArray() } }] } });
        Assert.Equal(0, history.UndoCount);
        stops[1] = stops[1] with { ColourHex = "#123456" }; layers[0] = layer with { Opacity = 0 };
        Assert.NotEqual("#123456", history.Current.Design.Layers[0].Battery!.Stops[1].ColourHex); Assert.Equal(1, history.Current.Design.Layers[0].Opacity);
    }
    [Fact]
    public void TypingMergesUntilAnotherEditOrExplicitBoundary()
    {
        var initial = Initial(Photo()); var history = new WidgetEditHistory(initial);
        history.Record(Rename(initial, "A"), "name"); history.Record(Rename(initial, "AB"), "name"); history.Record(Rename(initial, "ABC"), "name");
        Assert.Equal(1, history.UndoCount); history.BreakTyping(); history.Record(Rename(initial, "ABCD"), "name");
        Assert.Equal(2, history.UndoCount); Assert.Equal("ABC", history.Undo()!.NameText); Assert.Equal(initial.NameText, history.Undo()!.NameText);
        Assert.Equal("ABC", history.Redo()!.NameText);
        history.Record(history.Current with { Design = history.Current.Design with { BackgroundHex = "#112233" } });
        history.Record(Rename(history.Current, "New"), "name"); Assert.Equal(3, history.UndoCount); Assert.Equal(0, history.RedoCount);
    }
    [Fact]
    public void HistoryIsBoundedAndOnlyRetainsReachablePhotoReferences()
    {
        var photo = Photo(); var initial = Initial(photo); var history = new WidgetEditHistory(initial);
        history.Record(initial with { Design = initial.Design with { Layers = [] }, Selection = null });
        Assert.Contains(photo.AssetKey, history.PhotoKeys()); Assert.Equal(1, history.UndoCount);
        for (var i = 0; i < WidgetEditHistory.MaximumSteps; i++) history.Record(Rename(history.Current, "Name " + i));
        Assert.Equal(WidgetEditHistory.MaximumSteps, history.UndoCount); Assert.DoesNotContain(photo.AssetKey, history.PhotoKeys());
        for (var i = 0; i < WidgetEditHistory.MaximumSteps; i++) Assert.NotNull(history.Undo());
        Assert.Null(history.Undo()); Assert.Equal(WidgetEditHistory.MaximumSteps, history.RedoCount);
    }
    [Fact]
    public void RedoAndAnActiveTransactionRetainPhotosUntilTheBranchIsDiscarded()
    {
        var initial = Initial(); var photo = Photo(); var added = initial with { Design = initial.Design with { Layers = [photo] }, Selection = photo.Id };
        var history = new WidgetEditHistory(initial); history.Record(added); history.Undo(); Assert.Contains(photo.AssetKey, history.PhotoKeys());
        var first = new object(); var second = new object(); history.Begin(first); history.Begin(second); history.Record(added);
        history.End(first); Assert.True(history.IsInteracting); Assert.Contains(photo.AssetKey, history.PhotoKeys()); history.End(second);
        Assert.Equal(1, history.UndoCount); Assert.False(history.CanRedo);
        history.Undo(); history.Record(Rename(history.Current, "Branch")); Assert.DoesNotContain(photo.AssetKey, history.PhotoKeys());
    }
    [Fact]
    public void AutomaticSizingRebasePreservesDepthAndRedoAndDoesNotCreateAnInteractionEdit()
    {
        var initial = Initial(Photo()); var history = new WidgetEditHistory(initial); history.Record(Rename(initial, "Changed")); history.Undo();
        var measured = initial with { Design = initial.Design with { CanvasDimensions = new(158, 158) } };
        var source = new object(); history.Begin(source); history.Rebase(measured); history.End(source);
        Assert.Equal(0, history.UndoCount); Assert.Equal(1, history.RedoCount); Assert.Equal(new(158, 158), history.Current.Design.CanvasDimensions);
    }
    [Fact]
    public void TypingBackToTheStartingValueLeavesNoEmptyUndoStep()
    {
        var initial = Initial(Photo()); var history = new WidgetEditHistory(initial);
        history.Record(Rename(initial, "Temporary"), "name"); history.Record(initial, "name");
        Assert.False(history.CanUndo); Assert.Equal(0, history.UndoCount);
        history.Record(Rename(initial, "New"), "name"); Assert.Equal(1, history.UndoCount); Assert.Equal(initial.NameText, history.Undo()!.NameText);
    }
    [Fact]
    public void CancelledTypingAtTheLimitDoesNotEvictAValidStepOrItsPhoto()
    {
        var photo = Photo(); var initial = Initial(photo); var history = new WidgetEditHistory(initial);
        history.Record(initial with { Design = initial.Design with { Layers = [] }, Selection = null });
        for (var i = 1; i < WidgetEditHistory.MaximumSteps; i++) history.Record(Rename(history.Current, "Name " + i));
        var before = history.Current; Assert.Equal(WidgetEditHistory.MaximumSteps, history.UndoCount);
        history.Record(Rename(before, "Temporary"), "name"); Assert.Contains(photo.AssetKey, history.PhotoKeys());
        history.Record(before, "name"); Assert.Equal(WidgetEditHistory.MaximumSteps, history.UndoCount); Assert.Contains(photo.AssetKey, history.PhotoKeys());
        for (var i = 0; i < WidgetEditHistory.MaximumSteps; i++) Assert.NotNull(history.Undo());
        Assert.Equal(photo.Id, Assert.Single(history.Current.Design.Layers).Id);
    }
    [Fact]
    public void SelectionChangesEndTypingCoalescingWithoutAddingAnEdit()
    {
        var initial = Initial(Photo(), Photo()); var history = new WidgetEditHistory(initial);
        history.Record(Rename(initial, "First"), "name");
        history.Record(history.Current with { Selection = initial.Design.Layers[0].Id }); Assert.Equal(1, history.UndoCount);
        history.Record(Rename(history.Current, "Second"), "name"); Assert.Equal(2, history.UndoCount);
        Assert.Equal("First", history.Undo()!.NameText);
    }
    [Fact]
    public void ClearDropsHistoryInteractionsAndRetiredAssets()
    {
        var initial = Initial(); var photo = Photo(); var history = new WidgetEditHistory(initial);
        history.Record(initial with { Design = initial.Design with { Layers = [photo] } }); history.Begin(new object()); history.Clear(initial);
        Assert.False(history.IsInteracting); Assert.False(history.CanUndo); Assert.False(history.CanRedo); Assert.Empty(history.PhotoKeys());
    }
}
