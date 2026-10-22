using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal sealed class ClipboardCopyButton : ClipboardIconButton
{
    private static readonly BindableProperty NoticeProperty = BindableProperty.Create("Notice", typeof(ClipboardCopyNotice),
        typeof(ClipboardCopyButton), null, propertyChanged: (view, _, _) => ((ClipboardCopyButton)view).Update());
    private Guid? itemId;

    public ClipboardCopyButton(ClipboardCopyFeedback feedback) : base(StudioSymbol.Clipboard, "Copy", primary: true) =>
        SetBinding(NoticeProperty, new Binding(nameof(ClipboardCopyFeedback.Notice), source: feedback));

    public void Bind(Guid? id, string name)
    {
        itemId = id; Describe("Copy", name); Update();
    }

    private void Update() => Symbol = itemId is { } id && GetValue(NoticeProperty) is ClipboardCopyNotice { IsError: false } notice && notice.ItemId == id
        ? StudioSymbol.Check : StudioSymbol.Clipboard;
}

/// <summary>A passive overlay inside the page's safe area, above its tab bar.</summary>
internal sealed class ClipboardToastView : ContentView
{
    private static readonly BindableProperty NoticeProperty = BindableProperty.Create("Notice", typeof(ClipboardCopyNotice),
        typeof(ClipboardToastView), null, propertyChanged: (view, _, _) => ((ClipboardToastView)view).Update());
    private readonly Label message = SettingsControls.Text("", 14, bold: true);
    private readonly StudioGlyph glyph = new(StudioSymbol.Check);

    public ClipboardToastView(ClipboardCopyFeedback feedback)
    {
        AutomationId = "ClipboardCopyToast"; IsVisible = false;
        InputTransparent = true; CascadeInputTransparent = true;
        VerticalOptions = LayoutOptions.End; HorizontalOptions = LayoutOptions.Center;
        MaximumWidthRequest = 420; Margin = new Thickness(20, 0, 20, 12);
        var row = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 10 };
        glyph.WidthRequest = glyph.HeightRequest = 22; glyph.VerticalOptions = LayoutOptions.Center;
        message.VerticalOptions = LayoutOptions.Center; row.Add(glyph); row.Add(message, 1);
        var surface = new Border
        {
            Content = row, Padding = new Thickness(16, 12), MinimumHeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = 20 }, StrokeThickness = 1
        };
        surface.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Color.FromArgb("#292D36"));
        surface.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#7A8496")), new SolidColorBrush(Color.FromArgb("#7F8999")));
        Content = surface;
        // Announced explicitly after copying; decorative glyph must not duplicate the message.
        SetBinding(NoticeProperty, new Binding(nameof(ClipboardCopyFeedback.Notice), source: feedback));
    }

    private void Update()
    {
        var notice = GetValue(NoticeProperty) as ClipboardCopyNotice;
        message.Text = notice?.Message ?? ""; IsVisible = notice is not null;
        glyph.Symbol = notice?.IsError == true ? StudioSymbol.Clear : StudioSymbol.Check;
        glyph.RemoveBinding(StudioGlyph.TintProperty);
        glyph.RemoveDynamicResource(StudioGlyph.TintProperty);
        if (notice?.IsError == true)
        {
            glyph.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#B42336"), Color.FromArgb("#FF8798"));
        }
        else
        {
            glyph.ClearValue(StudioGlyph.TintProperty);
            glyph.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
        }
    }
}
