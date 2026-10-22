using ControlStudio.App.Views;
using ControlStudio.Core.Actions;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class ClipboardArrangeViewHandler : ViewHandler<IView, UITableView>
{
    private ArrangementSource? source;
    public ClipboardArrangeViewHandler() : base(ViewMapper) { }
    protected override UITableView CreatePlatformView() => new(CoreGraphics.CGRect.Empty, UITableViewStyle.InsetGrouped)
    {
        AllowsSelection = false, AllowsSelectionDuringEditing = false,
        BackgroundColor = UIColor.SystemGroupedBackground, EstimatedRowHeight = 64, RowHeight = UITableView.AutomaticDimension,
        AccessibilityIdentifier = "ClipboardNativeArrangeList"
    };

    protected override void ConnectHandler(UITableView platformView)
    {
        base.ConnectHandler(platformView);
        source = new ArrangementSource(((ClipboardArrangeView)VirtualView).Arrangement);
        platformView.Source = source; platformView.SetEditing(true, false);
    }
    protected override void DisconnectHandler(UITableView platformView)
    {
        platformView.SetEditing(false, false); platformView.Source = null;
        source?.Dispose(); source = null;
        base.DisconnectHandler(platformView);
    }

    private sealed class ArrangementSource(ClipboardArrangement draft) : UITableViewSource
    {
        private bool released;
        public override nint NumberOfSections(UITableView tableView) => 2;
        public override nint RowsInSection(UITableView tableView, nint section) => draft.Group((int)section).Count;
        public override string? TitleForHeader(UITableView tableView, nint section) => draft.Group((int)section).Count == 0 ? null : section == 0 ? "Pinned" : "Other items";
        public override string? TitleForFooter(UITableView tableView, nint section) => draft.Group((int)section).Count == 1 ? "Add another item to this group to arrange it." : null;
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath) => true;
        public override bool CanMoveRow(UITableView tableView, NSIndexPath indexPath) => draft.Group((int)indexPath.Section).Count > 1;
        public override UITableViewCellEditingStyle EditingStyleForRow(UITableView tableView, NSIndexPath indexPath) => UITableViewCellEditingStyle.None;
        public override bool ShouldIndentWhileEditing(UITableView tableView, NSIndexPath indexPath) => false;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell("ClipboardArrangeRow") as ArrangeCell ?? new ArrangeCell();
            var item = draft.Group((int)indexPath.Section)[(int)indexPath.Row];
            var weakSource = new WeakReference<ArrangementSource>(this);
            var weakTable = new WeakReference<UITableView>(tableView);
            cell.SetItem(item, offset => weakSource.TryGetTarget(out var current) && weakTable.TryGetTarget(out var table) && current.MoveAccessible(table, item.Id, offset));
            cell.ShowsReorderControl = CanMoveRow(tableView, indexPath);
            return cell;
        }

        public override NSIndexPath CustomizeMoveTarget(UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath proposedIndexPath)
        {
            var count = draft.Group((int)sourceIndexPath.Section).Count;
            var row = proposedIndexPath.Section == sourceIndexPath.Section ? (int)proposedIndexPath.Row : proposedIndexPath.Section < sourceIndexPath.Section ? 0 : count - 1;
            return NSIndexPath.FromRowSection(Math.Clamp(row, 0, Math.Max(0, count - 1)), sourceIndexPath.Section);
        }

        public override void MoveRow(UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
        {
            if (released || (sourceIndexPath.Section == destinationIndexPath.Section && sourceIndexPath.Row == destinationIndexPath.Row)) return;
            if (sourceIndexPath.Section != destinationIndexPath.Section ||
                !draft.Move((int)sourceIndexPath.Section, (int)sourceIndexPath.Row, (int)destinationIndexPath.Row)) tableView.ReloadData();
        }

        private bool MoveAccessible(UITableView table, Guid id, int offset)
        {
            if (released || !table.UserInteractionEnabled) return false;
            for (var section = 0; section < 2; section++)
            {
                var group = draft.Group(section); var from = -1;
                for (var row = 0; row < group.Count; row++) if (group[row].Id == id) { from = row; break; }
                if (from < 0 || !draft.Move(section, from, from + offset)) continue;
                using var sourcePath = NSIndexPath.FromRowSection(from, section);
                using var targetPath = NSIndexPath.FromRowSection(from + offset, section);
                if (UIAccessibility.IsReduceMotionEnabled) UIView.PerformWithoutAnimation(() => table.MoveRow(sourcePath, targetPath));
                else table.MoveRow(sourcePath, targetPath);
                SemanticScreenReader.Default.Announce($"Moved {group[from + offset].Name} to position {from + offset + 1} in {(section == 0 ? "Pinned" : "Other items")}.");
                return true;
            }
            return false;
        }
        protected override void Dispose(bool disposing) { released = true; base.Dispose(disposing); }
    }

    private sealed class ArrangeCell() : UITableViewCell(UITableViewCellStyle.Default, "ClipboardArrangeRow")
    {
        private UIAccessibilityCustomAction[] actions = [];
        private int revision;
        public void SetItem(ActionDefinition item, Func<int, bool> move)
        {
            ReleaseActions();
            using var content = DefaultContentConfiguration;
            content.Text = item.Name;
            using var text = content.TextProperties;
            text.Font = UIFont.PreferredBody!; text.Color = UIColor.Label;
            text.NumberOfLines = 1; text.LineBreakMode = UILineBreakMode.TailTruncation;
            text.AdjustsFontForContentSizeCategory = true;
            ContentConfiguration = content;
            BackgroundColor = UIColor.SecondarySystemGroupedBackground;
            SelectionStyle = UITableViewCellSelectionStyle.None;
            IsAccessibilityElement = true; AccessibilityLabel = item.Name + (item.IsFavorite ? ", pinned" : "");
            AccessibilityHint = "Drag the reorder handle, or use Move up and Move down actions.";
            var capturedRevision = revision; var weakCell = new WeakReference<ArrangeCell>(this);
            bool Move(int offset) => weakCell.TryGetTarget(out var cell) && cell.revision == capturedRevision && move(offset);
            actions =
            [
                new UIAccessibilityCustomAction("Move up", (Func<UIAccessibilityCustomAction, bool>)(_ => Move(-1))),
                new UIAccessibilityCustomAction("Move down", (Func<UIAccessibilityCustomAction, bool>)(_ => Move(1)))
            ];
            AccessibilityCustomActions = actions;
        }
        private void ReleaseActions() { revision++; AccessibilityCustomActions = null; foreach (var action in actions) action.Dispose(); actions = []; }
        public override void PrepareForReuse() { ReleaseActions(); base.PrepareForReuse(); }
        protected override void Dispose(bool disposing) { if (disposing) ReleaseActions(); base.Dispose(disposing); }
    }
}
