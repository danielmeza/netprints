using System;
using System.IO;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using Xunit;

namespace NetPrints.Cli.Tests.Infrastructure;

public sealed class ProjectLocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("np-locator-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AnExistingProjectFileIsReturned()
    {
        string project = Touch("App/App.csproj");

        ProjectLocation location = ProjectLocator.Locate(project, EnvironmentAt(_root));

        Assert.Equal(project, location.Path);
        Assert.Null(location.Error);
    }

    [Fact]
    public void ADirectoryWithOneProjectYieldsThatProject()
    {
        string project = Touch("App/App.csproj");
        Touch("App/Other.txt");

        Assert.Equal(project, ProjectLocator.Locate(Path.GetDirectoryName(project), EnvironmentAt(_root)).Path);
    }

    [Fact]
    public void AnOmittedArgumentUsesTheCurrentDirectory()
    {
        string project = Touch("App/App.csproj");

        Assert.Equal(project, ProjectLocator.Locate(null, EnvironmentAt(Path.Combine(_root, "App"))).Path);
    }

    [Fact]
    public void ARelativeArgumentResolvesAgainstTheEnvironmentsCurrentDirectory()
    {
        string project = Touch("App/App.csproj");

        Assert.Equal(project, ProjectLocator.Locate("App", EnvironmentAt(_root)).Path);
    }

    [Fact]
    public void ADirectoryWithoutAProjectIsAnError()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Empty"));

        ProjectLocation location = ProjectLocator.Locate("Empty", EnvironmentAt(_root));

        Assert.Null(location.Path);
        Assert.Contains("no .csproj", location.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ADirectoryWithTwoProjectsIsAnErrorNamingBoth()
    {
        Touch("Two/A.csproj");
        Touch("Two/B.csproj");

        ProjectLocation location = ProjectLocator.Locate("Two", EnvironmentAt(_root));

        Assert.Null(location.Path);
        Assert.Contains("A.csproj", location.Error, StringComparison.Ordinal);
        Assert.Contains("B.csproj", location.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ANonExistentPathIsAnError()
    {
        ProjectLocation location = ProjectLocator.Locate("Missing.csproj", EnvironmentAt(_root));

        Assert.Null(location.Path);
        Assert.Contains("does not exist", location.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotAProjectIsAnError()
    {
        string file = Touch("notes.txt");

        ProjectLocation location = ProjectLocator.Locate(file, EnvironmentAt(_root));

        Assert.Null(location.Path);
        Assert.Contains("not a .csproj", location.Error, StringComparison.Ordinal);
    }

    private static NetPrints.Cli.Infrastructure.CliEnvironment EnvironmentAt(string directory) =>
        new CliTestHost(directory).Environment;

    private string Touch(string relativePath)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.Combine(_root, Path.GetDirectoryName(relativePath) ?? string.Empty));
        File.WriteAllText(path, "<Project />");
        return path;
    }
}
