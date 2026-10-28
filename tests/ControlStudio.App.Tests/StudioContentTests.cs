using ControlStudio.Core;
using ControlStudio.Core.Actions;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class StudioContentTests
{
    [Fact]
    public void ClipboardLibraryKeepsSavedTextAndDoesNotMutateOtherRecords()
    {
        var first = new ActionDefinition(Guid.NewGuid(), "First", new CopyTextParameters("one"), SortOrder: 1);
        var last = new ActionDefinition(Guid.NewGuid(), "Last", new CopyTextParameters("two"), SortOrder: 3);
        var web = new ActionDefinition(Guid.NewGuid(), "Link", new OpenUrlParameters("https://example.com"), SortOrder: 0);
        var counter = new ActionDefinition(Guid.NewGuid(), "Counter", new IncrementCounterParameters(), SortOrder: 2);
        var saved = new[] { last, web, counter, first };
        Assert.Equal(new[] { first, last }, StudioContent.ClipboardItems(saved));
        Assert.Equal(new[] { last, web, counter, first }, saved);
    }

    [Fact]
    public void NewClipboardItemPreservesTextExactly()
    {
        const string payload = "  Line 1\nUnicode 🐱\n  ";
        var item = StudioContent.Draft(null, ClipboardKind.Text, "  Address  ", payload);
        Assert.NotEqual(Guid.Empty, item.Id); Assert.Equal("Address", item.Name);
        Assert.Equal(payload, Assert.IsType<CopyTextParameters>(item.Parameters).Text);
        Assert.Equal(ActionIcon.Copy, item.Icon);
        new ActionValidator().Validate(item);
    }

    [Fact]
    public void EditingClipboardPreservesItsIdentityAndExistingMetadata()
    {
        var original = new ActionDefinition(Guid.NewGuid(), "Old", new CopyTextParameters("old"), ActionCategory.Learning, ActionIcon.Star, ActionColor.Purple, true, 7);
        var edited = StudioContent.Draft(original, ClipboardKind.Text, "New", "new text");
        Assert.Equal(original with { Name = "New", Parameters = new CopyTextParameters("new text") }, edited);
        Assert.Equal("old", Assert.IsType<CopyTextParameters>(original.Parameters).Text);
    }

    [Fact]
    public void ClipboardEditorCannotOverwriteOtherActionKinds()
    {
        foreach (var parameters in new ActionParameters[] { new OpenUrlParameters("https://example.com"), new IncrementCounterParameters() })
            Assert.Throws<ArgumentException>(() => StudioContent.Draft(new ActionDefinition(Guid.NewGuid(), "Existing", parameters), ClipboardKind.Text, "New", "text"));
    }

    [Fact]
    public void ClipboardDraftStillUsesTheBoundedValidationContract()
    {
        foreach (var item in new[] { StudioContent.Draft(null, ClipboardKind.Text, " ", "text"), StudioContent.Draft(null, ClipboardKind.Text, "Name", ""),
            StudioContent.Draft(null, ClipboardKind.Text, "Name", new string('x', 10_001)), StudioContent.Draft(null, ClipboardKind.Text, "Name", "null\0character") })
            Assert.Throws<ActionValidationException>(() => new ActionValidator().Validate(item));
    }

    [Fact]
    public void WidgetPreviewsNeverPresentPrivateCounterAsShared()
    {
        var local = new CounterSnapshot(1, 4, 1, "local", false);
        Assert.Null(StudioContent.WidgetSnapshot(local));
        var shared = local with { Storage = "shared" };
        Assert.Same(shared, StudioContent.WidgetSnapshot(shared));
        Assert.Equal(4, shared.Value);
    }

    [Fact]
    public void InvalidCounterSnapshotCannotBecomeAWidgetPreview()
    {
        Assert.Throws<InvalidDataException>(() => StudioContent.WidgetSnapshot(new CounterSnapshot(2, 0, null, "shared", false)));
    }

    [Fact]
    public void ClipboardSwitcherKeepsKindsSeparateAndHidesCounterActions()
    {
        var values = new ActionParameters[] { new CopyTextParameters("text"), new PhotoParameters(Guid.NewGuid().ToString("N")),
            new OpenUrlParameters("https://example.com"), new IncrementCounterParameters() };
        var actions = values.Select((p, i) => new ActionDefinition(Guid.NewGuid(), "Name", p, SortOrder: i)).ToArray();
        foreach (var kind in Enum.GetValues<ClipboardKind>()) Assert.Equal(actions[(int)kind], Assert.Single(StudioContent.ClipboardItems(actions.Reverse(), kind)));
    }

    [Fact]
    public void AllClipboardEditsPreserveMetadataAndExactTextWhileLinksTrimOuterSpaces()
    {
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            var value = kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : "  payload\n🐱  ";
            var first = StudioContent.Draft(null, kind, "Name", value) with { IsFavorite = true, SortOrder = 7, Category = ActionCategory.Learning };
            var edited = StudioContent.Draft(first, kind, "  New name ", value);
            Assert.Equal(first with { Name = "New name" }, edited);
            Assert.Throws<ArgumentException>(() => StudioContent.Draft(first, (ClipboardKind)(((int)kind + 1) % 3), "Changed", value));
            if (kind == ClipboardKind.Text) Assert.Equal(value, ((CopyTextParameters)edited.Parameters).Text);
            if (kind == ClipboardKind.Link) Assert.Equal(value.Trim(), ((OpenUrlParameters)edited.Parameters).Url);
        }
    }

    [Fact]
    public void OrdinaryEditsKeepEachHeaderOverrideAndNullStillMeansAppAccent()
    {
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        foreach (var header in new string?[] { null, "#00FFD2" })
        {
            var value = kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : kind == ClipboardKind.Link ? "https://example.com" : "exact\ntext  ";
            var original = StudioContent.Draft(null, kind, "Name", value) with { HeaderColorHex = header, IsFavorite = true, SortOrder = 5 };
            var edited = StudioContent.Draft(original, kind, "Renamed", value);
            Assert.Equal(original with { Name = "Renamed" }, edited);
            Assert.Equal(header, edited.HeaderColorHex);
        }
    }
}
