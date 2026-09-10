using Avalonia.Media;

namespace Jadwal.UI.Themes;

public static class FatimidPalette
{
    public static readonly Color Emerald = Color.Parse("#147A5C");
    public static readonly Color EmeraldSoft = Color.Parse("#20147A5C");
    public static readonly Color Bronze = Color.Parse("#C29447");
    public static readonly Color BronzeSoft = Color.Parse("#25C29447");
    public static readonly Color Gold = Color.Parse("#E0B540");
    public static readonly Color GoldSoft = Color.Parse("#25E0B540");
    public static readonly Color CyanBlue = Color.Parse("#1E87C7");
    public static readonly Color DeepNavy = Color.Parse("#145F9C");
    public static readonly Color CardBorder = Color.Parse("#30C29447");

    public static readonly IBrush EmeraldBrush = new SolidColorBrush(Emerald);
    public static readonly IBrush BronzeBrush = new SolidColorBrush(Bronze);
    public static readonly IBrush GoldBrush = new SolidColorBrush(Gold);
    public static readonly IBrush CyanBlueBrush = new SolidColorBrush(CyanBlue);
    public static readonly IBrush DeepNavyBrush = new SolidColorBrush(DeepNavy);

    public static readonly LinearGradientBrush BannerGradient = new()
    {
        StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
        EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new(Color.Parse("#1F8AC9"), 0.0),
            new(Color.Parse("#145B96"), 1.0)
        }
    };
}
