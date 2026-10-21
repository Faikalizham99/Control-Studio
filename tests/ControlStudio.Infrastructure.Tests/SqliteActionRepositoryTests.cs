using ControlStudio.Core.Actions;
using System.Text;
using Xunit;

namespace ControlStudio.Infrastructure.Tests;

public sealed class SqliteActionRepositoryTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "ControlStudioTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(folder, "actions.sqlite3");
    private SqliteActionRepository Repository() => new(FilePath, new ActionValidator());
    private static ActionDefinition NewAction(string name = "Test") => new(Guid.NewGuid(), name, new CopyTextParameters("Quotes '\" and Unicode 🐱\nnext line"));

    [Fact]
    public async Task CreateEditMoveDeleteSurviveReopening()
    {
        var first = NewAction("First"); var second = NewAction("Second"); var third = NewAction("Third");
        var repo = Repository(); Assert.Empty(await repo.ReadAsync());
        await repo.SaveAsync(first); await repo.SaveAsync(second); await repo.SaveAsync(third);
        var edited = second with { Name = "Edited", IsFavorite = true, Category = ActionCategory.Finance, Color = ActionColor.Purple, Icon = ActionIcon.Star };
        await Repository().SaveAsync(edited);
        await Repository().MoveAsync(third.Id, -1);
        await Repository().DeleteAsync(first.Id);
        var actions = await Repository().ReadAsync();
        Assert.Equal(new[] { third.Id, second.Id }, actions.Select(x => x.Id));
        Assert.Equal(new[] { 0, 1 }, actions.Select(x => x.SortOrder));
        Assert.Equal(edited with { SortOrder = 1 }, actions[1]);
        Assert.Equal(third.Parameters, actions[0].Parameters);
    }

    [Fact]
    public async Task SeparateRepositoryInstancesDoNotLoseConcurrentEdits()
    {
        await Repository().ReadAsync();
        var actions = Enumerable.Range(0, 20).Select(i => NewAction($"Action {i}")).ToArray();
        await Task.WhenAll(actions.Select(x => Repository().SaveAsync(x)));
        var result = await Repository().ReadAsync();
        Assert.Equal(20, result.Count); Assert.Equal(20, result.Select(x => x.Id).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 20), result.Select(x => x.SortOrder));
    }

    [Fact]
    public async Task InvalidAndCancelledSavesLeaveExistingDataAlone()
    {
        var original = NewAction(); await Repository().SaveAsync(original);
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().SaveAsync(original with { Name = "" }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository().DeleteAsync(original.Id, new CancellationToken(true)));
        Assert.Equal(original, Assert.Single(await Repository().ReadAsync()));
    }

    [Fact]
    public async Task UnknownSchemaIsNeverReset()
    {
        await Repository().SaveAsync(NewAction());
        var bytes = File.ReadAllBytes(FilePath); bytes[63] = 9; File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().SaveAsync(NewAction()));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task UnknownDocumentSchemaIsPreservedOnReadAndSave()
    {
        await Repository().SaveAsync(NewAction());
        var bytes = File.ReadAllBytes(FilePath);
        var marker = Encoding.UTF8.GetBytes("\"schemaVersion\":2");
        var index = bytes.AsSpan().IndexOf(marker);
        Assert.True(index >= 0);
        bytes[index + marker.Length - 1] = (byte)'9';
        File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().ReadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().SaveAsync(NewAction()));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task UnknownSavedActionTypeIsPreserved()
    {
        await Repository().SaveAsync(NewAction());
        var bytes = File.ReadAllBytes(FilePath);
        var marker = Encoding.UTF8.GetBytes("copyText");
        var index = bytes.AsSpan().IndexOf(marker);
        Assert.True(index >= 0);
        Encoding.UTF8.GetBytes("runShell").CopyTo(bytes, index);
        File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().ReadAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().SaveAsync(NewAction()));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task LegacyTextSurvivesUpgradeAndAllClipboardKindsReopen()
    {
        var text = NewAction("Existing text"); await Repository().SaveAsync(text);
        // Construct the schema-1 bytes produced by the previous release.
        var legacy = File.ReadAllBytes(FilePath); legacy[63] = 1;
        var marker = Encoding.UTF8.GetBytes("\"schemaVersion\":2");
        var index = legacy.AsSpan().IndexOf(marker); Assert.True(index >= 0);
        legacy[index + marker.Length - 1] = (byte)'1'; File.WriteAllBytes(FilePath, legacy);
        Assert.Equal(text, Assert.Single(await Repository().ReadAsync()));
        Assert.Equal(legacy, File.ReadAllBytes(FilePath)); // A read never rewrites old data.

        var photo = NewAction("Photo") with { Parameters = new PhotoParameters(Guid.NewGuid().ToString("N")) };
        var code = NewAction("Code") with { Parameters = new SnippetParameters("const cat = '🐱';\n", SnippetLanguage.JavaScript) };
        var link = NewAction("Link") with { Parameters = new OpenUrlParameters("https://example.com/?q=hello%20world") };
        await Repository().SaveAsync(photo); await Repository().SaveAsync(code); await Repository().SaveAsync(link);
        var reopened = await Repository().ReadAsync();
        Assert.Equal(new[] { text, photo with { SortOrder = 1 }, code with { SortOrder = 2 }, link with { SortOrder = 3 } }, reopened);
        Assert.Equal(2, File.ReadAllBytes(FilePath)[63]);
    }

    [Fact]
    public async Task CFamilySnippetLanguagesAndExactCodeSurviveReopening()
    {
        const string code = "// 🐱\n  keep exact whitespace\t\n";
        foreach (var language in new[] { SnippetLanguage.CSharp, SnippetLanguage.C, SnippetLanguage.Cpp })
            await Repository().SaveAsync(NewAction(language.ToString()) with { Parameters = new SnippetParameters(code, language) });
        var saved = await Repository().ReadAsync();
        Assert.Equal(new[] { SnippetLanguage.CSharp, SnippetLanguage.C, SnippetLanguage.Cpp },
            saved.Select(item => Assert.IsType<SnippetParameters>(item.Parameters).Language));
        Assert.All(saved, item => Assert.Equal(code, ((SnippetParameters)item.Parameters).Code));
    }

    [Fact]
    public async Task CorruptDatabaseIsPreserved()
    {
        Directory.CreateDirectory(folder);
        var bytes = new byte[100]; bytes[0] = 77; File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<IOException>(() => Repository().ReadAsync());
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task MoveAtBoundaryAndDeleteTwiceAreSafe()
    {
        var action = NewAction(); await Repository().SaveAsync(action);
        await Repository().MoveAsync(action.Id, -1); await Repository().MoveAsync(action.Id, 1);
        Assert.Equal(action.Id, Assert.Single(await Repository().ReadAsync()).Id);
        await Repository().DeleteAsync(action.Id); await Repository().DeleteAsync(action.Id);
        Assert.Empty(await Repository().ReadAsync());
    }

    [Fact]
    public async Task ActionLimitIsEnforcedWithoutDroppingData()
    {
        var repository = Repository();
        for (var i = 0; i < SqliteActionRepository.MaximumActions; i++) await repository.SaveAsync(NewAction());
        await Assert.ThrowsAsync<ActionValidationException>(() => repository.SaveAsync(NewAction()));
        Assert.Equal(SqliteActionRepository.MaximumActions, (await Repository().ReadAsync()).Count);
    }

    public void Dispose() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}
