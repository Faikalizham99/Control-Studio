using System.Text;
using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

internal sealed class SnippetPage : ContentPage
{
    private readonly WebView web = new() { AutomationId = "SnippetCodePreview" };
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly Button beautify = DashboardStyle.Button("Beautify");
    private readonly Button apply;
    private readonly Button reset = DashboardStyle.Button("Original");
    private readonly Func<string, Task> save;
    private readonly SnippetLanguage language;
    private readonly string original;
    private string code;
    private string savedCode;
    private bool busy, loaded, started, active;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public SnippetPage(string title, SnippetParameters snippet, Func<string, Task> save, bool editing = false)
    {
        this.save = save; original = code = savedCode = snippet.Code; language = snippet.Language;
        Title = title; AutomationId = "SnippetPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        apply = DashboardStyle.Button(editing ? "Use formatting" : "Save formatting");
        var copy = DashboardStyle.Button("Copy");
        copy.Clicked += async (_, _) => await RunAsync(async () => { await Clipboard.Default.SetTextAsync(code); status.Text = "Code copied."; });
        beautify.Clicked += async (_, _) => await RunAsync(BeautifyAsync);
        reset.Clicked += async (_, _) => await RunAsync(async () => { await web.EvaluateJavaScriptAsync(SnippetDocument.SetScript(original, language, IsDark)); code = original; status.Text = "Original code restored in this preview."; UpdateButtons(); });
        apply.Clicked += async (_, _) => await RunAsync(async () => { await save(code); savedCode = code; status.Text = editing ? "Formatting applied to the draft. Tap Save to keep it." : "Formatted snippet saved."; if (editing && active) await Navigation.PopAsync(); });
        var actions = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        actions.Add(copy); actions.Add(beautify, 1); actions.Add(reset, 2);
        var header = new VerticalStackLayout { Padding = 16, Spacing = 10, Children =
        {
            SettingsControls.Text(StudioContent.LanguageName(language), 15, bold: true),
            SettingsControls.Text(SnippetDocument.CanFormat(language) ? "Beautify previews the formatting. Save when you are happy with it." : "Syntax-coloured code preview. Beautify is unavailable for this language.", 13, secondary: true),
            actions, status
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        layout.Add(header); layout.Add(web, 0, 1); apply.Margin = 16; layout.Add(apply, 0, 2); Content = layout;
        web.Navigating += (_, e) => e.Cancel = !SnippetDocument.AllowNavigation(e.Url, Foundation.NSBundle.MainBundle.BundlePath, ready.Task.IsCompleted);
        web.Navigated += (_, e) =>
        {
            if (e.Url == "about:blank") return;
            if (e.Result == WebNavigationResult.Success) ready.TrySetResult();
            else ready.TrySetException(new IOException("Code preview could not load."));
        };
        web.ProcessTerminated += (_, _) => { loaded = false; status.Text = "Code preview closed. Go back and open this snippet again."; UpdateButtons(); };
        UpdateButtons();
    }

    private bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true;
        if (Application.Current is { } app) app.RequestedThemeChanged += ThemeChanged;
        if (started)
        {
            if (loaded) await RunAsync(async () => { await web.EvaluateJavaScriptAsync($"window.studio.theme({(IsDark ? "true" : "false")})"); });
            return;
        }
        started = true;
        await RunAsync(async () =>
        {
            byte[]? clangWasm = null;
            if (SnippetDocument.UsesClang(language))
            {
                using var wasm = await FileSystem.Current.OpenAppPackageFileAsync("Snippets/vendor/clang/clang-format.wasm");
                using var bytes = new MemoryStream(); await wasm.CopyToAsync(bytes); clangWasm = bytes.ToArray();
            }
            var html = await SnippetDocument.CreateAsync(async file => { using var stream = await FileSystem.Current.OpenAppPackageFileAsync("Snippets/" + file); using var reader = new StreamReader(stream); return await reader.ReadToEndAsync(); }, clangWasm);
            if (!Navigation.NavigationStack.Contains(this)) return;
            web.Source = new HtmlWebViewSource { Html = html };
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (!Navigation.NavigationStack.Contains(this)) return;
            var response = await web.EvaluateJavaScriptAsync(SnippetDocument.SetScript(code, language, IsDark));
            if (response?.Trim('"') != "ready") throw new IOException("Code preview could not initialize.");
            loaded = true; UpdateButtons();
        });
    }
    protected override void OnDisappearing()
    {
        active = false; if (Application.Current is { } app) app.RequestedThemeChanged -= ThemeChanged;
        base.OnDisappearing();
    }
    private async void ThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (loaded) await RunAsync(async () => { await web.EvaluateJavaScriptAsync($"window.studio.theme({(IsDark ? "true" : "false")})"); });
    }
    private async Task BeautifyAsync()
    {
        await web.EvaluateJavaScriptAsync("window.studio.format(); 'started'");
        for (var attempt = 0; attempt < 100 && active; attempt++)
        {
            var result = (await web.EvaluateJavaScriptAsync("window.studio.result()"))?.Trim('"') ?? "";
            if (result.StartsWith("ok:", StringComparison.Ordinal))
            {
                code = Encoding.UTF8.GetString(Convert.FromBase64String(result[3..]));
                status.Text = "Beautified preview. Your saved code changes only when you save."; UpdateButtons(); return;
            }
            if (result == "error") { status.Text = "Could not format this code. Check its syntax and selected language. Original code was kept."; return; }
            if (result == "unavailable") { status.Text = "Beautify could not load. Close and reopen this preview to try again. Your code was kept."; return; }
            await Task.Delay(50);
        }
        status.Text = "Formatting did not finish. Reopen this preview to try again."; loaded = false; UpdateButtons();
    }
    private void UpdateButtons()
    {
        beautify.IsEnabled = loaded && !busy && SnippetDocument.CanFormat(language);
        reset.IsEnabled = loaded && !busy && code != original;
        apply.IsEnabled = loaded && !busy && code != savedCode;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy || !active) return; busy = true; UpdateButtons();
        try { await action(); }
        catch (Exception) { status.Text = "Code preview could not finish this action. Your saved snippet was kept."; }
        finally { busy = false; UpdateButtons(); }
    }
}
