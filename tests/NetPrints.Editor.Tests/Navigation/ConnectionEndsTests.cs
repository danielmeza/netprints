using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;

namespace NetPrints.Editor.Tests.Navigation;

public class ConnectionEndsTests
{
    private static readonly GraphPoint Source = new(0, 0);
    private static readonly GraphPoint Target = new(100, 40);

    [Fact]
    public void AClickNearTheSourceGoesToTheTarget() =>
        Assert.Equal(ConnectionEnd.Target, ConnectionEnds.Farther(new GraphPoint(10, 5), Source, Target));

    [Fact]
    public void AClickNearTheTargetGoesToTheSource() =>
        Assert.Equal(ConnectionEnd.Source, ConnectionEnds.Farther(new GraphPoint(90, 30), Source, Target));

    [Fact]
    public void AClickMidwayGoesToTheTarget() =>
        Assert.Equal(ConnectionEnd.Target, ConnectionEnds.Farther(new GraphPoint(50, 20), Source, Target));

    [Fact]
    public void DistanceIsMeasuredInBothAxes() =>
        Assert.Equal(ConnectionEnd.Source, ConnectionEnds.Farther(new GraphPoint(60, 300), Source, new GraphPoint(100, 290)));
}
