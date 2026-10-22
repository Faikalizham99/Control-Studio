using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

internal sealed class ClipboardLinkContext(ActionDefinition item, Uri url, Func<bool> isCurrent,
    Func<ActionDefinition, Task> open, Func<ActionDefinition, Task> copy, Func<ActionDefinition, Task> share)
{
    public Uri Url { get; } = url;
    public bool IsCurrent => isCurrent();
    public Task OpenAsync() => isCurrent() ? open(item) : Task.CompletedTask;
    public Task CopyAsync() => isCurrent() ? copy(item) : Task.CompletedTask;
    public Task ShareAsync() => isCurrent() ? share(item) : Task.CompletedTask;
}

/// <summary>Display-only URL label. The iOS handler supplies a lazy native context preview.</summary>
internal sealed class ClipboardLinkLabel : Label
{
    public static readonly BindableProperty ItemProperty = BindableProperty.Create(nameof(Item), typeof(ActionDefinition), typeof(ClipboardLinkLabel), null,
        propertyChanged: (view, _, _) => ((ClipboardLinkLabel)view).RefreshItem());
    private readonly Func<ActionDefinition, Task> open, copy, share;
    private int revision;
    public ActionDefinition? Item { get => (ActionDefinition?)GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    public ClipboardLinkLabel(Func<ActionDefinition, Task> open, Func<ActionDefinition, Task> copy, Func<ActionDefinition, Task> share)
    {
        this.open = open; this.copy = copy; this.share = share;
        FontSize = 14; TextDecorations = TextDecorations.Underline; MinimumHeightRequest = 44;
        MaxLines = 3; LineBreakMode = LineBreakMode.TailTruncation;
        AutomationId = "ClipboardSavedLink";
        this.SetAppThemeColor(TextColorProperty, Color.FromArgb("#0062D1"), Color.FromArgb("#64ACFF"));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => { if (CaptureContext() is { } context) await context.OpenAsync(); };
        GestureRecognizers.Add(tap);
        SemanticProperties.SetHint(this, "Tap to open. Touch and hold for a webpage preview, Copy link, Open in browser and Share.");
    }

    public ClipboardLinkContext? CaptureContext()
    {
        if (!IsEnabled || Item is not { Parameters: OpenUrlParameters link } item) return null;
        Uri url;
        try { url = ActionValidator.ValidateWebUrl(link.Url); }
        catch (ActionValidationException) { return null; }
        var capturedRevision = revision;
        return new ClipboardLinkContext(item, url, () => IsEnabled && revision == capturedRevision, open, copy, share);
    }

    private void RefreshItem()
    {
        revision++;
        Text = (Item?.Parameters as OpenUrlParameters)?.Url ?? "";
        SemanticProperties.SetDescription(this, Item is null ? "Saved link" : $"{Item.Name}. {Text}");
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        // A concealed/disabled link must not revive an earlier menu when revealed again.
        if (propertyName == nameof(IsEnabled) && !IsEnabled) revision++;
        base.OnPropertyChanged(propertyName);
    }
}
