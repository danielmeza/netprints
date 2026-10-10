using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>
/// F-02: a new project's <c>NetPrints.Sdk</c> reference must be the editor's own version, not the
/// hard-coded <c>1.0.0-dev</c> placeholder that never exists on nuget.org.
/// </summary>
public sealed class EditorSdkVersionTests : IDisposable
{
    private readonly string directory = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(directory);

    [Theory]
    [InlineData("1.2.3+abc", "1.2.3")]
    [InlineData("0.1.0-alpha.0.5+sha", "0.1.0-alpha.0.5")]
    [InlineData("1.0.0", "1.0.0")]
    public void StripBuildMetadataRemovesThePlusSuffixOnly(string version, string expected) =>
        Assert.Equal(expected, EditorSdkVersion.StripBuildMetadata(version));

    [Theory]
    [InlineData("0.2.1-alpha.0.7+abc", "0.2.0", "0.2.0")]
    [InlineData("0.2.0+abc", "0.2.0", "0.2.0")]
    [InlineData("0.3.0-rc.1+abc", "0.3.0-rc.1", "0.3.0-rc.1")]
    [InlineData("0.3.0", "0.2.0", "0.3.0")]
    [InlineData("0.2.1-alpha.0.7", null, "0.2.1-alpha.0.7")]
    [InlineData("0.2.1-alpha.0.7", "", "0.2.1-alpha.0.7")]
    [InlineData(null, "0.2.0", "0.2.0")]
    [InlineData(null, null, EditorSdkVersion.Fallback)]
    public void APrereleaseBuildWritesTheLatestPublishedVersionAndAReleaseItsOwn(string? informational, string? latestRelease, string expected) =>
        Assert.Equal(expected, EditorSdkVersion.Choose(informational, latestRelease));

    [Fact]
    public async Task CreateAsyncWritesTheEditorsOwnVersionNotThePlaceholder()
    {
        string version = EditorSdkVersion.Resolve(typeof(EditorServices).Assembly);
        var system = new MsBuildProjectSystem(new ProjectSystemOptions([], version), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        string csprojPath = await system.CreateAsync(directory, "SdkVersionProbe", DefaultProjectProfile.Instance,
            "SdkVersionProbe", TestContext.Current.CancellationToken);

        string content = await File.ReadAllTextAsync(csprojPath, TestContext.Current.CancellationToken);
        Assert.Contains($"Version=\"{version}\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("1.0.0-dev", content, StringComparison.Ordinal);
    }
}
