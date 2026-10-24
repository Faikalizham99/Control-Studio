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
    [Fact]
    public void AllGroupsHaveIconsAndModePicturesLoadOnlyWhileTheirGroupIsOpen()
    {
        var requests = new List<WidgetBlendMode>();
        var panel = new WidgetBlendPanel(_ => { }, mode => { requests.Add(mode); return ImageSource.FromFile("sample-" + mode + ".png"); }); host.Attach(panel);
        Assert.Empty(requests); panel.Show(Photo("Photo", WidgetBlendMode.Normal));
        var icons = Descendants(panel).OfType<WidgetBlendGroupGlyph>().ToArray();
        Assert.Equal(Enumerable.Range(0, 5), icons.Select(x => x.Group));
        var pictures = new Dictionary<WidgetBlendMode, WidgetBlendThumbnail>();
        for (var group = 0; group < WidgetBlending.Groups.Count; group++)
        {
            Click(panel, "WidgetBlendGroup" + group);
            var current = Descendants(panel).OfType<WidgetBlendThumbnail>().ToArray();
            Assert.Equal(WidgetBlending.Modes.Count(x => WidgetBlending.Group(x) == group), current.Length);
            foreach (var picture in current) { Assert.True(picture.IsPreviewActive); Assert.NotNull(Descendants(picture).OfType<Image>().Single().Source); pictures[picture.Mode] = picture; }
            foreach (var picture in pictures.Values.Where(x => WidgetBlending.Group(x.Mode) != group))
            { Assert.False(picture.IsPreviewActive); Assert.Null(Descendants(picture).OfType<Image>().Single().Source); }
        }
        Assert.Equal(14, pictures.Count); Assert.Equal(14, requests.Count);
        Click(panel, "CloseWidgetBlendPanel"); Assert.All(pictures.Values, picture => Assert.False(picture.IsPreviewActive));
        panel.Show(Photo("Photo 2", WidgetBlendMode.Screen));
        Assert.Equal(4, pictures.Values.Count(x => x.IsPreviewActive)); panel.Update(null);
        Assert.All(pictures.Values, picture => Assert.False(picture.IsPreviewActive));
    }
    [Fact]
    public void ModeSelectionKeepsItsPicturesAndPanelWhileUpdatingNamesAndAccent()
    {
        var value = Photo("First photo", WidgetBlendMode.Multiply); WidgetBlendPanel? panel = null; var requests = 0;
        panel = new WidgetBlendPanel(mode => { value = value with { BlendMode = mode }; panel!.Update(value); }, _ => { requests++; return ImageSource.FromFile("sample.png"); });
        host.Attach(panel); panel.Show(value); var picture = Descendants(panel).OfType<WidgetBlendThumbnail>().First();
        var source = Descendants(picture).OfType<Image>().Single().Source; var count = requests;
        Click(panel, "WidgetBlendModeDarkerColor"); Assert.Equal(WidgetBlendMode.DarkerColor, value.BlendMode); Assert.True(panel.IsVisible);
        Assert.Equal(count, requests); Assert.Same(source, Descendants(picture).OfType<Image>().Single().Source);
        value = value with { Name = "Renamed", Visible = false }; panel.Update(value);
        var tap = Descendants(panel).OfType<Button>().Single(x => x.AutomationId == "WidgetBlendModeDarkerColor");
        Assert.Contains("Renamed", SemanticProperties.GetDescription(tap)); Assert.Contains("selected", SemanticProperties.GetDescription(tap));
        Assert.Contains(Descendants(panel).OfType<Label>(), x => x.Text?.Contains("This layer is hidden") == true);
    }
    [Fact]
    public void GroupIconsAndNamesTrackSelectionAndAccentWithoutChoosingAMode()
    {
        host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.White); var edits = 0;
        var groups = new WidgetBlendGroups(_ => edits++); host.Attach(groups);
        groups.Update(2); Assert.Equal(0, edits);
        var target = Descendants(groups).OfType<Button>().Single(x => x.AutomationId == "WidgetBlendGroup2");
        var surface = Descendants(target.Parent).OfType<AccentSurface>().Single();
        var icon = Descendants(target.Parent).OfType<WidgetBlendGroupGlyph>().Single();
        Assert.True(surface.IsVisible); Assert.Equal(surface.Foreground, icon.Tint); Assert.Contains("selected", SemanticProperties.GetDescription(target));
        host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.Black); Assert.Equal(surface.Foreground, icon.Tint);
        Click(groups, "WidgetBlendGroup4"); Assert.Equal(1, edits); Assert.Equal(4, groups.SelectedIndex); Assert.False(surface.IsVisible);
        Assert.Throws<ArgumentOutOfRangeException>(() => groups.Update(5));
    }
    public void Dispose() => host.Dispose();
}
