using ControlStudio.Core.Actions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public async Task PinPatchesLatestMetadataAndSurvivesRelaunchWithoutMovingOtherItems()
    {
        var original = NewAction(); var other = NewAction("Other");
        await Repository().SaveAsync(original); await Repository().SaveAsync(other);
        var edited = original with { Name = "New name", HeaderColorHex = "#123456", Parameters = new CopyTextParameters("Updated\n🐱  ") };
        await Repository().SaveAsync(edited); await Repository().SetPinnedAsync(original.Id, true);
        var pinned = await Repository().ReadAsync(); Assert.Equal(edited with { IsFavorite = true }, pinned[0]);
        Assert.Equal(other with { SortOrder = 1 }, pinned[1]);
        await Repository().SetPinnedAsync(original.Id, false); Assert.Equal(edited, (await Repository().ReadAsync())[0]);
        var counter = NewAction("Counter") with { Parameters = new IncrementCounterParameters() }; await Repository().SaveAsync(counter);
        var before = await Repository().ReadAsync();
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().SetPinnedAsync(counter.Id, true));
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().SetPinnedAsync(Guid.NewGuid(), true));
        Assert.Equal(before, await Repository().ReadAsync());
    }

    [Theory]
    [InlineData("text")]
    [InlineData("photo")]
    [InlineData("link")]
    public async Task ArrangementIsAtomicPreservesOtherSlotsAndRetainsLatestContent(string type)
    {
        ActionParameters Parameters(int i) => type switch
        {
            "photo" => new PhotoParameters(Guid.NewGuid().ToString("N")),
            "link" => new OpenUrlParameters($"https://example.com/{i}?q=exact%20value#part"),
            _ => new CopyTextParameters($"Exact {i}\n🐱  ")
        };
        var selected = Enumerable.Range(0, 4).Select(i => NewAction($"Item {i}") with { Parameters = Parameters(i), IsFavorite = i < 2, HeaderColorHex = "#00FFD2" }).ToArray();
        var untouched = NewAction("Other") with { Parameters = type == "text" ? new OpenUrlParameters("https://example.org") : new CopyTextParameters("Other content") };
        var counter = NewAction("Counter") with { Parameters = new IncrementCounterParameters(), IsFavorite = true };
        foreach (var item in new[] { selected[0], untouched, selected[1], counter, selected[2], selected[3] }) await Repository().SaveAsync(item);
        // An edit made after the arrangement draft was captured must survive saving that draft.
        await Repository().SaveAsync(selected[1] with { Name = "Edited while arranging" });
        var before = await Repository().ReadAsync(); var lookup = before.ToDictionary(item => item.Id);
        await Repository().ReorderClipboardAsync(new[] { selected[1].Id, selected[0].Id, selected[3].Id, selected[2].Id });
        var result = await Repository().ReadAsync();
        var expected = new[] { selected[1].Id, untouched.Id, selected[0].Id, counter.Id, selected[3].Id, selected[2].Id };
        Assert.Equal(expected, result.Select(item => item.Id));
        for (var i = 0; i < result.Count; i++) Assert.Equal(lookup[expected[i]] with { SortOrder = i }, result[i]);
        Assert.Equal(before[1], result[1]); Assert.Equal(before[3], result[3]);
    }

    [Fact]
    public async Task InvalidStaleAndCancelledArrangementsLeaveTheEntireDocumentUnchanged()
    {
        var pinned = NewAction("Pinned") with { IsFavorite = true }; var normal = NewAction("Normal");
        var link = NewAction("Link") with { Parameters = new OpenUrlParameters("https://example.com") };
        foreach (var item in new[] { pinned, normal, link }) await Repository().SaveAsync(item);
        var before = await Repository().ReadAsync();
        foreach (var order in new[] { Array.Empty<Guid>(), new[] { pinned.Id, pinned.Id }, new[] { normal.Id, pinned.Id }, new[] { pinned.Id }, new[] { pinned.Id, Guid.NewGuid() }, new[] { pinned.Id, normal.Id, link.Id } })
        {
            await Assert.ThrowsAsync<ActionValidationException>(() => Repository().ReorderClipboardAsync(order));
            Assert.Equal(before, await Repository().ReadAsync());
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository().ReorderClipboardAsync(new[] { pinned.Id, normal.Id }, new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Repository().SetPinnedAsync(normal.Id, true, new CancellationToken(true)));
        Assert.Equal(before, await Repository().ReadAsync());
        await Repository().SaveAsync(NewAction("Added later")); before = await Repository().ReadAsync();
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().ReorderClipboardAsync(new[] { pinned.Id, normal.Id }));
        Assert.Equal(before, await Repository().ReadAsync());
        await Repository().DeleteAsync(pinned.Id); before = await Repository().ReadAsync();
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().ReorderClipboardAsync(new[] { pinned.Id, normal.Id }));
        Assert.Equal(before, await Repository().ReadAsync());
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
        var marker = Encoding.UTF8.GetBytes("\"schemaVersion\":4");
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
        var marker = Encoding.UTF8.GetBytes("\"schemaVersion\":4");
        var index = legacy.AsSpan().IndexOf(marker); Assert.True(index >= 0);
        legacy[index + marker.Length - 1] = (byte)'1'; File.WriteAllBytes(FilePath, legacy);
        Assert.Equal(text, Assert.Single(await Repository().ReadAsync()));
        Assert.Equal(legacy, File.ReadAllBytes(FilePath)); // A read never rewrites old data.

        var photo = NewAction("Photo") with { Parameters = new PhotoParameters(Guid.NewGuid().ToString("N")) };
        var link = NewAction("Link") with { Parameters = new OpenUrlParameters("https://example.com/?q=hello%20world") };
        await Repository().SaveAsync(photo); await Repository().SaveAsync(link);
        var reopened = await Repository().ReadAsync();
        Assert.Equal(new[] { text, photo with { SortOrder = 1 }, link with { SortOrder = 2 } }, reopened);
        Assert.Equal(4, File.ReadAllBytes(FilePath)[63]);
    }

    [Fact]
    public async Task UpgradeDeletesOnlyRetiredItemsAndPreservesRemainingData()
    {
        await WriteLegacyDocumentAsync();
        var kept = await Repository().ReadAsync();
        Assert.Equal(new[] { "Text", "Link", "Photo", "Counter" }, kept.Select(x => x.Name));
        Assert.Equal(new[] { 0, 1, 2, 3 }, kept.Select(x => x.SortOrder));
        Assert.Equal("exact\n🐱  ", Assert.IsType<CopyTextParameters>(kept[0].Parameters).Text);
        Assert.True(kept[0].IsFavorite); Assert.Equal(ActionCategory.Learning, kept[0].Category);
        Assert.Equal("https://example.com/?a=1", Assert.IsType<OpenUrlParameters>(kept[1].Parameters).Url);
        Assert.IsType<PhotoParameters>(kept[2].Parameters); Assert.IsType<IncrementCounterParameters>(kept[3].Parameters);
        Assert.Equal(4, File.ReadAllBytes(FilePath)[63]);
        Assert.Equal(kept, await Repository().ReadAsync());
        Assert.DoesNotContain("retired payload", Encoding.UTF8.GetString(File.ReadAllBytes(FilePath)));
        await Repository().SaveAsync(NewAction("New text")); Assert.Equal(5, (await Repository().ReadAsync()).Count);
    }

    [Fact]
    public async Task HeaderColoursSurviveRelaunchReorderEditsAndResetIndependently()
    {
        var text = NewAction("Text") with { HeaderColorHex = "#00FFD2", IsFavorite = true };
        var photo = NewAction("Photo") with { Parameters = new PhotoParameters(Guid.NewGuid().ToString("N")), HeaderColorHex = "#FFFFFF" };
        var link = NewAction("Link") with { Parameters = new OpenUrlParameters("https://example.com"), HeaderColorHex = "#000000" };
        foreach (var item in new[] { text, photo, link }) await Repository().SaveAsync(item);
        await Repository().MoveAsync(link.Id, -1);
        await Repository().SaveAsync(text with { Name = "Renamed" });
        var reopened = await Repository().ReadAsync();
        Assert.Equal(new[] { text with { Name = "Renamed" }, link with { SortOrder = 1 }, photo with { SortOrder = 2 } }, reopened);
        await Repository().SaveAsync(reopened[0] with { HeaderColorHex = null });
        var reset = await Repository().ReadAsync();
        Assert.Null(reset[0].HeaderColorHex); Assert.Equal(text.Parameters, reset[0].Parameters); Assert.True(reset[0].IsFavorite);
        Assert.Equal("#000000", reset[1].HeaderColorHex); Assert.Equal("#FFFFFF", reset[2].HeaderColorHex);
    }

    [Fact]
    public async Task SchemaThreeDefaultsFollowAccentAndUpgradePreservesExistingItems()
    {
        var original = NewAction("Existing"); await Repository().SaveAsync(original);
        var bytes = File.ReadAllBytes(FilePath);
        var marker = Encoding.UTF8.GetBytes("\"schemaVersion\":4");
        var index = bytes.AsSpan().IndexOf(marker); Assert.True(index >= 0);
        bytes[index + marker.Length - 1] = (byte)'3'; bytes[63] = 3; File.WriteAllBytes(FilePath, bytes);
        Assert.Equal(original, Assert.Single(await Repository().ReadAsync()));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
        var custom = NewAction("New custom") with { HeaderColorHex = "#007AFF" }; await Repository().SaveAsync(custom);
        var reopened = await Repository().ReadAsync();
        Assert.Equal(original, reopened[0]); Assert.Null(reopened[0].HeaderColorHex);
        Assert.Equal(custom with { SortOrder = 1 }, reopened[1]); Assert.Equal(4, File.ReadAllBytes(FilePath)[63]);
    }

    [Fact]
    public async Task MalformedSavedHeaderColourDoesNotResetOrOverwriteData()
    {
        await Repository().SaveAsync(NewAction() with { HeaderColorHex = "#007AFF" });
        var bytes = File.ReadAllBytes(FilePath);
        var marker = Encoding.UTF8.GetBytes("\"headerColorHex\":\"#007AFF\"");
        var index = bytes.AsSpan().IndexOf(marker); Assert.True(index >= 0);
        Encoding.UTF8.GetBytes("\"headerColorHex\":\"#GGGGGG\"").CopyTo(bytes, index); File.WriteAllBytes(FilePath, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(() => Repository().ReadAsync());
        await Assert.ThrowsAsync<ActionValidationException>(() => Repository().SaveAsync(NewAction("Another")));
        Assert.Equal(bytes, File.ReadAllBytes(FilePath));
    }

    [Fact]
    public async Task CancelledUpgradePreservesAllSavedBytes()
    {
        await WriteLegacyDocumentAsync(); var before = File.ReadAllBytes(FilePath);
        using var cancellation = new CancellationTokenSource();
        var repository = new SqliteActionRepository(FilePath, new CancelOnValidate(cancellation));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.ReadAsync(cancellation.Token));
        Assert.Equal(before, File.ReadAllBytes(FilePath));
        Assert.Equal(4, (await Repository().ReadAsync()).Count);
    }

    private sealed class CancelOnValidate(CancellationTokenSource cancellation) : IActionValidator
    {
        public void Validate(ActionDefinition action) { new ActionValidator().Validate(action); cancellation.Cancel(); }
    }

    [Fact]
    public async Task UnknownTypesAndFutureSchemasNeverTriggerPartialCleanup()
    {
        foreach (var future in new[] { false, true })
        {
            await WriteLegacyDocumentAsync(unknownType: !future, schema: future ? 9 : 2);
            var before = File.ReadAllBytes(FilePath);
            await Assert.ThrowsAsync<InvalidDataException>(() => Repository().ReadAsync());
            await Assert.ThrowsAsync<InvalidDataException>(() => Repository().SaveAsync(NewAction()));
            Assert.Equal(before, File.ReadAllBytes(FilePath));
            File.Delete(FilePath);
        }
    }

    private async Task WriteLegacyDocumentAsync(bool unknownType = false, int schema = 2)
    {
        var values = new[]
        {
            NewAction("Text") with { Parameters = new CopyTextParameters("exact\n🐱  "), IsFavorite = true, Category = ActionCategory.Learning },
            NewAction("Retired A") with { Parameters = new CopyTextParameters(new string('x', 400)) },
            NewAction("Link") with { Parameters = new OpenUrlParameters("https://example.com/?a=1") },
            NewAction("Retired B") with { Parameters = new CopyTextParameters(new string('x', 400)) },
            NewAction("Photo") with { Parameters = new PhotoParameters(Guid.NewGuid().ToString("N")) },
            NewAction("Counter") with { Parameters = new IncrementCounterParameters() }
        };
        foreach (var value in values) await Repository().SaveAsync(value);
        var original = JsonSerializer.Serialize(new DashboardDocument(4, (await Repository().ReadAsync()).ToList()), ActionJsonContext.Default.DashboardDocument);
        var legacy = JsonNode.Parse(original)!.AsObject(); legacy["schemaVersion"] = schema;
        foreach (var index in new[] { 1, 3 })
            legacy["actions"]![index]!["parameters"] = new JsonObject { ["type"] = "snippet", ["code"] = "retired payload 🐱", ["language"] = index == 1 ? 7 : 13 };
        if (unknownType) legacy["actions"]![2]!["parameters"]!["type"] = "runShell";
        // Replace the SQLite TEXT field with shorter valid JSON padded to the same UTF-8 length.
        // This creates a real older-format database without keeping the retired production model.
        var originalBytes = Encoding.UTF8.GetBytes(original); var legacyBytes = Encoding.UTF8.GetBytes(legacy.ToJsonString());
        Assert.True(legacyBytes.Length < originalBytes.Length);
        var bytes = File.ReadAllBytes(FilePath); var offset = bytes.AsSpan().IndexOf(originalBytes); Assert.True(offset >= 0);
        bytes.AsSpan(offset, originalBytes.Length).Fill((byte)' '); legacyBytes.CopyTo(bytes, offset); bytes[63] = 2;
        File.WriteAllBytes(FilePath, bytes);
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
