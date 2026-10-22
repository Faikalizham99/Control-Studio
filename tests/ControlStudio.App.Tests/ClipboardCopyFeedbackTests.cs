using ControlStudio.App.Views;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ClipboardCopyFeedbackTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private readonly ManualDelay delay = new();

    [Fact]
    public async Task NewCopyReplacesToastAndOldExpiryCannotClearTheNewOne()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait);
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var first = feedback.SuccessAsync(firstId);
        Assert.Equal(TimeSpan.FromSeconds(2), delay.Entries[0].Duration);
        var second = feedback.SuccessAsync(secondId);
        Assert.True(delay.Entries[0].Token.IsCancellationRequested);
        delay.Entries[0].Complete(); await first;
        Assert.Equal(secondId, feedback.Notice?.ItemId);
        delay.Entries[1].Complete(); await second;
        Assert.Null(feedback.Notice);
    }

    [Fact]
    public async Task ClearCancelsPendingFeedbackAndLateCompletionCannotRestoreIt()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait);
        var pending = feedback.SuccessAsync(Guid.NewGuid());
        feedback.Clear(); feedback.Clear();
        Assert.Null(feedback.Notice); Assert.True(delay.Entries[0].Token.IsCancellationRequested);
        delay.Entries[0].Complete(); await pending;
        Assert.Null(feedback.Notice);
        var next = feedback.SuccessAsync(Guid.NewGuid());
        Assert.NotNull(feedback.Notice); delay.Entries[1].Complete(); await next;
    }

    [Fact]
    public async Task RepeatedCopyRestartsDurationAndCanceledDelayDoesNotFault()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait); var id = Guid.NewGuid();
        var first = feedback.SuccessAsync(id);
        var second = feedback.SuccessAsync(id);
        delay.Entries[0].Cancel(); await first;
        Assert.Equal(id, feedback.Notice?.ItemId);
        Assert.Equal(TimeSpan.FromSeconds(2), delay.Entries[1].Duration);
        feedback.Clear(); delay.Entries[1].Cancel(); await second;
        Assert.Null(feedback.Notice);
    }

    [Fact]
    public async Task FailureClearsSuccessAndScreenReaderGetsMoreReadingTime()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait);
        var success = feedback.SuccessAsync(Guid.NewGuid(), screenReader: true);
        Assert.Equal(TimeSpan.FromSeconds(4), delay.Entries[0].Duration);
        var error = feedback.ErrorAsync();
        Assert.True(feedback.Notice?.IsError); Assert.Null(feedback.Notice?.ItemId);
        Assert.Equal("Could not copy. Please try again.", feedback.Notice?.Message);
        Assert.Equal(TimeSpan.FromSeconds(4), delay.Entries[1].Duration);
        delay.Entries[0].Complete(); await success;
        Assert.True(feedback.Notice?.IsError);
        delay.Entries[1].Complete(); await error; Assert.Null(feedback.Notice);
    }

    [Fact]
    public async Task CheckmarkTracksOnlyCopiedItemAndRebindingCannotLeakIt()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait);
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid();
        var first = new ClipboardCopyButton(feedback); first.Bind(firstId, "First");
        var second = new ClipboardCopyButton(feedback); second.Bind(secondId, "Second");
        host.Attach(new VerticalStackLayout { Children = { first, second } });
        var pending = feedback.SuccessAsync(firstId);
        Assert.Equal(StudioSymbol.Check, first.Symbol); Assert.Equal(StudioSymbol.Clipboard, second.Symbol);
        first.Bind(secondId, "Recycled"); Assert.Equal(StudioSymbol.Clipboard, first.Symbol);
        first.Bind(null, "item"); Assert.Equal(StudioSymbol.Clipboard, first.Symbol);
        first.Bind(firstId, "First edited"); Assert.Equal(StudioSymbol.Check, first.Symbol);
        delay.Entries[0].Complete(); await pending;
        Assert.Equal(StudioSymbol.Clipboard, first.Symbol);
    }

    [Fact]
    public async Task ToastIsPassiveWrapsTextAndAdaptsToThemeAndAccentAfterError()
    {
        var feedback = new ClipboardCopyFeedback(delay.Wait);
        var toast = new ClipboardToastView(feedback); host.Attach(toast);
        Assert.False(toast.IsVisible); Assert.True(toast.InputTransparent); Assert.True(toast.CascadeInputTransparent);
        Assert.Equal(LayoutOptions.End, toast.VerticalOptions);
        var surface = Assert.IsType<Border>(toast.Content);
        var row = Assert.IsType<Grid>(surface.Content);
        var glyph = Assert.IsType<StudioGlyph>(row.Children[0]);
        var label = Assert.IsType<Label>(row.Children[1]);
        Assert.True(label.FontAutoScalingEnabled); Assert.NotEqual(1, label.MaxLines);
        var error = feedback.ErrorAsync();
        Assert.True(toast.IsVisible); Assert.Equal(StudioSymbol.Clear, glyph.Symbol);
        var success = feedback.SuccessAsync(Guid.NewGuid());
        host.App.Resources["AccentText"] = Colors.Teal;
        Assert.Equal(Colors.Teal, glyph.Tint);
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.Dark })
        {
            host.App.UserAppTheme = theme;
            Assert.True(AppearanceColors.Contrast(label.TextColor, surface.BackgroundColor) >= 4.5);
            Assert.Equal(Colors.Teal, glyph.Tint);
        }
        Assert.Equal(StudioSymbol.Check, glyph.Symbol); Assert.Equal("Copied to clipboard", label.Text);
        delay.Entries[0].Complete(); await error;
        Assert.True(toast.IsVisible);
        delay.Entries[1].Complete(); await success;
        Assert.False(toast.IsVisible); Assert.Equal("", label.Text);
    }

    public void Dispose() => host.Dispose();

    private sealed class ManualDelay
    {
        public List<Entry> Entries { get; } = [];
        public Task Wait(TimeSpan duration, CancellationToken token)
        {
            var entry = new Entry(duration, token); Entries.Add(entry); return entry.Task;
        }
        public sealed class Entry(TimeSpan duration, CancellationToken token)
        {
            private readonly TaskCompletionSource completion = new();
            public TimeSpan Duration { get; } = duration;
            public CancellationToken Token { get; } = token;
            public Task Task => completion.Task;
            public void Complete() => completion.SetResult();
            public void Cancel() => completion.SetCanceled(Token);
        }
    }
}
