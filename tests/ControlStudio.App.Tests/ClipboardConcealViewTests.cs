using ControlStudio.App.Views;
using ControlStudio.Core.Actions;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ClipboardConcealViewTests : IDisposable
{
    private readonly MauiTestContext host = new();

    [Fact]
    public void RecycledCardsAndTabReloadsKeepVisibilityWithTheItemRatherThanTheTile()
    {
        var hidden = new HashSet<Guid>(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var card = new ClipboardConcealView(new Label { Text = "Private content" }, hidden);
        card.Bind(first); card.Toggle(); Assert.True(card.IsConcealed); Assert.True(card.AnimateTransition);
        card.Bind(second); Assert.False(card.IsConcealed); Assert.False(card.AnimateTransition);
        card.Bind(first); Assert.True(card.IsConcealed); Assert.False(card.AnimateTransition);
        // CollectionView may create a different tile after switching sections or refreshing.
        var reloadedCard = new ClipboardConcealView(new Image(), hidden);
        reloadedCard.Bind(first); Assert.True(reloadedCard.IsConcealed);
        reloadedCard.Toggle(); Assert.False(reloadedCard.IsConcealed); Assert.Empty(hidden);
        card.Bind(first); Assert.False(card.IsConcealed);
        card.Bind(null); card.Toggle(); Assert.False(card.IsConcealed); Assert.Empty(hidden);
    }

    [Fact]
    public void ConcealingTextAndPhotoPreservesTheirContentAndLayoutProperties()
    {
        var text = new Label { Text = "Exact\ncontent 👀", MaxLines = -1, LineBreakMode = LineBreakMode.WordWrap };
        var photo = new Image { Source = "private-photo.png", HeightRequest = 130, WidthRequest = 160, Aspect = Aspect.AspectFit };
        var photoSource = photo.Source;
        var payload = new VerticalStackLayout { Children = { text, photo } };
        var card = new ClipboardConcealView(payload, new HashSet<Guid>()); host.Attach(card); card.Bind(Guid.NewGuid());
        for (var i = 0; i < 3; i++)
        {
            card.Toggle(); Assert.True(card.IsConcealed); Assert.False(payload.IsEnabled); Assert.True(payload.InputTransparent);
            Assert.False(text.IsEnabled); Assert.False(photo.IsEnabled);
            Assert.True(payload.IsVisible); Assert.True(text.IsVisible); Assert.True(photo.IsVisible);
            Assert.Equal("Exact\ncontent 👀", text.Text); Assert.Equal(-1, text.MaxLines); Assert.Equal(LineBreakMode.WordWrap, text.LineBreakMode);
            Assert.Same(photoSource, photo.Source); Assert.Equal(130, photo.HeightRequest); Assert.Equal(160, photo.WidthRequest);
            card.Toggle(); Assert.False(card.IsConcealed); Assert.True(payload.IsEnabled); Assert.False(payload.InputTransparent);
            Assert.True(text.IsEnabled); Assert.True(photo.IsEnabled);
        }
    }

    [Fact]
    public async Task HiddenLinkCannotOpenOrProduceAPreviewAndOldMenuStaysInvalidAfterReveal()
    {
        var calls = new List<ActionDefinition>();
        Task Capture(ActionDefinition item) { calls.Add(item); return Task.CompletedTask; }
        var item = new ActionDefinition(Guid.NewGuid(), "Private link", new OpenUrlParameters("https://example.com/a%2Fb?q=complete#value"));
        var link = new ClipboardLinkLabel(Capture, Capture, Capture) { Item = item };
        var payload = new VerticalStackLayout { Children = { link } };
        var card = new ClipboardConcealView(payload, new HashSet<Guid>()); host.Attach(card); card.Bind(item.Id);
        var oldMenu = Assert.IsType<ClipboardLinkContext>(link.CaptureContext());
        card.Toggle(); Assert.False(link.IsEnabled); Assert.Null(link.CaptureContext()); Assert.False(oldMenu.IsCurrent);
        await oldMenu.OpenAsync(); await oldMenu.CopyAsync(); await oldMenu.ShareAsync(); Assert.Empty(calls);
        card.Toggle(); Assert.True(link.IsEnabled); Assert.False(oldMenu.IsCurrent);
        await oldMenu.OpenAsync(); await oldMenu.ShareAsync(); Assert.Empty(calls);
        var revealed = Assert.IsType<ClipboardLinkContext>(link.CaptureContext());
        await revealed.OpenAsync(); await revealed.CopyAsync(); await revealed.ShareAsync();
        Assert.Equal(3, calls.Count); Assert.All(calls, action => Assert.Same(item, action));
        Assert.Equal(((OpenUrlParameters)item.Parameters).Url, link.Text);
    }

    [Fact]
    public void EyeButtonSwitchesItsExistingGlyphAndAccessibleAction()
    {
        var eye = new ClipboardIconButton(StudioSymbol.Preview, "Hide content");
        var nativeTarget = Assert.IsType<Button>(Assert.IsType<Grid>(eye.Content).Children[0]);
        Assert.Equal(48, eye.MinimumWidthRequest); Assert.Equal(48, eye.MinimumHeightRequest);
        eye.Symbol = StudioSymbol.EyeOff; eye.Describe("Show content for", "Private item");
        Assert.Equal(StudioSymbol.EyeOff, eye.Symbol);
        Assert.Equal("Show content for Private item", SemanticProperties.GetDescription(nativeTarget));
        eye.Symbol = StudioSymbol.Preview; eye.Describe("Hide content for", "Private item");
        Assert.Equal(StudioSymbol.Preview, eye.Symbol);
        Assert.Equal("Hide content for Private item", SemanticProperties.GetDescription(nativeTarget));
    }

    public void Dispose() => host.Dispose();
}
