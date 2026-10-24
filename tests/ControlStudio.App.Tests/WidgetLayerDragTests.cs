using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetLayerDragTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static WidgetLayerRow Row(string name) => new(new WidgetLayer(Guid.NewGuid(), name, Guid.NewGuid().ToString("N"), 640, 480, new()),
        "/private/photo.png", "editor", _ => { }, (_, _) => { }, _ => { }, (_, _) => { });
    private WidgetLayerList List()
    {
        var list = new WidgetLayerList(); list.Items.Add(Row("First")); list.Items.Add(Row("Second")); list.Items.Add(Row("Third")); host.Attach(list); return list;
    }
    private static WidgetLayerPosition[] Positions(WidgetLayerList list) => list.Rows.Select((row, index) => new WidgetLayerPosition(row.LayerId, index * 72, 64)).ToArray();
    [Fact]
    public void DragOnlyChangesPresentationUntilSuccessfulDropAndFiresOnce()
    {
        var list = List(); var ids = list.Rows.Select(x => x.LayerId).ToArray(); var changes = new List<bool>(); var moves = new List<(Guid, Guid)>();
        list.InteractionChanged += (_, _) => changes.Add(list.IsReordering); list.Reordered += (source, target) => moves.Add((source, target));
        var drag = list.BeginReorder(Positions(list), ids[0]); Assert.NotNull(drag); Assert.True(list.IsReordering);
        Assert.Null(list.BeginReorder(Positions(list), ids[1])); Assert.True(drag.Move(190)); Assert.Equal(2, drag.Target);
        Assert.Equal(ids, list.Rows.Select(x => x.LayerId)); Assert.Empty(moves);
        Assert.Equal(144, drag.TopAt(0)); Assert.Equal(0, drag.TopAt(1)); Assert.Equal(72, drag.TopAt(2));
        list.CompleteReorder(drag); list.CompleteReorder(drag);
        Assert.Equal(new[] { true, false }, changes); Assert.Equal((ids[0], ids[2]), Assert.Single(moves)); Assert.False(list.IsReordering);
    }
    [Fact]
    public void CancellationAndDisablingRejectStaleDropCallbacksAndAllowAnotherDrag()
    {
        var list = List(); var moves = 0; var cancels = 0; list.Reordered += (_, _) => moves++; list.ReorderCancelled += (_, _) => cancels++;
        var positions = Positions(list); var drag = list.BeginReorder(positions, positions[2].Id)!; drag.Move(-300);
        list.CancelReorder(); list.CompleteReorder(drag); Assert.Equal(0, moves); Assert.Equal(1, cancels);
        var next = list.BeginReorder(positions, positions[0].Id)!; next.Move(500); list.CompleteReorder(drag); Assert.Same(next, list.Drag);
        list.IsEnabled = false; list.CompleteReorder(next); Assert.Equal(2, cancels); Assert.False(list.IsReordering); Assert.Equal(0, moves);
        Assert.Null(list.BeginReorder(positions, positions[0].Id)); list.IsEnabled = true;
        var final = list.BeginReorder(positions, positions[1].Id)!; final.Move(500); list.CompleteReorder(final); Assert.Equal(1, moves);
    }
    [Fact]
    public void ChangedRowsOrActivityRejectDropWithoutChangingAnything()
    {
        var list = List(); var positions = Positions(list); var allow = false; var moves = 0;
        list.CanStart = () => allow; list.Reordered += (_, _) => moves++;
        Assert.Null(list.BeginReorder(positions, positions[0].Id)); allow = true;
        var drag = list.BeginReorder(positions, positions[0].Id)!; drag.Move(300); list.Items.RemoveAt(1); list.CompleteReorder(drag);
        Assert.False(list.IsReordering); Assert.Equal(0, moves);
        positions = Positions(list); drag = list.BeginReorder(positions, positions[0].Id)!; drag.Move(300); allow = false; list.CompleteReorder(drag);
        Assert.Equal(0, moves); allow = true;
        positions = Positions(list); (positions[0], positions[1]) = (positions[1], positions[0]); Assert.Null(list.BeginReorder(positions, positions[0].Id));
        list.Items.RemoveAt(1); Assert.Null(list.BeginReorder(Positions(list), list.Rows.Single().LayerId));
    }
    [Fact]
    public void VariableHeightRowsKeepAFullSizeGapWithoutBouncingAtTheThreshold()
    {
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var plan = new WidgetLayerDragPlan([new(ids[0], 0, 64), new(ids[1], 72, 112), new(ids[2], 192, 80), new(ids[3], 280, 64)], 1);
        Assert.False(plan.Move(128)); Assert.True(plan.Move(233)); Assert.Equal(2, plan.Target); Assert.False(plan.Move(234));
        Assert.Equal(0, plan.TopAt(0)); Assert.Equal(160, plan.TopAt(1)); Assert.Equal(72, plan.TopAt(2)); Assert.Equal(280, plan.TopAt(3));
        Assert.True(plan.Move(-100)); Assert.Equal(0, plan.Target); Assert.Equal(0, plan.TopAt(1)); Assert.Equal(120, plan.TopAt(0));
        Assert.True(plan.Move(1000)); Assert.Equal(3, plan.Target); Assert.Equal(232, plan.TopAt(1));
        Assert.False(plan.Move(double.NaN)); Assert.False(plan.Move(double.PositiveInfinity)); Assert.Equal(3, plan.Target);
        Assert.True(plan.Move(128)); Assert.Equal(1, plan.Target); Assert.Equal(72, plan.TopAt(1));
    }
    [Fact]
    public void FingerTrackingCanReachBothEndsWithATallRowAndAnOffCentreGrip()
    {
        WidgetLayerPosition[] positions = [new(Guid.NewGuid(), 0, 112), new(Guid.NewGuid(), 120, 64), new(Guid.NewGuid(), 192, 80)];
        for (var source = 0; source < positions.Length; source++)
            for (var target = 0; target < positions.Length; target++)
            {
                var drag = new WidgetLayerDragPlan(positions, source); const double grip = 24;
                var centre = positions[target].Centre + Math.Sign(target - source) * 2;
                drag.Track(centre + grip - positions[source].Height / 2, grip);
                Assert.Equal(target, drag.Target); Assert.Equal(centre, drag.PreviewCentre);
                var previous = drag.PreviewCentre; Assert.False(drag.Track(double.NaN, grip)); Assert.Equal(previous, drag.PreviewCentre);
            }
    }
    [Fact]
    public void InvalidGeometryCannotStartADragAndSingleRowKeepsTheMenuAlternative()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => new WidgetLayerDragPlan([new(id, 0, 64), new(id, 72, 64)], 0));
        Assert.Throws<ArgumentException>(() => new WidgetLayerDragPlan([new(id, double.NaN, 64), new(Guid.NewGuid(), 72, 64)], 0));
        Assert.Throws<ArgumentException>(() => new WidgetLayerDragPlan([new(id, 0, 0), new(Guid.NewGuid(), 72, 64)], 0));
        Assert.Throws<ArgumentException>(() => new WidgetLayerDragPlan([new(id, 0, 64), new(Guid.NewGuid(), 32, 64)], 0));
        var row = Row("Example"); Assert.Equal(44, row.DragHandle.WidthRequest); Assert.Equal(48, row.DragHandle.HeightRequest);
        Assert.Contains("Move forward and backward", SemanticProperties.GetHint(row.DragHandle));
        row.Update(new(row.LayerId, "Renamed", Guid.NewGuid().ToString("N"), 640, 480, new()), true);
        Assert.Equal("Reorder Renamed", SemanticProperties.GetDescription(row.DragHandle));
    }
    public void Dispose() => host.Dispose();
}
