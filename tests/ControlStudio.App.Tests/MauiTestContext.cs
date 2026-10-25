using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Dispatching;

namespace ControlStudio.App.Tests;

internal sealed class MauiTestContext : IDisposable
{
    private readonly Application? previousApp = Application.Current;
    private readonly IDispatcherProvider previousDispatcher = DispatcherProvider.Current;
    public Application App { get; }

    public MauiTestContext(Queue<Action>? dispatchQueue = null)
    {
        DispatcherProvider.SetCurrent(new InlineDispatcherProvider(dispatchQueue));
        App = new TestApplication { UserAppTheme = AppTheme.Light };
        ((IApplication)App).CreateWindow(null);
    }

    public void Attach(View view) => ((TestApplication)App).Root.Content = view;

    public void Dispose() { Application.Current = previousApp; DispatcherProvider.SetCurrent(previousDispatcher); }

    private sealed class TestApplication : Application
    {
        public ContentPage Root { get; } = new();
        protected override Window CreateWindow(IActivationState? activationState) => new(Root);
    }

    private sealed class InlineDispatcherProvider(Queue<Action>? queue) : IDispatcherProvider
    {
        public IDispatcher GetForCurrentThread() => new InlineDispatcher(queue);
    }
    private sealed class InlineDispatcher(Queue<Action>? queue) : IDispatcher
    {
        public bool IsDispatchRequired => false;
        public bool Dispatch(Action action) { if (queue is null) action(); else queue.Enqueue(action); return true; }
        public bool DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}
