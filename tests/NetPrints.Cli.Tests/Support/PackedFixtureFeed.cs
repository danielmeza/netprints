using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Support;

/// <summary>A temporary local NuGet feed holding <c>CatalogFixtureLib</c> 1.0.0, packed once per test class with <c>-p:IsPackable=true</c> (the project is not packable by default).</summary>
public sealed class PackedFixtureFeed : IAsyncLifetime
{
    public const string PackageVersion = "1.0.0";

    private readonly string _root = Directory.CreateTempSubdirectory("np-feed-").FullName;

    public string Feed => Path.Combine(_root, "feed");

    public string Packages => Path.Combine(_root, "packages");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Feed);
        ProcessResult result = await new ProcessRunner().RunAsync(
            new ProcessStartRequest(
                "dotnet",
                ["pack", CatalogFixtures.LibraryProject, "--no-build", "-nodeReuse:false", "-c", LocalSdkLayout.DetectConfiguration(), "-p:IsPackable=true", "-o", Feed, "--nologo", "-v", "q"],
                _root),
            CancellationToken.None);
        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
    }

    /// <summary>Writes the <c>NuGet.config</c> that restores from this feed only, into an isolated package cache, so nothing reaches the network or the real global cache.</summary>
    /// <param name="directory">The working directory of the tool run.</param>
    public void WriteNuGetConfig(string directory) => File.WriteAllText(
        Path.Combine(directory, "NuGet.config"),
        $"""
        <configuration>
          <packageSources>
            <clear />
            <add key="fixture" value="{Feed}" />
          </packageSources>
          <packageSourceMapping>
            <clear />
            <packageSource key="fixture">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
          <config>
            <add key="globalPackagesFolder" value="{Packages}" />
          </config>
        </configuration>
        """);

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_root, recursive: true);
        return ValueTask.CompletedTask;
    }
}
