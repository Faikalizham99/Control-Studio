using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetClockFontLabelHandler : LabelHandler
{
    private bool patternApplied;
    private static readonly IPropertyMapper<ILabel, ILabelHandler> fontMapper = new PropertyMapper<ILabel, ILabelHandler>(LabelHandler.Mapper)
    {
        [nameof(ILabel.Font)] = MapClockFont,
        [nameof(WidgetClockFontLabel.Typeface)] = MapClockFont,
        [nameof(WidgetClockFontLabel.PaintGradient)] = MapGradient,
        [nameof(ILabel.Text)] = (handler, view) => { LabelHandler.MapText(handler, view); MapGradient(handler, view); },
        [nameof(ILabel.TextColor)] = (handler, view) => { LabelHandler.MapTextColor(handler, view); MapGradient(handler, view); },
        [nameof(ILabel.HorizontalTextAlignment)] = (handler, view) => { LabelHandler.MapHorizontalTextAlignment(handler, view); MapGradient(handler, view); },
        [nameof(ILabel.VerticalTextAlignment)] = (handler, view) => { LabelHandler.MapVerticalTextAlignment(handler, view); MapGradient(handler, view); }
    };
    public WidgetClockFontLabelHandler() : base(fontMapper) { }
    protected override void ConnectHandler(Microsoft.Maui.Platform.MauiLabel platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView is WidgetClockFontLabel label) label.SizeChanged += SizeChanged;
    }
    protected override void DisconnectHandler(Microsoft.Maui.Platform.MauiLabel platformView)
    {
        if (VirtualView is WidgetClockFontLabel label) label.SizeChanged -= SizeChanged;
        if (patternApplied) SetColour(platformView, UIColor.Black);
        patternApplied = false;
        base.DisconnectHandler(platformView);
    }
    private void SizeChanged(object? sender, EventArgs args) => MapGradient(this, VirtualView);
    private static void MapClockFont(ILabelHandler handler, ILabel view)
    {
        LabelHandler.MapFont(handler, view);
        if (view is not WidgetClockFontLabel label || handler.PlatformView is not { } native) return;
        // Named faces use MAUI's normal font/scaling path. System designs need
        // the public native descriptor so Light/Semibold/Heavy are exact too.
        if (label.Typeface.Font is WidgetClockFont.System or WidgetClockFont.SfProRounded)
        {
            using var font = AppleWidgetClockFonts.Create(label.Typeface, label.FontSize);
            using var metrics = UIFontMetrics.GetMetrics(UIFontTextStyle.Body.GetConstant()!);
            using var scaled = label.FontAutoScalingEnabled ? metrics.GetScaledFont(font) : null;
            native.Font = scaled ?? font;
            native.AdjustsFontForContentSizeCategory = label.FontAutoScalingEnabled;
        }
        MapGradient(handler, view);
        native.SetNeedsLayout();
    }
    private static void MapGradient(ILabelHandler handler, ILabel view)
    {
        if (view is not WidgetClockFontLabel label || handler.PlatformView is not { } native) return;
        if (!label.PaintGradient || label.Typeface.Gradient is not { Enabled: true } gradient)
        {
            LabelHandler.MapTextColor(handler, view);
            if (handler is WidgetClockFontLabelHandler { patternApplied: true } previous)
            { SetColour(native, native.TextColor ?? UIColor.Label); previous.patternApplied = false; }
            return;
        }
        var bounds = new CoreGraphics.CGRect(0, 0, label.Width, label.Height);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = (nfloat)Math.Min(1, 1024 / Math.Max(bounds.Width, bounds.Height)); format.Opaque = false;
        format.PreferredRange = UIGraphicsImageRendererFormatRange.Standard;
        using var renderer = new UIGraphicsImageRenderer(bounds.Size, format);
        var text = native.TextRectForBounds(bounds, native.Lines);
        using var image = renderer.CreateImage(context => AppleWidgetTextGradient.Draw(context.CGContext, text, gradient));
        using var colour = UIColor.FromPatternImage(image); SetColour(native, colour);
        if (handler is WidgetClockFontLabelHandler current) current.patternApplied = true;
    }
    private static void SetColour(UILabel native, UIColor colour)
    {
        native.TextColor = colour;
        if (native.AttributedText is not { } attributed) return;
        using var copy = new NSMutableAttributedString(attributed);
        copy.AddAttribute(UIStringAttributeKey.ForegroundColor, colour, new NSRange(0, copy.Length)); native.AttributedText = copy;
    }
}
