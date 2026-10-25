using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

/// <summary>Native system designs and weights also apply to font/weight specimens.</summary>
internal sealed class WidgetClockFontLabel : Label
{
    public WidgetClockFontLabel() => UpdateFace();
    public static readonly BindableProperty TypefaceProperty = BindableProperty.Create(nameof(Typeface),
        typeof(WidgetClockOptions), typeof(WidgetClockFontLabel), new WidgetClockOptions(), propertyChanged: (view, _, _) => ((WidgetClockFontLabel)view).UpdateFace());
    public WidgetClockOptions Typeface { get => (WidgetClockOptions)GetValue(TypefaceProperty); set => SetValue(TypefaceProperty, value); }
    private void UpdateFace()
    {
        var weight = WidgetClockWeights.Resolve(Typeface);
        FontFamily = WidgetClockFonts.Family(Typeface.Font, weight);
        // Non-iOS/layout fallback. The iOS handler uses the exact native weight/design.
        FontAttributes = (Typeface.Font is WidgetClockFont.System or WidgetClockFont.SfProRounded) && weight >= WidgetClockWeight.Semibold
            ? FontAttributes.Bold : FontAttributes.None;
        InvalidateMeasure();
    }
}
