using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetLayerRowTests : IDisposable
{
    private readonly MauiTestContext host = new();
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
        row.ReleaseSource(); Assert.Null(image.Source);
    }
    public void Dispose() => host.Dispose();
}
