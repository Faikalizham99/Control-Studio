using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal interface IAsyncPageLoad;

/// <summary>Single navigation per origin, transition-aware reads, content-free timings.</summary>
internal static class PageNavigation
{
    private sealed class Origin { public bool Opening; }
    private sealed class Opening(ILogger logger, string target, long started)
    {
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Aborted, Ready;
        public void Mark(string stage)
        {
            try { logger.LogInformation("Page timing Target={Target};Stage={Stage};ElapsedMs={ElapsedMs}", target, stage, Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2)); }
            catch { /* Optional timing must never change navigation behavior. */ }
        }
    }
    private static readonly ConditionalWeakTable<Page, Origin> origins = new();
    private static readonly ConditionalWeakTable<Page, Opening> openings = new();
    public static async Task PushAsync(Page owner, Func<Page> create, ILogger logger, CancellationToken token = default)
    {
        var origin = origins.GetValue(owner, _ => new());
        if (origin.Opening || !EditorPageActivity.IsForeground(owner)) return;
        origin.Opening = true; var started = Stopwatch.GetTimestamp();
        try
        {
            // Give queued pressed/loading feedback a UI turn before constructing
            // controls. UI controls themselves must remain on the UI thread.
            await Task.Yield();
            token.ThrowIfCancellationRequested(); if (!EditorPageActivity.IsForeground(owner)) return;
            var page = create(); var opening = new Opening(logger, page.GetType().Name, started);
            openings.Add(page, opening); opening.Mark("constructed");
            EventHandler appearing = (_, _) => opening.Mark("appearing"); page.Appearing += appearing;
            try
            {
                await owner.Navigation.PushAsync(page);
                opening.Mark("transition-complete");
                if (page is not IAsyncPageLoad) DataReady(page);
            }
            catch { opening.Aborted = true; throw; }
            finally { page.Appearing -= appearing; opening.Release.TrySetResult(); }
        }
        finally { origin.Opening = false; }
    }
    public static async Task<bool> WaitForTransitionAsync(Page page, CancellationToken token)
    {
        if (!openings.TryGetValue(page, out var opening)) return true;
        await opening.Release.Task.WaitAsync(token); return !opening.Aborted;
    }
    public static void DataReady(Page page)
    {
        if (!openings.TryGetValue(page, out var opening) || opening.Aborted || opening.Ready) return;
        opening.Ready = true; opening.Mark("data-ready");
    }
    public static async Task PopAsync(Page owner)
    {
        var origin = origins.GetValue(owner, _ => new());
        if (origin.Opening || !EditorPageActivity.IsForeground(owner)) return;
        origin.Opening = true;
        try
        {
            // Back can be available during a read, including its transition gate.
            // Serialize it behind the push rather than asking UIKit to push/pop together.
            if (await WaitForTransitionAsync(owner, default) && EditorPageActivity.IsForeground(owner))
                await owner.Navigation.PopAsync();
        }
        finally { origin.Opening = false; }
    }
}
