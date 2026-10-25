using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetLayerRowTests : IDisposable
{
    private readonly MauiTestContext host = new();
    [Fact]
    public void SelectionKeepsBorderGeometryAndOnlyUserActionsCaptureAnAnchor()
    {
        var layer = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new());
        var starting = 0; var actions = 0;
        var row = new WidgetLayerRow(layer, null, "editor", _ => actions++, (_, _) => actions++, _ => actions++, (_, _) => { }, _ => actions++); host.Attach(row);
        row.ActionStarting += (_, _) => { starting++; Assert.Equal(actions + 1, starting); };
        var border = Assert.IsType<Border>(row.Content); var stroke = border.StrokeThickness;
        row.Update(layer with { Locked = true, Visible = false }, true); row.Update(layer, false);
        Assert.Equal(stroke, border.StrokeThickness); Assert.Equal(0, starting);
        var grid = Assert.IsType<Grid>(border.Content); var header = Assert.IsType<CardActionHeader>(grid.Children[1]);
        var buttons = Assert.IsType<ActionButtonRow>(header.Children[1]);
        Assert.IsType<Switch>(buttons.Children[0]).IsToggled = false;
        foreach (var control in buttons.Children.OfType<ClipboardIconButton>())
            ((IButtonController)Assert.IsType<Grid>(control.Content).Children.OfType<Button>().Single()).SendClicked();
        Assert.Equal(3, starting); Assert.Equal(3, actions);
        row.SetReordering(true);
        foreach (var control in buttons.Children.OfType<ClipboardIconButton>())
            ((IButtonController)Assert.IsType<Grid>(control.Content).Children.OfType<Button>().Single()).SendClicked();
        Assert.Equal(3, starting);
    }
    [Fact]
    public void RefreshingLayerPreservesPhotoAndSwitchWithoutSendingVisibilityChanges()
    {
        var layer = new WidgetLayer(Guid.NewGuid(), "First", Guid.NewGuid().ToString("N"), 640, 480, new()); var changes = 0; Guid? selected = null;
        var row = new WidgetLayerRow(layer, "/private/photo.png", "local-editor", id => selected = id, (_, _) => changes++, _ => { }, (_, _) => { }); host.Attach(row);
        var grid = Assert.IsType<Grid>(Assert.IsType<Border>(row.Content).Content);
        var header = Assert.IsType<CardActionHeader>(grid.Children[1]);
        var actions = Assert.IsType<ActionButtonRow>(header.Children[1]); var visible = Assert.IsType<Switch>(actions.Children[0]);
        var selectGrid = Assert.IsType<Grid>(header.Children[0]); var image = Assert.IsType<Image>(Assert.IsType<Border>(selectGrid.Children[0]).Content);
        var source = image.Source;
        row.Update(layer with { Name = "Renamed", Visible = false }, true);
        Assert.Equal(0, changes); Assert.False(visible.IsToggled); Assert.Same(source, image.Source);
        visible.IsToggled = true; Assert.Equal(1, changes);
        var tap = Assert.IsType<Button>(Assert.IsType<Grid>(selectGrid.Children[1]).Children[0]);
        Assert.Contains("Renamed", SemanticProperties.GetDescription(tap)); ((IButtonController)tap).SendClicked(); Assert.Equal(layer.Id, selected);
        // The editor refreshes the model after accepting the preceding visibility change.
        row.Update(layer with { Name = "Renamed", Visible = true }, true);
        selected = null; row.SetReordering(true); visible.IsToggled = false; ((IButtonController)tap).SendClicked();
        Assert.Equal(1, changes); Assert.Null(selected); Assert.False(visible.IsEnabled); Assert.False(tap.IsEnabled);
        row.SetReordering(false); Assert.True(visible.IsToggled); Assert.True(visible.IsEnabled); Assert.True(tap.IsEnabled);
        Assert.Equal(1, changes); ((IButtonController)tap).SendClicked(); Assert.Equal(layer.Id, selected);
        row.ReleaseSource(); Assert.Null(image.Source);
    }
    [Fact]
    public void LockButtonFollowsModelAndRenameWithoutReloadingThePhotoOrFiringChanges()
    {
        var layer = new WidgetLayer(Guid.NewGuid(), "First", Guid.NewGuid().ToString("N"), 640, 480, new()); var toggles = 0;
        var row = new WidgetLayerRow(layer, "/private/photo.png", "editor", _ => { }, (_, _) => { }, _ => { }, (_, _) => { }, _ => toggles++); host.Attach(row);
        var grid = Assert.IsType<Grid>(Assert.IsType<Border>(row.Content).Content); var header = Assert.IsType<CardActionHeader>(grid.Children[1]);
        var actions = Assert.IsType<ActionButtonRow>(header.Children[1]); var control = Assert.IsType<ClipboardIconButton>(actions.Children[1]);
        var button = Assert.IsType<Button>(Assert.IsType<Grid>(control.Content).Children[0]);
        Assert.Equal(48, control.WidthRequest); Assert.Equal(StudioSymbol.Unlock, control.Symbol);
        ((IButtonController)button).SendClicked(); Assert.Equal(1, toggles);
        row.Update(layer with { Locked = true, Name = "Renamed" }, true); Assert.Equal(1, toggles);
        Assert.Equal(StudioSymbol.Lock, control.Symbol); Assert.Contains("Renamed, locked", SemanticProperties.GetDescription(button));
        var buttonSurface = Assert.IsType<Border>(Assert.IsType<Grid>(control.Content).Children[1]);
        ((IButtonController)button).SendPressed(); Assert.Equal(.95, buttonSurface.Scale);
        row.SetReordering(true); ((IButtonController)button).SendClicked(); Assert.Equal(1, toggles); Assert.False(button.IsEnabled);
        Assert.Equal(1, buttonSurface.Scale); ((IButtonController)button).SendPressed(); Assert.Equal(1, buttonSurface.Scale);
        row.SetReordering(false); ((IButtonController)button).SendClicked(); Assert.Equal(2, toggles);
    }
    public void Dispose() => host.Dispose();
}
