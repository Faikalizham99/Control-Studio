using System.ComponentModel;
using CoreGraphics;
using ControlStudio.App.Views;
using Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using SafariServices;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class ClipboardLinkLabelHandler : LabelHandler
{
    private UIContextMenuInteraction? interaction;
    private MenuDelegate? menu;
    private ClipboardLinkLabel? link;
    private UIAccessibilityCustomAction[] accessibilityActions = [];

    protected override void ConnectHandler(MauiLabel platformView)
    {
        base.ConnectHandler(platformView);
        platformView.UserInteractionEnabled = true;
        platformView.AccessibilityTraits |= UIAccessibilityTrait.Link;
        link = (ClipboardLinkLabel)VirtualView;
        link.PropertyChanged += Changed; link.Unloaded += Unloaded;
        menu = new MenuDelegate(this);
        interaction = new UIContextMenuInteraction(menu); platformView.AddInteraction(interaction);
        accessibilityActions =
        [
            AccessibilityAction("Open in browser", context => context.OpenAsync()),
            AccessibilityAction("Copy link", context => context.CopyAsync()),
            AccessibilityAction("Share", context => context.ShareAsync())
        ];
        platformView.AccessibilityCustomActions = accessibilityActions;
    }

    protected override void DisconnectHandler(MauiLabel platformView)
    {
        if (link is not null) { link.PropertyChanged -= Changed; link.Unloaded -= Unloaded; }
        platformView.AccessibilityCustomActions = null;
        foreach (var action in accessibilityActions) action.Dispose(); accessibilityActions = [];
        CancelMenu();
        if (interaction is not null) { platformView.RemoveInteraction(interaction); interaction.Dispose(); interaction = null; }
        menu?.Dispose(); menu = null; link = null;
        base.DisconnectHandler(platformView);
    }

    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ClipboardLinkLabel.Item)) PlatformView.AccessibilityTraits |= UIAccessibilityTrait.Link;
        if (args.PropertyName == nameof(ClipboardLinkLabel.Item) || (args.PropertyName == nameof(ClipboardLinkLabel.IsEnabled) && link?.IsEnabled != true)) CancelMenu();
    }
    private UIAccessibilityCustomAction AccessibilityAction(string title, Func<ClipboardLinkContext, Task> action) => new(title, (Func<UIAccessibilityCustomAction, bool>)(_ =>
    {
        if (link?.CaptureContext() is not { } context) return false;
        Run(() => action(context)); return true;
    }));
    private void Unloaded(object? sender, EventArgs args) => CancelMenu();
    private void CancelMenu() { interaction?.DismissMenu(); menu?.ReleasePreview(); }
    private async void Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            MauiContext?.Services.GetService<ILogger<ClipboardLinkLabelHandler>>()?.LogWarning("Link interaction failed. ErrorType={ErrorType}", ex.GetType().Name);
        }
    }

    private sealed class MenuDelegate(ClipboardLinkLabelHandler owner) : UIContextMenuInteractionDelegate
    {
        private SFSafariViewController? preview;
        private ClipboardLinkContext? current;

        public override UIContextMenuConfiguration? GetConfigurationForMenu(UIContextMenuInteraction interaction, CGPoint location)
        {
            current = owner.link?.CaptureContext();
            if (current is not { } context) return null;
            return UIContextMenuConfiguration.Create(null,
                () => CreatePreview(context),
                _ => UIMenu.Create(context.Url.Host, new UIMenuElement[]
                {
                    UIAction.Create("Copy link", UIImage.GetSystemImage("doc.on.doc"), null, _ => owner.Run(context.CopyAsync)),
                    UIAction.Create("Open in browser", UIImage.GetSystemImage("safari"), null, _ => owner.Run(context.OpenAsync)),
                    UIAction.Create("Share", UIImage.GetSystemImage("square.and.arrow.up"), null, _ => owner.Run(context.ShareAsync))
                }));
        }

        private UIViewController? CreatePreview(ClipboardLinkContext context)
        {
            if (!context.IsCurrent || owner.PlatformView.Window is null) return null;
            // This is the only preview allocation. A library row itself performs no network requests.
            try
            {
                preview?.Dispose();
                using var url = new NSUrl(context.Url.AbsoluteUri);
                using var configuration = new SFSafariViewControllerConfiguration { EntersReaderIfAvailable = false, BarCollapsingEnabled = true };
                preview = new SFSafariViewController(url, configuration);
                var bounds = owner.PlatformView.Window?.Bounds ?? new CGRect(0, 0, 360, 760);
                preview.PreferredContentSize = new CGSize(Math.Clamp((double)bounds.Width - 40, 240, 460), Math.Clamp((double)bounds.Height * .5, 280, 520));
                return preview;
            }
            catch { preview?.Dispose(); preview = null; return null; } // Keep menu actions available when rich preview creation fails.
        }

        public override void WillPerformPreviewAction(UIContextMenuInteraction interaction, UIContextMenuConfiguration configuration, IUIContextMenuInteractionCommitAnimating animator)
        {
            if (current is { } context) animator.AddCompletion(() => owner.Run(context.OpenAsync));
        }

        public override void WillEnd(UIContextMenuInteraction interaction, UIContextMenuConfiguration configuration, IUIContextMenuInteractionAnimating? animator)
        {
            var previous = preview; preview = null; current = null;
            if (animator is null) previous?.Dispose(); else animator.AddCompletion(() => previous?.Dispose());
        }

        public void ReleasePreview() { current = null; preview?.Dispose(); preview = null; }
        protected override void Dispose(bool disposing) { if (disposing) ReleasePreview(); base.Dispose(disposing); }
    }
}
