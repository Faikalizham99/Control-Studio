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
        var item = StudioContent.ClipboardDraft(null, "  Address  ", payload);
        Assert.NotEqual(Guid.Empty, item.Id); Assert.Equal("Address", item.Name);
        Assert.Equal(payload, Assert.IsType<CopyTextParameters>(item.Parameters).Text);
        Assert.Equal(ActionIcon.Copy, item.Icon);
        new ActionValidator().Validate(item);
    }

    [Fact]
    public void EditingClipboardPreservesItsIdentityAndExistingMetadata()
    {
        var original = new ActionDefinition(Guid.NewGuid(), "Old", new CopyTextParameters("old"), ActionCategory.Learning, ActionIcon.Star, ActionColor.Purple, true, 7);
        var edited = StudioContent.ClipboardDraft(original, "New", "new text");
        Assert.Equal(original with { Name = "New", Parameters = new CopyTextParameters("new text") }, edited);
        Assert.Equal("old", Assert.IsType<CopyTextParameters>(original.Parameters).Text);
    }

    [Fact]
    public void ClipboardEditorCannotOverwriteOtherActionKinds()
    {
        foreach (var parameters in new ActionParameters[] { new OpenUrlParameters("https://example.com"), new IncrementCounterParameters() })
            Assert.Throws<ArgumentException>(() => StudioContent.ClipboardDraft(new ActionDefinition(Guid.NewGuid(), "Existing", parameters), "New", "text"));
    }

    [Fact]
    public void ClipboardDraftStillUsesTheBoundedValidationContract()
    {
        foreach (var item in new[] { StudioContent.ClipboardDraft(null, " ", "text"), StudioContent.ClipboardDraft(null, "Name", ""),
            StudioContent.ClipboardDraft(null, "Name", new string('x', 10_001)), StudioContent.ClipboardDraft(null, "Name", "null\0character") })
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
}
