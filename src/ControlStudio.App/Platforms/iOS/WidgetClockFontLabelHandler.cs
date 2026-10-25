using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetClockFontLabelHandler : LabelHandler
{
    private static readonly IPropertyMapper<ILabel, ILabelHandler> fontMapper = new PropertyMapper<ILabel, ILabelHandler>(LabelHandler.Mapper)
    {
        [nameof(ILabel.Font)] = MapClockFont,
        [nameof(WidgetClockFontLabel.Typeface)] = MapClockFont
    };
    public WidgetClockFontLabelHandler() : base(fontMapper) { }
    private static void MapClockFont(ILabelHandler handler, ILabel view)
    {
        LabelHandler.MapFont(handler, view);
        if (view is not WidgetClockFontLabel label || handler.PlatformView is not { } native) return;
        // Named faces use MAUI's normal font/scaling path. System designs need
        // the public native descriptor so Light/Semibold/Heavy are exact too.
        if (label.Typeface.Font is not (WidgetClockFont.System or WidgetClockFont.SfProRounded)) return;
        using var font = AppleWidgetClockFonts.Create(label.Typeface, label.FontSize);
        using var metrics = UIFontMetrics.GetMetrics(UIFontTextStyle.Body.GetConstant()!);
        using var scaled = label.FontAutoScalingEnabled ? metrics.GetScaledFont(font) : null;
        native.Font = scaled ?? font;
        native.AdjustsFontForContentSizeCategory = label.FontAutoScalingEnabled;
        native.SetNeedsLayout();
    }
}
