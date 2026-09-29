using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Reflection;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>
/// RC-T09 (compilation-and-diagnostics.md §7): the tooltip's method documentation, resolved from a
/// real project's references (<see cref="ProjectSnapshot.References"/>, project-system.md §4) instead
/// of <see cref="ReflectionProvider"/> guessing a documentation path itself (T058).
/// </summary>
public sealed class DocumentationTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("netprints-doc-tests-").FullName;

    public void Dispose()
    {
        try
        { Directory.Delete(directory, recursive: true); }
        catch (IOException) { }
    }

    [Fact]
    public async Task ConsoleWriteLineDocumentationIsNonEmptyOnLinux()
    {
        string csprojPath = Path.Combine(directory, "DocTest.csproj");
        File.WriteAllText(csprojPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>DocTest</RootNamespace>
              </PropertyGroup>
            </Project>

            """);
        File.WriteAllText(Path.Combine(directory, "Program.cs"), "System.Console.WriteLine(\"hi\");");

        var projectSystem = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        ProjectSnapshot snapshot = await projectSystem.LoadAsync(csprojPath, TestContext.Current.CancellationToken);

        var provider = new ReflectionProvider(snapshot.References, [], new HashSet<string>());

        var writeLine = provider.GetMethods(new ReflectionProviderMethodQuery
        {
            Type = TypeSpecifier.FromType(typeof(Console)),
            Static = true,
        }).First(m => m.Name == "WriteLine");

        var stringOverload = provider.GetPublicMethodOverloads(writeLine)
            .First(o => o.Parameters.Count == 1 && o.Parameters[0].Value == TypeSpecifier.FromType<string>());

        string? documentation = provider.GetMethodDocumentation(stringOverload);

        Assert.False(string.IsNullOrEmpty(documentation));
    }
}
