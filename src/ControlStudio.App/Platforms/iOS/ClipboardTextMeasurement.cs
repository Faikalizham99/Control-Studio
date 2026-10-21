using CoreGraphics;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class ClipboardTextMeasurement
{
    public static bool? ExceedsLines(Label label, int lines)
    {
        if (label.Handler?.PlatformView is not UILabel native || label.Width <= 0 || !double.IsFinite(label.Width)) return null;
        // Measure a separate native label: never change the live label's line limit or its saved text.
        // Its font/attributed text include the handler's actual Dynamic Type and Unicode layout.
        using var measurement = new UILabel
        {
            Font = native.Font, Lines = 0
        };
        if (native.AttributedText is { } attributed) measurement.AttributedText = attributed;
        else measurement.Text = native.Text;
        measurement.LineBreakMode = UILineBreakMode.WordWrap;
        var insets = native is MauiLabel maui ? maui.TextInsets : UIEdgeInsets.Zero;
        var width = label.Width - (double)(insets.Left + insets.Right);
        if (width <= 0) return null;
        var constraint = new CGSize(width, nfloat.MaxValue);
        var fullHeight = measurement.SizeThatFits(constraint).Height;
        measurement.Lines = lines; measurement.LineBreakMode = UILineBreakMode.TailTruncation;
        var collapsedHeight = measurement.SizeThatFits(constraint).Height;
        return fullHeight > collapsedHeight + .5;
    }
}
