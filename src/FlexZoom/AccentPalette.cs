using System.Windows.Media;

namespace FlexZoom;

internal static class AccentPalette
{
    public static Color ColorFor(AccentColor accent) => (Color)ColorConverter.ConvertFromString(accent switch
    {
        AccentColor.Blue => "#83BEFF",
        AccentColor.Teal => "#69D9BF",
        AccentColor.Rose => "#F69DBB",
        AccentColor.Amber => "#F1C475",
        _ => "#A8A0FF"
    });
    public static Color SurfaceFor(AccentColor accent)
    {
        var color = ColorFor(accent);
        return Color.FromRgb((byte)(color.R * .2 + 20), (byte)(color.G * .2 + 20), (byte)(color.B * .2 + 20));
    }
}
