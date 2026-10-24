using ControlStudio.Core.Widgets;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal sealed class WidgetBlendThumbnail : ContentView
{
    private readonly Image image = new() { Aspect = Aspect.AspectFit, InputTransparent = true };
    private readonly Func<WidgetBlendMode, ImageSource?> source;
    public WidgetBlendMode Mode { get; }
    public bool IsPreviewActive { get; private set; }
    public WidgetBlendThumbnail(WidgetBlendMode mode, Func<WidgetBlendMode, ImageSource?>? source = null)
    {
        Mode = mode;
#if IOS
        this.source = source ?? Platforms.iOS.AppleWidgetBlendSamples.Source;
#else
        this.source = source ?? (_ => null);
#endif
        WidthRequest = 58; HeightRequest = 44; InputTransparent = true;
        // Neutral fallback while the illustrative native image loads, or if it fails.
        var fallback = new StudioGlyph(StudioSymbol.Blend, false) { Tint = Color.FromArgb("#52637D"), WidthRequest = 24, HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        Content = new Border { BackgroundColor = Color.FromArgb("#EEF1F6"), Stroke = Color.FromArgb("#C8D1DE"), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }, Content = new Grid { Children = { fallback, image } } };
        AutomationProperties.SetIsInAccessibleTree(this, false); AutomationProperties.SetIsInAccessibleTree(image, false);
    }
    public void SetActive(bool value)
    {
        if (IsPreviewActive == value) return;
        IsPreviewActive = value; image.Source = value ? source(Mode) : null;
    }
}

/// <summary>Local vector symbols for Normal, Darken, Lighten, Contrast and Effects.</summary>
internal sealed class WidgetBlendGroupGlyph : GraphicsView, IDrawable
{
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(WidgetBlendGroupGlyph), Colors.Gray,
        propertyChanged: (view, _, _) => ((WidgetBlendGroupGlyph)view).Invalidate());
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public int Group { get; }
    public WidgetBlendGroupGlyph(int group)
    {
        if (group < 0 || group >= WidgetBlending.Groups.Count) throw new ArgumentOutOfRangeException(nameof(group));
        Group = group; Drawable = this; WidthRequest = HeightRequest = 24; InputTransparent = true;
        SetDynamicResource(TintProperty, "AccentText"); AutomationProperties.SetIsInAccessibleTree(this, false);
    }
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState(); canvas.Scale((float)Width / 24, (float)Height / 24); canvas.StrokeColor = Tint; canvas.StrokeSize = 1.6f;
        if (Group == 0)
        { canvas.FillColor = Tint.WithAlpha(.25f); canvas.FillRoundedRectangle(3, 3, 13, 13, 3); canvas.DrawRoundedRectangle(3, 3, 13, 13, 3); canvas.DrawRoundedRectangle(8, 8, 13, 13, 3); }
        else if (Group is 1 or 2)
        {
            canvas.FillColor = Tint.WithAlpha(Group == 1 ? .8f : .16f); canvas.FillCircle(8.5f, 12, 6.3f);
            canvas.FillColor = Tint.WithAlpha(Group == 1 ? .45f : .35f); canvas.FillCircle(15.5f, 12, 6.3f);
            canvas.DrawCircle(8.5f, 12, 6.3f); canvas.DrawCircle(15.5f, 12, 6.3f);
            if (Group == 2) { canvas.DrawLine(12, 1, 12, 4); canvas.DrawLine(12, 20, 12, 23); }
        }
        else if (Group == 3)
        {
            canvas.SaveState(); canvas.ClipRectangle(3, 3, 9, 18); canvas.FillColor = Tint; canvas.FillCircle(12, 12, 9); canvas.RestoreState();
            canvas.DrawCircle(12, 12, 9); canvas.DrawLine(12, 3, 12, 21);
        }
        else
        {
            canvas.FillColor = Color.FromArgb("#30A9FF"); canvas.FillCircle(8, 12, 6);
            canvas.FillColor = Color.FromArgb("#FF934D"); canvas.FillCircle(16, 12, 6);
            canvas.FillColor = Color.FromArgb("#B95CE6"); canvas.FillRoundedRectangle(10, 8, 4, 8, 2);
            canvas.DrawCircle(8, 12, 6); canvas.DrawCircle(16, 12, 6);
        }
        canvas.RestoreState();
    }
}
