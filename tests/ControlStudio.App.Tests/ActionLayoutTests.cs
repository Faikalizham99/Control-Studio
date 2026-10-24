using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ActionLayoutTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private sealed class Probe(double width = 48, double height = 48) : View
    {
        public double NaturalWidth { get; set; } = width;
        public Rect Arranged { get; private set; }
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(Math.Min(widthConstraint, NaturalWidth), Math.Min(heightConstraint, height));
        protected override Size ArrangeOverride(Rect bounds) { Arranged = bounds; return bounds.Size; }
    }
    private static Size Place(Layout layout, double width, double x = 0, double y = 0)
    {
        var size = ((Microsoft.Maui.ILayout)layout).CrossPlatformMeasure(width, double.PositiveInfinity);
        ((Microsoft.Maui.ILayout)layout).CrossPlatformArrange(new Rect(x, y, width, size.Height)); return size;
    }
    [Theory]
    [InlineData(300, 48)] [InlineData(272, 48)] [InlineData(264, 104)] [InlineData(216, 104)] [InlineData(48, 272)]
    public void ActionsRetainTheirTapSizesAndWrapInsideAvailableSpace(double width, double height)
    {
        var buttons = Enumerable.Range(0, 5).Select(_ => new Probe()).ToArray();
        var row = new ActionButtonRow(buttons); host.Attach(row);
        Assert.Equal(height, Place(row, width).Height);
        foreach (var button in buttons)
        {
            Assert.Equal(48, button.Arranged.Width); Assert.Equal(48, button.Arranged.Height);
            Assert.InRange(button.Arranged.Left, 0, width - 48); Assert.InRange(button.Arranged.Right, 48, width);
        }
        foreach (var line in buttons.GroupBy(x => x.Arranged.Y))
        {
            var ordered = line.OrderBy(x => x.Arranged.X).ToArray();
            for (var i = 1; i < ordered.Length; i++) Assert.Equal(8, ordered[i].Arranged.Left - ordered[i - 1].Arranged.Right, 8);
            Assert.Equal(width, ordered[^1].Arranged.Right, 8);
        }
    }
    [Fact]
    public void HiddenActionsReflowWithoutGapsAndReappearingActionsKeepTheirOrder()
    {
        var buttons = Enumerable.Range(0, 5).Select(_ => new Probe()).ToArray(); var row = new ActionButtonRow(buttons); host.Attach(row);
        Assert.Equal(104, Place(row, 264).Height); buttons[1].IsVisible = false;
        Assert.Equal(48, Place(row, 264).Height); Assert.Equal(8, buttons[2].Arranged.Left - buttons[0].Arranged.Right, 8);
        buttons[1].IsVisible = true; Assert.Equal(104, Place(row, 264).Height);
        Assert.Equal(8, buttons[1].Arranged.Left - buttons[0].Arranged.Right, 8); Assert.Same(buttons[1], row.Children[1]);
    }
    [Fact]
    public void PaddingOffsetsAndRTLAlignmentAreRespected()
    {
        var first = new Probe(); var last = new Probe();
        var row = new ActionButtonRow(first, last) { Padding = new Thickness(12, 6), FlowDirection = FlowDirection.RightToLeft }; host.Attach(row);
        Assert.Equal(60, Place(row, 200, 11, 17).Height);
        Assert.Equal(23, last.Arranged.Left); Assert.Equal(79, first.Arranged.Left); Assert.Equal(23, first.Arranged.Top);
        row.Alignment = TextAlignment.Center; Place(row, 200, 11, 17);
        Assert.Equal(59, last.Arranged.Left); Assert.Equal(115, first.Arranged.Left);
    }
    [Theory]
    [InlineData(100, false)] [InlineData(190, true)] [InlineData(400, true)]
    public void CardHeadersStackBeforeTheTitleOrActionsAreSqueezed(double titleWidth, bool stacked)
    {
        var title = new Probe(titleWidth, 72); var action = new Probe(156, 48);
        var header = new CardActionHeader(title, action); host.Attach(header); var size = Place(header, 317);
        Assert.Equal(stacked ? 128 : 72, size.Height);
        Assert.Equal(stacked ? 317 : 156, action.Arranged.Width);
        if (stacked) { Assert.Equal(80, action.Arranged.Top); Assert.Equal(317, title.Arranged.Width); }
        else { Assert.Equal(8, action.Arranged.Left - title.Arranged.Right); Assert.Equal(12, action.Arranged.Top); }
        title.NaturalWidth = 80; Assert.Equal(72, Place(header, 317).Height); Assert.Equal(8, action.Arranged.Left - title.Arranged.Right);
    }
    [Fact]
    public void RTLHeadersPutActionsOnTheLeadingPhysicalSideWithoutChangingChildOrder()
    {
        var title = new Probe(100, 48); var actions = new Probe(156, 48);
        var header = new CardActionHeader(title, actions) { FlowDirection = FlowDirection.RightToLeft }; host.Attach(header); Place(header, 317);
        Assert.Equal(0, actions.Arranged.X); Assert.Equal(164, title.Arranged.X); Assert.Same(title, header.Children[0]);
    }
    [Fact]
    public void IntrinsicSwitchesRemainCentredAndPaletteItemsCanAlignTheirTops()
    {
        var toggle = new Probe(51, 31) { VerticalOptions = LayoutOptions.Center }; var menu = new Probe();
        var row = new ActionButtonRow(toggle, menu); host.Attach(row); Assert.Equal(48, Place(row, 200).Height);
        Assert.Equal(8.5, toggle.Arranged.Y); Assert.Equal(8, menu.Arranged.Left - toggle.Arranged.Right);
        var swatch = new Probe(44, 44) { VerticalOptions = LayoutOptions.Start };
        var custom = new Probe(44, 68) { VerticalOptions = LayoutOptions.Start };
        var palette = new ActionButtonRow(swatch, custom) { Alignment = TextAlignment.Center }; host.Attach(palette); Place(palette, 200);
        Assert.Equal(0, swatch.Arranged.Y); Assert.Equal(0, custom.Arranged.Y); Assert.Equal(8, custom.Arranged.Left - swatch.Arranged.Right);
    }
    [Fact]
    public void ColourPickerPreservesSwatchTargetsAndSelectionAfterWrapping()
    {
        var picker = new ColourPickerView(new NoPicker()); host.Attach(picker);
        var palette = Assert.IsType<ActionButtonRow>(Assert.IsType<VerticalStackLayout>(picker.Content).Children[0]);
        Assert.Equal(6, palette.Children.Count);
        foreach (var choice in palette.Children.Cast<ColourChoice>()) Assert.Equal(44, choice.WidthRequest);
        var nativeTargets = Descendants(palette).OfType<Button>().ToArray(); Assert.Equal(6, nativeTargets.Length);
        ((IButtonController)nativeTargets[1]).SendClicked(); Assert.Equal(ControlStudio.Core.AppearanceValues.Presets[1], picker.SelectedHex);
        palette.IsEnabled = false; Assert.All(nativeTargets, button => Assert.False(button.IsEnabled));
        palette.IsEnabled = true; Assert.All(nativeTargets, button => Assert.True(button.IsEnabled));
    }
    private sealed class NoPicker : IColourPicker
    {
        public Task<string> PickAsync(string initialHex, Action<string> preview) => Task.FromResult(initialHex);
    }
    private static IEnumerable<Element> Descendants(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in Descendants(child)) yield return value;
    }
    public void Dispose() => host.Dispose();
}
