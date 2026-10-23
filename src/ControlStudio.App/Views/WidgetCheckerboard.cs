using Microsoft.Maui.ApplicationModel;

namespace ControlStudio.App.Views;

/// <summary>Editor-only transparency guide. Never exported into the widget image.</summary>
internal sealed class WidgetCheckerboard : GraphicsView, IDrawable
{
    public WidgetCheckerboard()
    {
        Drawable = this; InputTransparent = true;
        AutomationProperties.SetIsInAccessibleTree(this, false);
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#EBEEF2"), Color.FromArgb("#172128"));
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(BackgroundColor)) Invalidate();
    }
    public void Draw(ICanvas canvas, RectF bounds)
    {
        canvas.FillColor = BackgroundColor; canvas.FillRectangle(bounds);
        canvas.FillColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#26323A") : Color.FromArgb("#D8DFE7");
        for (var y = 0; y < bounds.Height; y += 12)
            for (var x = 0; x < bounds.Width; x += 12)
                if ((x / 12 + y / 12) % 2 == 0) canvas.FillRectangle(x, y, 12, 12);
    }
}
