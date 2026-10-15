using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.StartPage;
using NetPrints.Projects;
using NetPrints.Testing;
using NetPrints.Workspace;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>A project created from the Executable template builds with no CS5001 (no entry point) and runs, through a real <c>dotnet build</c>.</summary>
public sealed class ExecutableTemplateBuildTests : IDisposable
{
    private readonly string root = TestPaths.CreateTempDirectory();

    public void Dispose() => TestPaths.TryDelete(root);

    [Fact(Timeout = 600_000)]
    public async Task ACreatedExecutableProjectBuildsWithoutCS5001AndRuns()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        var service = new ProjectTemplateService(() => registry.ProjectTemplates, _ => DefaultProjectProfile.Instance, projects);
        string folder = Path.Combine(root, "DemoApp");

        string csproj = await service.CreateAsync(registry.ProjectTemplates.Single(template => template.Id == "netprints.template.console"), "DemoApp", folder,
            TestContext.Current.CancellationToken);

        // The SDK package is not published for this repository's own tests: build against the in-repo SDK, as the samples do.
        LocalSdkLayout.Write(folder);
        string text = await File.ReadAllTextAsync(csproj, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(csproj,
            text.Replace("<PackageReference Include=\"NetPrints.Sdk\"", "<PackageReference Condition=\"'$(NetPrintsUseLocalSdk)' != 'true'\" Include=\"NetPrints.Sdk\"", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        (int buildExit, string buildOutput) = await RunDotnetAsync(folder, "build", csproj, "-v:n", "-tl:off", "--nologo");
        Assert.True(buildExit == 0, buildOutput);
        Assert.DoesNotContain("CS5001", buildOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(folder, "Program.netpc.g.cs")), buildOutput);

        (int runExit, string runOutput) = await RunDotnetAsync(folder, "run", "--project", csproj, "--no-build");
        Assert.True(runExit == 0, runOutput);
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        foreach (string arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        Task<string> errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(output, errors, process.WaitForExitAsync(TestContext.Current.CancellationToken));
        return (process.ExitCode, await output + await errors);
    }
}
