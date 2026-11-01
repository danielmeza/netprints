using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Workspace.Run;
using Xunit;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>run-profiles.md §7.3: without an SDK the run port throws the same <see cref="ProjectSystemException"/> every other member does.</summary>
public sealed class NoSdkRunProjectSystemTests
{
    [Fact]
    public async Task BuildingInAConfigurationReportsNoSdk()
    {
        IRunProjectSystem projects = new NoSdkProjectSystem();

        ProjectSystemException exception = await Assert.ThrowsAsync<ProjectSystemException>(
            () => projects.BuildAsync("App.csproj", RunConfigurations.Release, TestContext.Current.CancellationToken));

        Assert.Equal(ProjectSystemException.NoSdkRegistered, exception.Code);
    }

    [Fact]
    public async Task EvaluatingPropertiesReportsNoSdk()
    {
        IRunProjectSystem projects = new NoSdkProjectSystem();

        ProjectSystemException exception = await Assert.ThrowsAsync<ProjectSystemException>(
            () => projects.EvaluatePropertiesAsync("App.csproj", RunConfigurations.Debug, ["TargetPath"], TestContext.Current.CancellationToken));

        Assert.Equal(ProjectSystemException.NoSdkRegistered, exception.Code);
    }
}
