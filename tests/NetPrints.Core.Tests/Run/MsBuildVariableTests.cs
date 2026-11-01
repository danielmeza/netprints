using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Run;

/// <summary>FR-057, run-profiles.md §7.6: the record of the variables MSBuild's registration added or overwrote.</summary>
public class MsBuildVariableTests
{
    private static Dictionary<string, string> Env(params (string Name, string Value)[] entries) =>
        entries.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

    [Fact]
    public void AnAddedVariableHasNoPriorValue()
    {
        var result = MsBuildVariable.Between(Env(("A", "1")), Env(("A", "1"), ("MSBUILD_EXE_PATH", "/sdk/msbuild")), StringComparer.Ordinal);

        MsBuildVariable added = Assert.Single(result);
        Assert.Equal("MSBUILD_EXE_PATH", added.Name);
        Assert.Null(added.PriorValue);
    }

    [Fact]
    public void AnOverwrittenVariableKeepsItsEarlierValue()
    {
        var result = MsBuildVariable.Between(Env(("DOTNET_HOST_PATH", "/user/value")), Env(("DOTNET_HOST_PATH", "/sdk/dotnet")), StringComparer.Ordinal);

        MsBuildVariable changed = Assert.Single(result);
        Assert.Equal("DOTNET_HOST_PATH", changed.Name);
        Assert.Equal("/user/value", changed.PriorValue);
    }

    [Fact]
    public void AnUnchangedVariableIsIgnored()
    {
        var result = MsBuildVariable.Between(Env(("A", "1"), ("B", "2")), Env(("A", "1"), ("B", "2")), StringComparer.Ordinal);

        Assert.Empty(result);
    }

    [Fact]
    public void AVariableThatDisappearedIsNotReported()
    {
        var result = MsBuildVariable.Between(Env(("A", "1")), Env(), StringComparer.Ordinal);

        Assert.Empty(result);
    }

    [Fact]
    public void AnEmptyPriorValueIsNotAnAbsentOne()
    {
        var result = MsBuildVariable.Between(Env(("A", "")), Env(("A", "x")), StringComparer.Ordinal);

        Assert.Equal("", Assert.Single(result).PriorValue);
    }

    [Fact]
    public void NamesDifferingOnlyInCaseAreTheSameVariableWhenTheComparisonIgnoresCase()
    {
        var before = Env(("Path", "/a"));
        var after = Env(("PATH", "/a"));

        Assert.Empty(MsBuildVariable.Between(before, after, StringComparer.OrdinalIgnoreCase));
        MsBuildVariable added = Assert.Single(MsBuildVariable.Between(before, after, StringComparer.Ordinal));
        Assert.Equal("PATH", added.Name);
        Assert.Null(added.PriorValue);
    }

    [Fact]
    public void ACaseVariantWithAnotherValueIsChangedWhenTheComparisonIgnoresCase()
    {
        var result = MsBuildVariable.Between(Env(("Path", "/a")), Env(("PATH", "/b")), StringComparer.OrdinalIgnoreCase);

        MsBuildVariable changed = Assert.Single(result);
        Assert.Equal("PATH", changed.Name);
        Assert.Equal("/a", changed.PriorValue);
    }

    [Fact]
    public void SnapshotsWithCaseVariantsDoNotThrowUnderACaseInsensitiveComparison()
    {
        var before = Env(("a", "1"), ("A", "1"));

        var result = MsBuildVariable.Between(before, Env(("a", "1"), ("A", "1"), ("B", "3")), StringComparer.OrdinalIgnoreCase);

        Assert.Equal("B", Assert.Single(result).Name);
    }

    [Fact]
    public void TheDefaultComparisonFollowsTheOperatingSystem()
    {
        var before = Env(("Path", "/a"));
        var after = Env(("PATH", "/a"));

        var result = MsBuildVariable.Between(before, after);

        Assert.Equal(OperatingSystem.IsWindows() ? 0 : 1, result.Count);
    }

    [Fact]
    public void TheResultIsOrderedByName()
    {
        var result = MsBuildVariable.Between(Env(), Env(("Z", "1"), ("B", "2"), ("M", "3")), StringComparer.Ordinal);

        Assert.Equal(["B", "M", "Z"], result.Select(v => v.Name));
    }

    [Fact]
    public void EveryRecordedNameHasAValueInTheProcessEnvironment()
    {
        Assert.NotEmpty(MsBuildRegistration.Variables);
        Assert.All(MsBuildRegistration.Variables, v => Assert.False(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v.Name)), v.Name));
    }
}
