using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetBlendPanelTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static IEnumerable<Element> Descendants(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in Descendants(child)) yield return value;
    }
    private static void Click(Element root, string id) => ((IButtonController)Descendants(root).OfType<Button>().Single(x => x.AutomationId == id)).SendClicked();
    private static WidgetLayer Photo(string name, WidgetBlendMode mode) => new(Guid.NewGuid(), name, Guid.NewGuid().ToString("N"), 500, 400, new(), BlendMode: mode);
    [Fact]
    public void BrowsingDoesNotEditAndEveryModeCanBeAuditionedWithoutClosing()
    {
        var current = Photo("Selected photo", WidgetBlendMode.Normal); var count = 0; WidgetBlendPanel? panel = null;
        panel = new WidgetBlendPanel(mode => { count++; current = current with { BlendMode = mode }; panel!.Update(current); });
        host.Attach(panel); panel.Show(current);
        for (var group = 0; group < WidgetBlending.Groups.Count; group++)
        {
            var before = count; Click(panel, "WidgetBlendGroup" + group); Assert.Equal(before, count);
            foreach (var mode in WidgetBlending.Modes.Where(x => WidgetBlending.Group(x) == group))
            {
                Click(panel, "WidgetBlendMode" + mode); Assert.Equal(mode, current.BlendMode); Assert.True(panel.IsVisible);
                var tap = Descendants(panel).OfType<Button>().Single(x => x.AutomationId == "WidgetBlendMode" + mode);
                Assert.Contains("selected", SemanticProperties.GetDescription(tap));
                Assert.True(Assert.Single(Descendants(tap.Parent).OfType<AccentSurface>()).IsVisible);
            }
        }
        Assert.Equal(WidgetBlending.Modes.Count - 1, count);
        Click(panel, "CloseWidgetBlendPanel"); Assert.False(panel.IsVisible);
    }
    [Fact]
    public void SwitchingLayersShowsTheCorrectModeGroupAndEmptySelectionClosesThePanel()
    {
        var panel = new WidgetBlendPanel(_ => { }); host.Attach(panel);
        panel.Show(Photo("First", WidgetBlendMode.SoftLight));
        Assert.Contains(Descendants(panel).OfType<Button>(), x => x.AutomationId == "WidgetBlendModeSoftLight");
        panel.Show(Photo("Second", WidgetBlendMode.DarkerColor));
        Assert.DoesNotContain(Descendants(panel).OfType<Button>(), x => x.AutomationId == "WidgetBlendModeSoftLight");
        var tap = Descendants(panel).OfType<Button>().Single(x => x.AutomationId == "WidgetBlendModeDarkerColor");
        Assert.Contains("Second", SemanticProperties.GetDescription(tap)); Assert.Contains("selected", SemanticProperties.GetDescription(tap));
        panel.Update(null); Assert.False(panel.IsVisible);
    }
    [Fact]
    public void CanvasForwardsTheSameDraftAndReleasesNativeCompositionOnSuspendAndDeparture()
    {
        var layer = Photo("Photo", WidgetBlendMode.ColorDodge);
        var draft = WidgetDesignRules.New() with { Layers = [layer] };
        var canvas = new WidgetCanvasView(_ => "/private/photo.png", _ => { }); host.Attach(canvas); canvas.Update(draft, layer.Id);
        var surface = Descendants(canvas).OfType<WidgetCompositeView>().Single(); Assert.Same(draft, surface.Design);
        string? error = null; canvas.RenderFailed += (_, message) => error = message;
        surface.Failed("Preview unavailable"); Assert.Equal("Preview unavailable", error);
        canvas.SuspendComposition(); Assert.Null(surface.Design);
        canvas.Update(draft, layer.Id); Assert.Same(draft, surface.Design);
        canvas.ReleaseSources(); Assert.Null(surface.Design);
    }
    public void Dispose() => host.Dispose();
}
