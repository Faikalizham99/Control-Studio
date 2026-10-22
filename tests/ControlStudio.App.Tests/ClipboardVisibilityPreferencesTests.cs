using ControlStudio.App.Views;
using ControlStudio.Core.Actions;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ClipboardVisibilityPreferencesTests : IDisposable
{
    private readonly MauiTestContext host = new();

    [Fact]
    public void HideAndRevealPersistAcrossNewPagesAndRecreatedPreferenceAdapters()
    {
        var persisted = new Dictionary<string, object>(); var id = Guid.NewGuid(); var other = Guid.NewGuid();
        var first = new ClipboardConcealView(new Label { Text = "Private text" }, new(new TestPreferences(persisted)));
        first.Bind(id); Assert.False(first.IsConcealed); first.Toggle(); Assert.True(first.IsConcealed);
        Assert.True(Assert.IsType<bool>(Assert.Single(persisted).Value));
        // Drop all page/store objects: only the simulated persistent preferences backend survives.
        var reopened = new ClipboardConcealView(new Label { Text = "Private text" }, new(new TestPreferences(persisted)));
        reopened.Bind(id); Assert.True(reopened.IsConcealed); Assert.False(reopened.AnimateTransition); Assert.False(reopened.Content.IsEnabled);
        reopened.Bind(other); Assert.False(reopened.IsConcealed); reopened.Bind(id); Assert.True(reopened.IsConcealed);
        reopened.Toggle(); Assert.False(reopened.IsConcealed); Assert.Empty(persisted);
        var restarted = new ClipboardConcealView(new Label(), new(new TestPreferences(persisted)));
        restarted.Bind(id); Assert.False(restarted.IsConcealed); Assert.False(restarted.AnimateTransition);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void VisibilityStaysWithIdentityThroughEditsPinningAndReordering(int kindValue)
    {
        var kind = (ClipboardKind)kindValue;
        var original = StudioContent.Draft(null, kind, "Name", kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : kind == ClipboardKind.Link ? "https://example.com" : "Exact\ntext  ");
        var backing = new TestPreferences(); var preferences = new ClipboardVisibilityPreferences(backing);
        preferences.SetConcealed(original.Id, true);
        var edited = StudioContent.Draft(original, kind, "New name", kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : kind == ClipboardKind.Link ? "https://example.org/new" : "Changed content") with { IsFavorite = true, SortOrder = 9, HeaderColorHex = "#00FFD2" };
        var other = StudioContent.Draft(null, kind, "Other", ((original.Parameters as CopyTextParameters)?.Text) ?? (kind == ClipboardKind.Photo ? Guid.NewGuid().ToString("N") : "https://example.net"));
        Assert.True(new ClipboardVisibilityPreferences(new TestPreferences(backing.Values)).IsConcealed(edited.Id));
        Assert.False(preferences.IsConcealed(other.Id));
        Assert.Equal(original.Id, edited.Id); Assert.Single(backing.Values);
        Assert.Equal(edited, StudioContent.ClipboardItems(new[] { other, edited }, kind)[0]);
    }

    [Fact]
    public void DeleteCleanupAndRevealingRemoveOnlyTheTargetFlagAndLeaveOtherPreferencesAlone()
    {
        var backing = new TestPreferences(); backing.Set("accent", "#00FFD2");
        var preferences = new ClipboardVisibilityPreferences(backing); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        preferences.SetConcealed(first, true); preferences.SetConcealed(second, true); preferences.Remove(first); preferences.Remove(first);
        var reopened = new ClipboardVisibilityPreferences(new TestPreferences(backing.Values));
        Assert.False(reopened.IsConcealed(first)); Assert.True(reopened.IsConcealed(second)); Assert.False(reopened.IsConcealed(Guid.NewGuid()));
        Assert.Equal("#00FFD2", backing.Get("accent", ""));
        reopened.SetConcealed(second, false); Assert.Equal("accent", Assert.Single(backing.Values).Key);
        Assert.Throws<ArgumentException>(() => preferences.SetConcealed(Guid.Empty, true)); Assert.Single(backing.Values);
    }

    [Fact]
    public void RestoredHiddenLinkIsDisabledBeforeAttachmentOrAnyRevealAnimation()
    {
        var item = new ActionDefinition(Guid.NewGuid(), "Private site", new OpenUrlParameters("https://example.com"));
        var preferences = new ClipboardVisibilityPreferences(new TestPreferences()); preferences.SetConcealed(item.Id, true);
        var link = new ClipboardLinkLabel(_ => Task.CompletedTask, _ => Task.CompletedTask, _ => Task.CompletedTask) { Item = item };
        var payload = new VerticalStackLayout { Children = { link } }; var card = new ClipboardConcealView(payload, preferences);
        card.Bind(item.Id);
        // Restore synchronously, before the view joins a page/native window.
        Assert.True(card.IsConcealed); Assert.False(card.AnimateTransition); Assert.False(link.IsEnabled); Assert.Null(link.CaptureContext());
        host.Attach(card); Assert.False(link.IsEnabled); Assert.True(payload.IsVisible); Assert.True(payload.InputTransparent);
        card.Toggle(); Assert.False(preferences.IsConcealed(item.Id)); Assert.NotNull(link.CaptureContext());
    }

    [Fact]
    public void ReadFailureKeepsContentHiddenAndARecycledItemCanRecoverWithoutAFlashAnimation()
    {
        var backing = new TestPreferences { FailReads = true }; var preferences = new ClipboardVisibilityPreferences(backing);
        var payload = new Label { Text = "Private text" }; var card = new ClipboardConcealView(payload, preferences);
        var errors = new List<ClipboardVisibilityErrorEventArgs>(); card.VisibilityFailed += (_, args) => errors.Add(args);
        var id = Guid.NewGuid(); card.Bind(id);
        Assert.True(card.IsConcealed); Assert.False(payload.IsEnabled); Assert.False(card.AnimateTransition); Assert.True(Assert.Single(errors).Loading);
        backing.FailReads = false; preferences.SetConcealed(id, true); card.Bind(id); Assert.True(card.IsConcealed);
        card.Bind(Guid.NewGuid()); Assert.False(card.IsConcealed); Assert.True(payload.IsEnabled); Assert.False(card.AnimateTransition);
        card.Bind(null); Assert.False(card.IsConcealed); Assert.Single(errors);
    }

    [Fact]
    public void FailedWritesLeaveTheDisplayedAndPersistedChoiceUnchangedAndReportAnError()
    {
        var backing = new TestPreferences(); var preferences = new ClipboardVisibilityPreferences(backing);
        var card = new ClipboardConcealView(new Label { Text = "Private text" }, preferences); var id = Guid.NewGuid(); card.Bind(id);
        var errors = new List<ClipboardVisibilityErrorEventArgs>(); card.VisibilityFailed += (_, args) => errors.Add(args);
        backing.FailWrites = true; card.Toggle(); Assert.False(card.IsConcealed); Assert.False(preferences.IsConcealed(id));
        backing.FailWrites = false; card.Toggle(); Assert.True(card.IsConcealed); Assert.True(preferences.IsConcealed(id));
        backing.FailWrites = true; card.Toggle(); Assert.True(card.IsConcealed); Assert.True(preferences.IsConcealed(id)); Assert.False(card.Content.IsEnabled);
        Assert.Equal(2, errors.Count); Assert.All(errors, error => Assert.False(error.Loading));
        backing.FailWrites = false; card.Toggle(); Assert.False(card.IsConcealed); Assert.False(preferences.IsConcealed(id));
    }

    public void Dispose() => host.Dispose();
}
