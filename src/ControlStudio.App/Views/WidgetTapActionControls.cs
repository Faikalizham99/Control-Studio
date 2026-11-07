using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Raw draft fields stay outside the validated/rendered design until Save.</summary>
internal sealed class WidgetTapActionControls : ContentView
{
    private readonly WidgetSegments actions;
    private readonly ProfileNameEntry name = new() { Placeholder = "Exact Shortcut name", MaxLength = WidgetTapActions.MaximumShortcutNameLength,
        FontSize = 16, HeightRequest = 44, BackgroundColor = Colors.Transparent, AutomationId = "WidgetShortcutName",
        IsTextPredictionEnabled = false, IsSpellCheckEnabled = false, Keyboard = Keyboard.Create(KeyboardFlags.None), ReturnType = ReturnType.Done };
    private readonly VerticalStackLayout shortcut;
    private readonly Label error = SettingsControls.Text("", 13);
    private readonly Label help = SettingsControls.Text("Tap the Home Screen widget to open its editor.", 13, secondary: true);
    private readonly ClipboardEditorActionButton test = new("Test Shortcut", ClipboardEditorControls.Icon(StudioSymbol.Open), "TestWidgetShortcut", primary: true, readableDisabled: true, compact: true);
    private bool updating, available = true;
    public WidgetTapAction Action { get; private set; }
    public string ShortcutName { get; private set; } = "";
    public Func<bool>? CanInteract { get; set; }
    private bool CanUse => available && IsEnabled && (CanInteract?.Invoke() ?? true);
    public event Action<bool>? Changed;
    public event EventHandler? TestRequested;
    public event Action<View>? PositionChanging;
    public WidgetTapActionControls()
    {
        AutomationId = "WidgetTapActionSettings";
        actions = new(["Edit widget", "Run Shortcut"], "WidgetTapAction", index =>
        {
            if (!CanUse) { actions!.Update((int)Action); return; }
            PositionChanging?.Invoke(actions!); name.Unfocus(); Action = (WidgetTapAction)index;
            error.IsVisible = false; Refresh(); Changed?.Invoke(false);
        });
        name.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(name, "Shortcut name");
        SemanticProperties.SetHint(name, "Enter the exact name of an existing Shortcut in the Shortcuts app.");
        name.TextChanged += (_, args) =>
        {
            if (updating) return;
            if (!CanUse) { SetSelection(Action, ShortcutName); return; }
            ShortcutName = args.NewTextValue ?? ""; error.IsVisible = false; Refresh(); Changed?.Invoke(true);
        };
        test.Describe("Test Shortcut. Runs this Shortcut now, without saving the widget.");
        name.Completed += (_, _) => { if (CanUse) EndEditing(); };
        test.Clicked += (_, _) =>
        {
            if (!CanUse || Action != WidgetTapAction.RunShortcut) return;
            EndEditing();
            if (TryValidate()) TestRequested?.Invoke(this, EventArgs.Empty);
        };
        error.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B42318"), Color.FromArgb("#FF8F96"));
        error.AutomationId = "WidgetShortcutError"; error.IsVisible = false;
        shortcut = new VerticalStackLayout { Spacing = 10, Children = {
            SettingsControls.Text("Shortcut name", 14, bold: true), ClipboardEditorControls.Field(name, new Thickness(12, 0)),
            SettingsControls.Text("Use the exact name shown in Shortcuts. Renaming it there requires updating this field. Shortcuts may open or ask for input.", 12, secondary: true), error, test
        } };
        var title = SettingsControls.Text("Tap action", 21, bold: true); SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);
        Content = new VerticalStackLayout { Spacing = 12, Children = { title, actions, help, shortcut,
            SettingsControls.Text("Applies to the whole widget in every slot using this design. You can always edit it in Studio → My Widgets.", 12, secondary: true) } };
        Refresh();
    }
    public void SetSelection(WidgetTapAction action, string shortcutName)
    {
        updating = true;
        try { Action = action; ShortcutName = shortcutName; actions.Update((int)action); name.Text = shortcutName; error.IsVisible = false; Refresh(); }
        finally { updating = false; }
    }
    public void SetAvailable(bool value) { available = value; actions.IsEnabled = name.IsEnabled = value; Refresh(); }
    public void EndEditing() => name.Unfocus();
    private void Refresh()
    {
        shortcut.IsVisible = Action == WidgetTapAction.RunShortcut;
        help.Text = Action == WidgetTapAction.RunShortcut ? "Tap the Home Screen widget to run your saved Shortcut." : "Tap the Home Screen widget to open its editor.";
        test.IsEnabled = available && Action == WidgetTapAction.RunShortcut && WidgetTapActions.ValidName(ShortcutName);
    }
    public bool TryValidate()
    {
        try { WidgetTapActions.Validate(Action, Action == WidgetTapAction.RunShortcut ? ShortcutName : ""); error.IsVisible = false; return true; }
        catch (IOException ex) { error.Text = ex.Message; error.IsVisible = true; return false; }
    }
}
