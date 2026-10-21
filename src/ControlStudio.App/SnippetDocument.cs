using System.Text;
using ControlStudio.Core.Actions;

namespace ControlStudio.App;

internal static class SnippetDocument
{
    public static readonly string[] ScriptFiles =
    [
        "vendor/prettier/standalone.js", "vendor/prettier/plugins/babel.js", "vendor/prettier/plugins/estree.js",
        "vendor/prettier/plugins/typescript.js", "vendor/prettier/plugins/html.js", "vendor/prettier/plugins/postcss.js",
        "vendor/prettier/plugins/markdown.js", "vendor/highlight/highlight.min.js", "vendor/highlight/languages/swift.min.js", "clang-formatter.js", "viewer.js"
    ];
    public static bool CanFormat(SnippetLanguage language) => language is SnippetLanguage.JavaScript or SnippetLanguage.TypeScript
        or SnippetLanguage.Json or SnippetLanguage.Html or SnippetLanguage.Css or SnippetLanguage.Markdown || UsesClang(language);

    public static bool UsesClang(SnippetLanguage language) => language is SnippetLanguage.C or SnippetLanguage.Cpp or SnippetLanguage.CSharp;

    public static string SetScript(string code, SnippetLanguage language, bool dark) =>
        $"window.studio.set('{Convert.ToBase64String(Encoding.UTF8.GetBytes(code))}', '{language}', {(dark ? "true" : "false")})";

    // MAUI's iOS HtmlWebViewSource uses the bundle directory as its simulated request URL.
    // Permit that initial document only; code preview content never navigates anywhere.
    public static bool AllowNavigation(string url, string bundlePath, bool finished) => !finished &&
        (url == "about:blank" || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile &&
            string.Equals(uri.LocalPath.TrimEnd('/'), bundlePath.TrimEnd('/'), StringComparison.Ordinal)));

    public static async Task<string> CreateAsync(Func<string, Task<string>> read, byte[]? clangWasm = null)
    {
        var html = await read("viewer.html");
        var scripts = new StringBuilder();
        foreach (var file in ScriptFiles)
        {
            var script = await read(file);
            // Trusted bundled code must never prematurely close its HTML element.
            if (script.Contains("</script", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid bundled snippet asset.");
            scripts.Append("<script nonce=\"studio-local\">").Append(script).Append("\n</script>");
        }
        return html.Replace("{{LIGHT_STYLE}}", await read("vendor/highlight/styles/github.min.css"))
            .Replace("{{DARK_STYLE}}", await read("vendor/highlight/styles/github-dark.min.css"))
            .Replace("{{CLANG_WASM}}", clangWasm is null ? "" : Convert.ToBase64String(clangWasm))
            .Replace("{{SCRIPTS}}", scripts.ToString());
    }
}
