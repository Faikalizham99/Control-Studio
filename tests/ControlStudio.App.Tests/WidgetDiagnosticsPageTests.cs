using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetDiagnosticsPageTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private WidgetDiagnosticsPage Open(Func<Task<WidgetBackgroundReport>> read, Func<string, Task>? copy = null)
    {
        var page = new WidgetDiagnosticsPage(read, copy); host.App.Windows[0].Page = page;
        ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing(); return page;
    }
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in Descendants(child)) yield return value;
    }
    private static Button Copy(WidgetDiagnosticsPage page) => Descendants(page).OfType<Button>().Single(x => x.AutomationId == "CopyWidgetDiagnostics");
    private static string Status(WidgetDiagnosticsPage page) => Descendants(page).OfType<Label>().Single(x => x.AutomationId == "WidgetDiagnosticsStatus").Text;
    [Fact] public async Task CopyRequiresExplicitActionAndCopiesTheDisplayedReportExactly()
    {
        var copied = new List<string>(); var page = Open(() => Task.FromResult(new WidgetBackgroundReport("Ready", "exact report")), text => { copied.Add(text); return Task.CompletedTask; });
        Assert.Empty(copied); Assert.True(Copy(page).IsEnabled);
        await page.CopyAsync(); Assert.Equal("exact report", Assert.Single(copied)); Assert.Contains("Report copied", Status(page));
    }
    [Fact] public async Task LateReadAfterLeavingCannotUpdateThePageOrEnableCopy()
    {
        var pending = new TaskCompletionSource<WidgetBackgroundReport>(); var page = Open(() => pending.Task);
        Assert.False(Copy(page).IsEnabled); ((IPageController)page).SendDisappearing();
        pending.SetResult(new("late status", "late report")); await Task.Yield();
        Assert.DoesNotContain("late", Status(page)); Assert.False(Copy(page).IsEnabled);
    }
    [Fact] public async Task CopyFailureCannotClaimSuccessAndAllowsRetry()
    {
        var page = Open(() => Task.FromResult(new WidgetBackgroundReport("Ready", "report")), _ => throw new IOException("private details"));
        await page.CopyAsync(); Assert.Contains("could not be copied", Status(page)); Assert.DoesNotContain("private", Status(page)); Assert.True(Copy(page).IsEnabled);
    }
    [Fact] public async Task ReadFailureClearsTheOldReportAndDisablesCopy()
    {
        var fails = false; var page = Open(() => fails ? throw new IOException("private details") : Task.FromResult(new WidgetBackgroundReport("Ready", "old")));
        fails = true; await page.RefreshAsync(); Assert.False(Copy(page).IsEnabled); Assert.Contains("could not be loaded", Status(page));
        Assert.DoesNotContain("private", Status(page));
    }
    [Fact] public async Task RepeatedCopyCannotOverlapAndBackgroundedPageDoesNotShowSuccess()
    {
        var pending = new TaskCompletionSource(); var calls = 0;
        var page = Open(() => Task.FromResult(new WidgetBackgroundReport("Ready", "report")), _ => { calls++; return pending.Task; });
        var first = page.CopyAsync(); await page.CopyAsync(); Assert.Equal(1, calls);
        ((IWindow)host.App.Windows[0]).Deactivated(); pending.SetResult(); await first;
        Assert.DoesNotContain("copied", Status(page)); Assert.False(Copy(page).IsEnabled);
    }
    public void Dispose() => host.Dispose();
}
