namespace ControlStudio.App.Views;

/// <summary>History actions above the canvas, separate from the selected layer's tools.</summary>
internal sealed class WidgetHistoryControls : ContentView
{
    private readonly HistoryButton undo = new(StudioSymbol.Undo, "Undo", "WidgetUndo");
    private readonly HistoryButton redo = new(StudioSymbol.Redo, "Redo", "WidgetRedo");
    public event EventHandler? UndoRequested, RedoRequested;
    public WidgetHistoryControls()
    {
        AutomationId = "WidgetHistoryControls";
        Content = new HorizontalStackLayout { Spacing = ActionSpacing.Gap, Children = { undo, redo } };
        undo.Clicked += (_, _) => UndoRequested?.Invoke(this, EventArgs.Empty);
        redo.Clicked += (_, _) => RedoRequested?.Invoke(this, EventArgs.Empty);
        Update(false, false, 0, 0);
    }
    public void Update(bool canUndo, bool canRedo, int undoCount, int redoCount)
    { undo.Update(canUndo, undoCount); redo.Update(canRedo, redoCount); }
    public void ResetPressedState() { undo.Scale = redo.Scale = 1; }

    private sealed class HistoryButton : ContentView
    {
        private readonly StudioGlyph icon;
        private readonly Button tap;
        private readonly string action;
        private bool? lastEnabled;
        private int lastCount = -1;
        public event EventHandler? Clicked;
        public HistoryButton(StudioSymbol symbol, string action, string id)
        {
            this.action = action;
            WidthRequest = HeightRequest = MinimumWidthRequest = MinimumHeightRequest = 48;
            icon = new StudioGlyph(symbol, false) { WidthRequest = 23, HeightRequest = 23, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var surface = SettingsControls.Rounded(icon, 15, 1); surface.InputTransparent = true;
            surface.SetAppTheme<Brush>(BackgroundProperty, new SolidColorBrush(Color.FromArgb("#F2F4F9")), new SolidColorBrush(Color.FromArgb("#33353C")));
            tap = SettingsControls.TapTarget(action, id); tap.CornerRadius = 15;
            tap.Clicked += (_, args) => { if (IsEnabled) Clicked?.Invoke(this, args); };
            tap.Pressed += (_, _) => { if (IsEnabled) Scale = .95; }; tap.Released += (_, _) => Scale = 1;
            Content = new Grid { Children = { tap, surface } };
        }
        public void Update(bool enabled, int count)
        {
            if (lastEnabled == enabled && lastCount == count) return;
            if (lastEnabled != enabled)
            {
                IsEnabled = enabled; Scale = 1;
                if (enabled) icon.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
                else
                {
                    icon.RemoveDynamicResource(StudioGlyph.TintProperty);
                    icon.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#687080"), Color.FromArgb("#A0A8B8"));
                }
            }
            lastEnabled = enabled; lastCount = count;
            SemanticProperties.SetDescription(tap, action + " widget edit");
            SemanticProperties.SetHint(tap, enabled ? $"{count} {(count == 1 ? "step" : "steps")} available in this editing session." : count == 0 ? "No edits to " + action.ToLowerInvariant() + "." : "Available after the current interaction finishes.");
        }
    }
}
