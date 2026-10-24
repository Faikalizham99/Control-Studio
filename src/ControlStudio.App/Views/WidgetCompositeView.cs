using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Native composition surface. Editing guides remain outside the saved image.</summary>
internal sealed class WidgetCompositeView(Func<string, string?> assetPath) : View
{
    public static readonly BindableProperty DesignProperty = BindableProperty.Create(nameof(Design), typeof(WidgetDesign), typeof(WidgetCompositeView));
    public WidgetDesign? Design { get => (WidgetDesign?)GetValue(DesignProperty); set => SetValue(DesignProperty, value); }
    public Func<string, string?> AssetPath { get; } = assetPath;
    public event EventHandler<string>? RenderFailed;
    internal void Failed(string message) => RenderFailed?.Invoke(this, message);
}
