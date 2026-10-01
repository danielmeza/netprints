using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Projects;

/// <summary>The SDK props publish the package version as <c>NetPrintsSdkVersion</c> (ADR-0015 amendment); the in-repo local SDK does not.</summary>
public sealed class SdkVersionPropertyTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("netprints-sdkver-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0-alpha.0.23")]
    public async Task APackageLayoutReportsItsVersionFolder(string version)
    {
        string build = Directory.CreateDirectory(Path.Combine(_directory, "packages", "netprints.sdk", version, "build")).FullName;
        File.Copy(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.props"), Path.Combine(build, "NetPrints.Sdk.props"));
        string project = Path.Combine(_directory, "P.proj");
        await File.WriteAllTextAsync(project, $"<Project><Import Project=\"{Path.Combine(build, "NetPrints.Sdk.props")}\" /></Project>", TestContext.Current.CancellationToken);

        Assert.Equal(version, await EvaluateAsync(project));
    }

    [Fact]
    public async Task TheInRepoLocalSdkReportsNoVersion()
    {
        string project = Path.Combine(_directory, "P.proj");
        await File.WriteAllTextAsync(
            project,
            $"<Project><PropertyGroup><NetPrintsUseLocalSdk>true</NetPrintsUseLocalSdk></PropertyGroup><Import Project=\"{Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.props")}\" /></Project>",
            TestContext.Current.CancellationToken);

        Assert.Equal("", await EvaluateAsync(project));
    }

    private async Task<string> EvaluateAsync(string project)
    {
        (int exit, string output) = await ExternalProcess.RunDotnetAsync(_directory, environment: null, "msbuild", project, "-getProperty:NetPrintsSdkVersion", "-nologo");
        Assert.True(exit == 0, output);
        return output.Trim();
    }
}
