using System.Globalization;
using System.Text;

namespace ControlStudio.Core.Widgets;

public sealed record WidgetTextOptions(string Content = "Your text",
    WidgetClockFont Font = WidgetClockFont.Rounded, string ColourHex = "#FFFFFF", double FontScale = .72,
    WidgetClockAlignment Alignment = WidgetClockAlignment.Centre, WidgetClockWeight? Weight = null);

public static class WidgetTexts
{
    public const int MaximumLength = 500;
    public static WidgetLayer New(string name = "Text 1", string colourHex = "#FFFFFF") => new(Guid.NewGuid(), name, "", 1, 1,
        new(.05, .35, .9, .3), Kind: WidgetLayerKind.Text, Text: new(ColourHex: colourHex));
    public static string NormalizeContent(string? value)
    {
        var lineFeed = (value ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var builder = new StringBuilder();
        foreach (var rune in lineFeed.EnumerateRunes())
            if (!Rune.IsControl(rune) || rune.Value is '\n' or '\t') builder.Append(rune.ToString());
        var text = builder.ToString();
        if (text.Length <= MaximumLength) return text;
        var boundary = StringInfo.ParseCombiningCharacters(text).LastOrDefault(start => start <= MaximumLength);
        return text[..boundary];
    }
    public static void Validate(WidgetTextOptions text)
    {
        if (text.Content is null || text.Content != NormalizeContent(text.Content))
            throw new IOException($"Use up to {MaximumLength} characters of valid text.");
        try { WidgetClocks.Validate(Style(text)); }
        catch (IOException ex) { throw new IOException("Choose valid text settings before saving.", ex); }
    }
    public static WidgetClockOptions Style(WidgetTextOptions text) => new(Font: text.Font, ColourHex: text.ColourHex,
        FontScale: text.FontScale, Alignment: text.Alignment, Weight: text.Weight);
    public static WidgetTextOptions WithStyle(WidgetTextOptions text, WidgetClockOptions style) => text with
    { Font = style.Font, ColourHex = style.ColourHex, FontScale = style.FontScale, Alignment = style.Alignment, Weight = style.Weight };
}
