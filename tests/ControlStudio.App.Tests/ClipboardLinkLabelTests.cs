using ControlStudio.App.Views;
using ControlStudio.Core.Actions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ClipboardLinkLabelTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static ActionDefinition Link(string url = "https://example.com/a%2Fb?q=hello%20world#fragment") => new(Guid.NewGuid(), "Saved website", new OpenUrlParameters(url));

    [Fact]
    public void LinkIsUnderlinedAndUsesReadableBlueAcrossLiveThemeChanges()
    {
        var link = new ClipboardLinkLabel(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask) { Item = Link() };
        host.Attach(link);
        Assert.Equal(TextDecorations.Underline, link.TextDecorations); Assert.True(link.MinimumHeightRequest >= 44);
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark, AppTheme.Light })
        {
            host.App.UserAppTheme = theme;
            Assert.Equal(theme == AppTheme.Dark ? "#64ACFF" : "#0062D1", AppearanceColors.ToRgbHex(link.TextColor));
            foreach (var background in theme == AppTheme.Dark ? new[] { "#101116", "#1C1E27", "#303238" } : new[] { "#FFFFFF", "#F2F2F7" })
                Assert.True(AppearanceColors.Contrast(link.TextColor, Color.FromArgb(background)) >= 4.5);
        }
    }

    [Fact]
    public async Task MerelyDisplayingAndCapturingAMenuDoesNotRunActionsAndFullUrlIsPreserved()
    {
        var original = Link(); var copied = new List<ActionDefinition>(); var opened = new List<ActionDefinition>(); var shared = new List<ActionDefinition>();
        var link = new ClipboardLinkLabel(item => { opened.Add(item); return Task.CompletedTask; }, item => { copied.Add(item); return Task.CompletedTask; }, item => { shared.Add(item); return Task.CompletedTask; }) { Item = original };
        var menu = Assert.IsType<ClipboardLinkContext>(link.CaptureContext());
        Assert.Empty(copied); Assert.Empty(opened); Assert.Empty(shared);
        Assert.Equal(((OpenUrlParameters)original.Parameters).Url, link.Text);
        Assert.Equal(link.Text, menu.Url.OriginalString);
        link.MaxLines = 3; link.LineBreakMode = LineBreakMode.TailTruncation;
        await menu.CopyAsync(); await menu.OpenAsync(); await menu.ShareAsync();
        Assert.Same(original, Assert.Single(copied)); Assert.Same(original, Assert.Single(opened)); Assert.Same(original, Assert.Single(shared));
    }

    [Fact]
    public async Task RecycledOrEditedItemsInvalidateOldMenuActions()
    {
        var calls = new List<ActionDefinition>();
        Task Capture(ActionDefinition item) { calls.Add(item); return Task.CompletedTask; }
        var original = Link(); var link = new ClipboardLinkLabel(Capture, Capture, Capture) { Item = original };
        var oldMenu = Assert.IsType<ClipboardLinkContext>(link.CaptureContext());
        link.Item = original with { Parameters = new OpenUrlParameters("https://example.org/new") };
        Assert.False(oldMenu.IsCurrent);
        await oldMenu.CopyAsync(); await oldMenu.OpenAsync(); await oldMenu.ShareAsync(); Assert.Empty(calls);
        var current = Assert.IsType<ClipboardLinkContext>(link.CaptureContext()); await current.CopyAsync();
        Assert.Equal("https://example.org/new", Assert.IsType<OpenUrlParameters>(Assert.Single(calls).Parameters).Url);
        link.Item = null; Assert.Equal("", link.Text); Assert.Null(link.CaptureContext()); Assert.False(current.IsCurrent);
        await current.OpenAsync(); Assert.Single(calls);
    }

    [Fact]
    public async Task DisabledLinksDoNotOfferOrExecuteActions()
    {
        var count = 0;
        Task Capture(ActionDefinition _) { count++; return Task.CompletedTask; }
        var link = new ClipboardLinkLabel(Capture, Capture, Capture) { Item = Link() };
        var menu = Assert.IsType<ClipboardLinkContext>(link.CaptureContext());
        link.IsEnabled = false; Assert.Null(link.CaptureContext());
        await menu.CopyAsync(); await menu.OpenAsync(); await menu.ShareAsync(); Assert.Equal(0, count);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///private/file")]
    [InlineData("data:text/html,test")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/has space")]
    public void InvalidAddressesNeverProduceAContext(string url)
    {
        var link = new ClipboardLinkLabel(_ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException()) { Item = Link(url) };
        Assert.Null(link.CaptureContext());
    }

    [Fact]
    public void TextAndPhotoItemsCannotBeInterpretedAsLinks()
    {
        var link = new ClipboardLinkLabel(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask);
        foreach (var parameters in new ActionParameters[] { new CopyTextParameters("https://example.com"), new PhotoParameters(Guid.NewGuid().ToString("N")) })
        {
            link.Item = new ActionDefinition(Guid.NewGuid(), "Other item", parameters);
            Assert.Equal("", link.Text); Assert.Null(link.CaptureContext());
        }
    }
    public void Dispose() => host.Dispose();
}
