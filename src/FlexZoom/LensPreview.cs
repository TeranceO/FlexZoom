using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FlexZoom;

public sealed class LensPreview : FrameworkElement
{
    public Settings Settings { get; set; } = new();
    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var center = new Point(w / 2, h / 2);
        var background = new SolidColorBrush(Color.FromRgb(20, 25, 38));
        dc.DrawRoundedRectangle(background, null, new Rect(8, 15, Math.Max(1, w - 16), h - 30), 12, 12);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(39, 48, 67)), 1);
        for (int x = 24; x < w - 10; x += 22) dc.DrawLine(grid, new Point(x, 16), new Point(x, h - 16));
        for (int y = 28; y < h - 10; y += 22) dc.DrawLine(grid, new Point(9, y), new Point(w - 9, y));
        var text = new FormattedText("Look a little closer.", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14, Brushes.SlateGray, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
        double size = Math.Min(h - 26, 120 + (Settings.Width - 160) / 560.0 * 44);
        double lh = Settings.Shape == LensShape.Rectangle ? Math.Clamp(size * Settings.Height / Settings.Width, 65, 166) : size;
        double lw = Settings.Shape == LensShape.Rectangle ? Math.Min(w - 40, size * 1.3) : size;
        var rect = new Rect(center.X - lw / 2, center.Y - lh / 2, lw, lh);
        Geometry geometry = Settings.Shape == LensShape.Circle ? new EllipseGeometry(rect) : new RectangleGeometry(rect);
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(14, 17, 26)), new Pen(new SolidColorBrush(AccentPalette.SurfaceFor(Settings.Accent)), 10), geometry);
        dc.PushClip(geometry);
        dc.DrawRectangle(Settings.InvertColors ? Brushes.AliceBlue : new SolidColorBrush(AccentPalette.SurfaceFor(Settings.Accent)), null, rect);
        var enlarged = new FormattedText("Look a little closer.", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 14 * Settings.Zoom, Settings.InvertColors ? Brushes.Black : Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(enlarged, new Point(center.X - enlarged.Width / 2, center.Y - enlarged.Height / 2));
        dc.Pop();
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(AccentPalette.ColorFor(Settings.Accent)), 2), geometry);
        var cursor = Geometry.Parse("M0,0 L0,19 L5,14 L9,23 L13,21 L9,12 L16,12 Z");
        dc.PushTransform(new TranslateTransform(center.X + 10, center.Y + 10));
        dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1), cursor); dc.Pop();
    }
}
