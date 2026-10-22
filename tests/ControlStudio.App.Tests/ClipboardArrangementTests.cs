using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ControlStudio.Core.Actions;
using ControlStudio.App.Views;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class ClipboardArrangementTests
{
    private static ActionDefinition Item(string name, int order, bool pinned = false, ActionParameters? parameters = null) =>
        new(Guid.NewGuid(), name, parameters ?? new CopyTextParameters("Exact\ncontent 🐱  "), IsFavorite: pinned, SortOrder: order, HeaderColorHex: "#00FFD2");

    [Fact]
    public void EachTabPlacesPinnedItemsFirstAndKeepsSavedOrderWithinBothGroups()
    {
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            ActionParameters parameters = kind switch { ClipboardKind.Photo => new PhotoParameters(Guid.NewGuid().ToString("N")), ClipboardKind.Link => new OpenUrlParameters("https://example.com"), _ => new CopyTextParameters("text") };
            var first = Item("first", 0, parameters: parameters); var pinnedLast = Item("pinned last", 3, true, parameters);
            var pinnedFirst = Item("pinned first", 1, true, parameters); var last = Item("last", 2, parameters: parameters);
            var counter = Item("counter", 4, true, new IncrementCounterParameters());
            Assert.Equal(new[] { pinnedFirst, pinnedLast, first, last }, StudioContent.ClipboardItems(new[] { last, counter, pinnedLast, first, pinnedFirst }, kind));
        }
    }

    [Fact]
    public void ArrangementIsADraftAndMovesOnlyWithinItsGroupsWithoutChangingSavedData()
    {
        var source = new[] { Item("one", 0, true), Item("two", 1, true), Item("three", 2), Item("four", 3) };
        var original = source.ToArray(); var draft = new ClipboardArrangement(source); var changes = 0;
        draft.Changed += (_, _) => changes++;
        Assert.False(draft.HasChanges);
        Assert.True(draft.Move(0, 0, 1)); Assert.True(draft.Move(1, 1, 0));
        Assert.Equal(new[] { source[1].Id, source[0].Id, source[3].Id, source[2].Id }, draft.OrderedIds);
        Assert.True(draft.HasChanges); Assert.Equal(2, changes); Assert.Equal(original, source);
        Assert.Same(source[1], draft.Pinned[0]); Assert.Same(source[3], draft.Others[0]);
        Assert.True(draft.Move(0, 1, 0)); Assert.True(draft.Move(1, 0, 1)); Assert.False(draft.HasChanges);
        foreach (var move in new[] { (-1, 0, 1), (2, 0, 1), (0, -1, 0), (0, 0, 2), (1, 0, 0) }) Assert.False(draft.Move(move.Item1, move.Item2, move.Item3));
        Assert.Equal(4, changes); Assert.Equal(original.Select(item => item.Id), draft.OrderedIds);
    }

    [Fact]
    public void InvalidOrMixedDraftsAreRejectedAndEmptySingleGroupsRemainSafe()
    {
        var text = Item("one", 0); var link = Item("link", 1, parameters: new OpenUrlParameters("https://example.com"));
        Assert.Throws<ArgumentException>(() => new ClipboardArrangement(new[] { text, text }));
        Assert.Throws<ArgumentException>(() => new ClipboardArrangement(new[] { text, link }));
        Assert.Throws<ArgumentException>(() => new ClipboardArrangement(new[] { Item("counter", 0, parameters: new IncrementCounterParameters()) }));
        var empty = new ClipboardArrangement([]); Assert.Empty(empty.OrderedIds); Assert.False(empty.Move(0, 0, 1));
        var single = new ClipboardArrangement(new[] { text }); Assert.Empty(single.Pinned); Assert.False(single.Move(1, 0, 1));
    }

    [Fact]
    public void PinTransitionsEmitMovesInsteadOfResettingTheLibraryAndRetainExactRecords()
    {
        var first = Item("first", 0); var middle = Item("middle", 1); var last = Item("last", 2);
        var list = new ObservableCollection<ActionDefinition>(new[] { first, middle, last });
        var notifications = new List<NotifyCollectionChangedAction>(); list.CollectionChanged += (_, args) => notifications.Add(args.Action);
        var pinned = last with { IsFavorite = true };
        ClipboardCollection.Update(list, StudioContent.ClipboardItems(new[] { first, middle, pinned }));
        Assert.Equal(new[] { pinned, first, middle }, list);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Move, NotifyCollectionChangedAction.Replace }, notifications);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, notifications); Assert.Same(first, list[1]);
        ClipboardCollection.Update(list, new[] { middle with { Name = "Edited" }, pinned });
        Assert.Equal(new[] { middle.Id, last.Id }, list.Select(item => item.Id)); Assert.Equal("Edited", list[0].Name);
        Assert.Equal(last.Parameters, list[1].Parameters); Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, notifications);
    }
}

[Collection("MAUI controls")]
public sealed class ClipboardPinHeaderTests : IDisposable
{
    private readonly MauiTestContext host = new();
    [Fact]
    public void PinKeepsItsHitTargetWhileHeaderTitleUsesTheCorrectContrastAndFlexibleWidth()
    {
        var pin = new ClipboardIconButton(StudioSymbol.Pin, "Pin"); var header = new ClipboardHeaderView(pin); host.Attach(header);
        var layout = Assert.IsType<Grid>(header.Content); var title = Assert.IsType<Label>(layout.Children[0]);
        Assert.Same(pin, layout.Children[1]); Assert.True(layout.ColumnDefinitions[0].Width.IsStar); Assert.True(layout.ColumnDefinitions[1].Width.IsAuto);
        Assert.Equal(48, pin.WidthRequest); Assert.Equal(2, title.MaxLines);
        foreach (var colour in new[] { "#FFFFFF", "#000000", "#00FFD2", "#007AFF" })
        {
            header.SetItem("A long item name that can wrap", colour);
            Assert.True(AppearanceColors.Contrast(header.BackgroundColor, title.TextColor) >= 3);
            Assert.Equal("A long item name that can wrap", title.Text); Assert.Same(pin, layout.Children[1]);
        }
        host.App.Resources["Accent"] = Color.FromArgb("#00FFD2"); host.App.Resources["AccentActionText"] = Colors.Black;
        header.SetItem("Default", null); Assert.Equal("#00FFD2", AppearanceColors.ToRgbHex(header.BackgroundColor)); Assert.Equal(Colors.Black, title.TextColor);
    }
    public void Dispose() => host.Dispose();
}
