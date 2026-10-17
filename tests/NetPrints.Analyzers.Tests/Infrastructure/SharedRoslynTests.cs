using NetPrints.Roslyn;
using Xunit;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>The sources in <c>src/Shared/Roslyn</c>, compiled into this project the way each analyzer assembly links them.</summary>
public class SharedRoslynTests
{
    [Fact]
    public void EquatableArraysWithEqualElementsAreEqual()
    {
        EquatableArray<string> left = new(["a", "b"]);
        EquatableArray<string> right = new(["a", "b"]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, new EquatableArray<string>(["a", "c"]));
        Assert.NotEqual(left, new EquatableArray<string>(["a"]));
    }

    [Fact]
    public void ADefaultEquatableArrayEqualsAnEmptyOne()
    {
        Assert.Equal(default, new EquatableArray<int>([]));
        Assert.Equal(0, default(EquatableArray<int>).Length);
    }

    [Fact]
    public void HelpLinksAnchorTheLowerCaseCode()
    {
        Assert.Equal("https://danielmeza.github.io/netprints/guide/diagnostics#nps001", DiagnosticHelpLinks.For("NPS001"));
    }
}
