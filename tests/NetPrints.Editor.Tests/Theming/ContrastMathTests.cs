using Avalonia.Media;
using NetPrints.Editor.Theming;

namespace NetPrints.Editor.Tests.Theming;

public class ContrastMathTests
{
    private static readonly Color Black = Color.FromRgb(0, 0, 0);
    private static readonly Color White = Color.FromRgb(255, 255, 255);

    [Fact]
    public void BlackOnWhiteIs21To1()
    {
        Assert.Equal(21.0, ContrastMath.Ratio(Black, White), 6);
        Assert.Equal(21.0, ContrastMath.Ratio(White, Black), 6);
    }

    [Fact]
    public void AColourAgainstItselfIs1To1()
    {
        Assert.Equal(1.0, ContrastMath.Ratio(Color.FromRgb(90, 120, 200), Color.FromRgb(90, 120, 200)), 6);
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(255, 1.0)]
    public void LuminanceSpansZeroToOne(byte channel, double expected)
    {
        Assert.Equal(expected, ContrastMath.Luminance(Color.FromRgb(channel, channel, channel)), 6);
    }

    [Fact]
    public void AMidGreyHasTheWcagLuminance()
    {
        Assert.Equal(0.2159, ContrastMath.Luminance(Color.FromRgb(128, 128, 128)), 3);
    }

    [Fact]
    public void CompositeMixesByTheAlphaOfTheTopColour()
    {
        Color mixed = ContrastMath.Composite(Color.FromArgb(128, 255, 255, 255), Black);

        Assert.Equal(Color.FromRgb(128, 128, 128), mixed);
        Assert.Equal(Black, ContrastMath.Composite(Color.FromArgb(0, 255, 255, 255), Black));
        Assert.Equal(White, ContrastMath.Composite(Color.FromArgb(255, 255, 255, 255), Black));
    }

    [Fact]
    public void WithOpacityScalesTheAlphaChannel()
    {
        Color faded = ContrastMath.WithOpacity(Color.FromArgb(200, 10, 20, 30), 0.5);

        Assert.Equal(Color.FromArgb(100, 10, 20, 30), faded);
    }

    [Theory]
    [InlineData(1.14998, 1.15, false)]
    [InlineData(1.15, 1.15, true)]
    [InlineData(4.4999, 4.5, false)]
    [InlineData(2.9999, 3.0, false)]
    [InlineData(3.137, 3.0, true)]
    public void ACheckPassesOnlyWhenTheExactRatioReachesTheMinimum(double ratio, double minimum, bool passed) =>
        Assert.Equal(passed, new ContrastCheck(ContrastGates.CardGate, "a / b", ratio, minimum).Passed);
}
