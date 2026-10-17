using NetPrints.Extensibility.Nodes;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>NPXE0007: a feature is available when the C# target is new enough and its types are known.</summary>
public class CSharpFeatureTests
{
    private static readonly CSharpFeature Collections = new(new CSharpFeatureId("fx.collections"), new CSharpLanguageVersion(12, 0), ["System.Collections.Generic.List`1"]);

    private static CSharpTarget TargetOf(string langVersion) =>
        CSharpTarget.Resolve("p.csproj", [new CSharpFrameworkInput("net10.0", langVersion, null)]).Target;

    [Fact]
    public void ANewEnoughTargetMakesTheFeatureAvailable()
    {
        Availability availability = Collections.Check(TargetOf("12.0"));

        Assert.True(availability.IsAvailable);
        Assert.Null(availability.Reason);
    }

    [Fact]
    public void AnOlderTargetMakesItUnavailableAndNamesTheVersions()
    {
        Availability availability = Collections.Check(TargetOf("11.0"));

        Assert.False(availability.IsAvailable);
        Assert.Contains("12.0", availability.Reason);
        Assert.Contains("11.0", availability.Reason);
    }

    [Fact]
    public void AMissingRequiredTypeMakesItUnavailable()
    {
        Availability availability = Collections.Check(TargetOf("13.0"), typeName => typeName != "System.Collections.Generic.List`1");

        Assert.False(availability.IsAvailable);
        Assert.Contains("System.Collections.Generic.List`1", availability.Reason);
    }

    [Fact]
    public void KnownTypesAndNoPredicateAreAvailable()
    {
        Assert.True(Collections.Check(TargetOf("13.0"), _ => true).IsAvailable);
        Assert.True(Collections.Check(CSharpTarget.Unconstrained).IsAvailable);
    }
}
