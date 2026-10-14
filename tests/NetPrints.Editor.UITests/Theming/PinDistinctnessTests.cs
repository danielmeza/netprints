using Avalonia.Headless.XUnit;
using Avalonia.Media;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Theming;

/// <summary>Every pair of pin family colours in a variant is far enough apart to tell by colour alone: CIEDE2000 at least 12 (FR-107, G-R5).</summary>
public class PinDistinctnessTests
{
    public const double MinimumDeltaE = 12.0;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(CanvasPaletteTests.Variants), MemberType = typeof(CanvasPaletteTests))]
    public void EveryPairOfPinFamiliesIsDistinguishableByColour(string variant)
    {
        var colours = PinTypeFamily.All
            .Select(family => (family.Name, Lab: ToLab(CanvasPaletteTests.ColorOf(CanvasPaletteTests.Resolve($"Pin.{family.Name}", CanvasPaletteTests.VariantOf(variant))))))
            .ToList();

        var tooClose = new List<string>();
        for (int i = 0; i < colours.Count; i++)
        {
            for (int j = i + 1; j < colours.Count; j++)
            {
                double distance = DeltaE00(colours[i].Lab, colours[j].Lab);
                if (distance < MinimumDeltaE)
                {
                    tooClose.Add($"{colours[i].Name} / {colours[j].Name}: {distance:F1}");
                }
            }
        }

        Assert.True(tooClose.Count == 0, $"pin families closer than {MinimumDeltaE} in {variant}: {string.Join("; ", tooClose)}");
    }

    private static (double L, double A, double B) ToLab(Color colour)
    {
        static double Linear(byte value)
        {
            double c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        static double F(double t) => t > 216.0 / 24389.0 ? Math.Cbrt(t) : (((24389.0 / 27.0) * t) + 16) / 116;

        double r = Linear(colour.R);
        double g = Linear(colour.G);
        double b = Linear(colour.B);
        double x = ((0.4124564 * r) + (0.3575761 * g) + (0.1804375 * b)) / 0.95047;
        double y = (0.2126729 * r) + (0.7151522 * g) + (0.0721750 * b);
        double z = ((0.0193339 * r) + (0.1191920 * g) + (0.9503041 * b)) / 1.08883;
        double fx = F(x);
        double fy = F(y);
        double fz = F(z);
        return ((116 * fy) - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double DeltaE00((double L, double A, double B) first, (double L, double A, double B) second)
    {
        double c1 = Math.Sqrt((first.A * first.A) + (first.B * first.B));
        double c2 = Math.Sqrt((second.A * second.A) + (second.B * second.B));
        double meanC = (c1 + c2) / 2;
        double meanC7 = Math.Pow(meanC, 7);
        double g = 0.5 * (1 - Math.Sqrt(meanC7 / (meanC7 + Math.Pow(25, 7))));
        double a1 = (1 + g) * first.A;
        double a2 = (1 + g) * second.A;
        double cp1 = Math.Sqrt((a1 * a1) + (first.B * first.B));
        double cp2 = Math.Sqrt((a2 * a2) + (second.B * second.B));
        double hp1 = Hue(first.B, a1);
        double hp2 = Hue(second.B, a2);

        double dL = second.L - first.L;
        double dC = cp2 - cp1;
        double dh = 0;
        if (cp1 * cp2 != 0)
        {
            dh = hp2 - hp1;
            if (dh > 180)
            {
                dh -= 360;
            }
            else if (dh < -180)
            {
                dh += 360;
            }
        }

        double dH = 2 * Math.Sqrt(cp1 * cp2) * Math.Sin(Radians(dh / 2));
        double meanL = (first.L + second.L) / 2;
        double meanCp = (cp1 + cp2) / 2;
        double meanH = hp1 + hp2;
        if (cp1 * cp2 != 0)
        {
            meanH = Math.Abs(hp1 - hp2) <= 180
                ? (hp1 + hp2) / 2
                : (hp1 + hp2 < 360 ? (hp1 + hp2 + 360) / 2 : (hp1 + hp2 - 360) / 2);
        }

        double t = 1 - (0.17 * Math.Cos(Radians(meanH - 30))) + (0.24 * Math.Cos(Radians(2 * meanH)))
            + (0.32 * Math.Cos(Radians((3 * meanH) + 6))) - (0.20 * Math.Cos(Radians((4 * meanH) - 63)));
        double deltaTheta = 30 * Math.Exp(-Math.Pow((meanH - 275) / 25, 2));
        double meanCp7 = Math.Pow(meanCp, 7);
        double rc = 2 * Math.Sqrt(meanCp7 / (meanCp7 + Math.Pow(25, 7)));
        double sl = 1 + (0.015 * Math.Pow(meanL - 50, 2) / Math.Sqrt(20 + Math.Pow(meanL - 50, 2)));
        double sc = 1 + (0.045 * meanCp);
        double sh = 1 + (0.015 * meanCp * t);
        double rt = -Math.Sin(Radians(2 * deltaTheta)) * rc;

        return Math.Sqrt(Math.Pow(dL / sl, 2) + Math.Pow(dC / sc, 2) + Math.Pow(dH / sh, 2) + (rt * (dC / sc) * (dH / sh)));
    }

    private static double Hue(double b, double a)
    {
        if (a == 0 && b == 0)
        {
            return 0;
        }

        double degrees = Math.Atan2(b, a) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }
}
