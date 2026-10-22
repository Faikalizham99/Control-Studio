namespace ControlStudio.App;

internal sealed record ClipboardCopyNotice(string Message, Guid? ItemId, bool IsError);

/// <summary>One replaceable, transient notice. Call from the UI context; never persists content.</summary>
internal sealed class ClipboardCopyFeedback : BindableObject
{
    private static readonly BindablePropertyKey NoticePropertyKey = BindableProperty.CreateReadOnly(
        nameof(Notice), typeof(ClipboardCopyNotice), typeof(ClipboardCopyFeedback), null);
    public static readonly BindableProperty NoticeProperty = NoticePropertyKey.BindableProperty;
    public ClipboardCopyNotice? Notice => (ClipboardCopyNotice?)GetValue(NoticeProperty);
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private CancellationTokenSource? pending;

    public ClipboardCopyFeedback() : this(Task.Delay) { }
    internal ClipboardCopyFeedback(Func<TimeSpan, CancellationToken, Task> delay) => this.delay = delay;

    public Task SuccessAsync(Guid itemId, bool screenReader = false) => ShowAsync(
        new("Copied to clipboard", itemId, false), TimeSpan.FromSeconds(screenReader ? 4 : 2));
    public Task ErrorAsync() => ShowAsync(new("Could not copy. Please try again.", null, true), TimeSpan.FromSeconds(4));

    public void Clear()
    {
        var previous = pending; pending = null;
        previous?.Cancel(); SetValue(NoticePropertyKey, null);
    }

    private async Task ShowAsync(ClipboardCopyNotice notice, TimeSpan duration)
    {
        Clear();
        var current = new CancellationTokenSource(); pending = current;
        SetValue(NoticePropertyKey, notice);
        try
        {
            await delay(duration, current.Token);
            if (ReferenceEquals(pending, current)) SetValue(NoticePropertyKey, null);
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(pending, current)) pending = null;
            current.Dispose();
        }
    }
}
