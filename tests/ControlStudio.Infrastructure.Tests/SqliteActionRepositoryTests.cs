using ControlStudio.Core.Actions;
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
        var bytes = File.ReadAllBytes(FilePath); bytes[63] = 2; File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().SaveAsync(NewAction()));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
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
