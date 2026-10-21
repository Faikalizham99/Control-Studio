using System.Text;
using ControlStudio.Core.Actions;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class SnippetDocumentTests
{
    [Fact]
    public void UserCodeCrossesTheJavascriptBoundaryOnlyAsBase64Data()
    {
        const string code = "</script><img src=x onerror=alert(1)>\"');window.pwned=true;//\n🐱";
        var script = SnippetDocument.SetScript(code, SnippetLanguage.Html, true);
        Assert.DoesNotContain("</script>", script); Assert.DoesNotContain("window.pwned", script);
        var encoded = script.Split('\'')[1]; Assert.Equal(code, Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        Assert.Contains("'Html', true", script);
    }

    [Fact]
    public async Task UntrustedClosingScriptCannotEnterTheDocumentAsABundledAsset()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => SnippetDocument.CreateAsync(_ => Task.FromResult("</ScRiPt>")));
    }

    [Fact]
    public void BeautifyAvailabilityMatchesBundledParsers()
    {
        Assert.True(SnippetDocument.CanFormat(SnippetLanguage.TypeScript)); Assert.True(SnippetDocument.CanFormat(SnippetLanguage.Json));
        Assert.True(SnippetDocument.CanFormat(SnippetLanguage.CSharp)); Assert.True(SnippetDocument.CanFormat(SnippetLanguage.C));
        Assert.True(SnippetDocument.CanFormat(SnippetLanguage.Cpp)); Assert.False(SnippetDocument.CanFormat(SnippetLanguage.Python));
        Assert.False(SnippetDocument.CanFormat(SnippetLanguage.PlainText));
    }

    [Fact]
    public void CFamilyNamesAreReadableAndExistingPersistedLanguageIdsStayStable()
    {
        Assert.Equal("C", StudioContent.LanguageName(SnippetLanguage.C));
        Assert.Equal("C++", StudioContent.LanguageName(SnippetLanguage.Cpp));
        Assert.Equal("C#", StudioContent.LanguageName(SnippetLanguage.CSharp));
        Assert.Equal(7, (int)SnippetLanguage.CSharp); Assert.Equal(12, (int)SnippetLanguage.Xml);
        Assert.Equal(13, (int)SnippetLanguage.C); Assert.Equal(14, (int)SnippetLanguage.Cpp);
        Assert.True(SnippetDocument.UsesClang(SnippetLanguage.CSharp));
        Assert.False(SnippetDocument.UsesClang(SnippetLanguage.JavaScript));
    }

    [Fact]
    public void OnlyInitialBundleNavigationIsAllowed()
    {
        const string bundle = "/private/containers/ControlStudio.app";
        Assert.True(SnippetDocument.AllowNavigation("file:///private/containers/ControlStudio.app/", bundle, false));
        Assert.True(SnippetDocument.AllowNavigation("about:blank", bundle, false));
        foreach (var url in new[] { "file:///private/containers/ControlStudio.app/secrets", "https://example.com", "javascript:alert(1)", "data:text/html,test" })
            Assert.False(SnippetDocument.AllowNavigation(url, bundle, false));
        Assert.False(SnippetDocument.AllowNavigation("file:///private/containers/ControlStudio.app/", bundle, true));
    }
}
