using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Accessible, single-tap position adjustments in the keyboard's inverted T layout.</summary>
internal sealed class WidgetLayerNudgeControls : ContentView
{
    public event Action<WidgetMoveDirection>? MoveRequested;
    public event Action<View>? PositionChanging;

    public WidgetLayerNudgeControls(string prefix)
    {
        AutomationId = prefix;
        var keys = new Grid
        {
            HorizontalOptions = LayoutOptions.Center, RowSpacing = 8, ColumnSpacing = 8,
            RowDefinitions = { new(48), new(48) },
            ColumnDefinitions = { new(48), new(48), new(48) }
        };
        void Add(WidgetMoveDirection direction, StudioSymbol symbol, int column, int row)
        {
            var key = new ClipboardEditorActionButton("", ClipboardEditorControls.Icon(symbol, size: 22), prefix + direction)
            {
                WidthRequest = 48, HeightRequest = 48, MinimumWidthRequest = 48, MinimumHeightRequest = 48
            };
            key.Describe("Move selected layer " + direction.ToString().ToLowerInvariant() + " by 1 point");
            key.Clicked += (_, _) =>
            {
                if (!IsEnabled) return;
                PositionChanging?.Invoke(key); MoveRequested?.Invoke(direction);
            };
            keys.Add(key, column, row);
        }
        Add(WidgetMoveDirection.Up, StudioSymbol.ArrowUp, 1, 0);
        Add(WidgetMoveDirection.Left, StudioSymbol.ArrowLeft, 0, 1);
        Add(WidgetMoveDirection.Down, StudioSymbol.ArrowDown, 1, 1);
        Add(WidgetMoveDirection.Right, StudioSymbol.ArrowRight, 2, 1);
        var help = SettingsControls.Text("Move · 1 pt per tap", 12, secondary: true);
        help.HorizontalTextAlignment = TextAlignment.Center;
        Content = new VerticalStackLayout { Spacing = 8, Children = { keys, help } };
    }
}
