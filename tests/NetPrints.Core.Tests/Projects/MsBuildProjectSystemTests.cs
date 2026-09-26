using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// PS-T07, PS-T08, PS-T09, PS-T11 and the <c>CreateAsync</c> part of PS-T15 (project-system.md §7):
    /// <see cref="MsBuildProjectSystem"/> against real, temporary projects and a real MSBuild instance
    /// (<see cref="MsBuildTestInitializer"/> registers it once for this assembly).
    /// </summary>
    public class MsBuildProjectSystemTests
    {
        /// <summary>Wraps a real <see cref="ProcessRunner"/>, recording every request it ran.</summary>
        private sealed class RecordingProcessRunner : IProcessRunner
        {
            private readonly IProcessRunner inner = new ProcessRunner();

            public List<ProcessStartRequest> Requests { get; } = [];

            public int RestoreCallCount => Requests.Count(request => request.Arguments.Contains("restore"));

            public async Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return await inner.RunAsync(request, cancellationToken).ConfigureAwait(false);
            }
        }

        private static MsBuildProjectSystem NewSystem(IProcessRunner? processRunner = null) =>
            new(new ProjectSystemOptions([], "9.9.9-test"), processRunner ?? new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        private static void WriteAppProject(string csprojPath, string rootNamespace, string extraItems = "")
        {
            File.WriteAllText(csprojPath, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>{rootNamespace}</RootNamespace>
                    <NetPrintsProfile>netprints.default</NetPrintsProfile>
                  </PropertyGroup>
                  <ItemGroup>
                    {extraItems}
                  </ItemGroup>
                </Project>

                """);
        }

        // PS-T07: LoadAsync on a HelloWorld-equivalent project: properties, graph files, System.Console.dll
        // from a reference pack with DocumentationPath; restore runs when obj/ is missing and not when current.
        [Fact]
        public async Task LoadAsyncReadsPropertiesGraphFilesAndFrameworkReferenceDocs()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-mbps-t07-").FullName;
            try
            {
                string csprojPath = Path.Combine(directory, "PsT07.csproj");
                WriteAppProject(csprojPath, "PsT07", """<NetPrintsGraph Include="Program.netpc.json" />""");
                await File.WriteAllTextAsync(Path.Combine(directory, "Program.netpc.json"), "{}", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"),
                    "System.Console.WriteLine(\"Hello, World!\");", TestContext.Current.CancellationToken);

                var runner = new RecordingProcessRunner();
                MsBuildProjectSystem system = NewSystem(runner);

                ProjectSnapshot first = await system.LoadAsync(csprojPath, TestContext.Current.CancellationToken);

                Assert.Equal("PsT07", first.Name);
                Assert.Equal("PsT07", first.RootNamespace);
                Assert.Equal(BinaryType.Executable, first.OutputType);
                Assert.Equal("net10.0", first.TargetFramework);
                Assert.Equal("netprints.default", first.ProfileId);
                Assert.False(first.ReferencesNetPrintsSdk);
                string graphFile = Assert.Single(first.GraphFiles);
                Assert.EndsWith("Program.netpc.json", graphFile, StringComparison.Ordinal);

                ResolvedAssembly consoleReference = Assert.Single(first.References,
                    reference => reference.Path.EndsWith("System.Console.dll", StringComparison.Ordinal));
                Assert.NotNull(consoleReference.DocumentationPath);
                Assert.True(File.Exists(consoleReference.DocumentationPath));

                Assert.Equal(1, runner.RestoreCallCount);

                ProjectSnapshot second = await system.LoadAsync(csprojPath, TestContext.Current.CancellationToken);
                Assert.Equal(1, runner.RestoreCallCount);
                Assert.Equal(first.Name, second.Name);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // PS-T08: LoadAsync with a PackageReference (a local test package built into a temp feed) and a
        // ProjectReference: both resolved.
        [Fact]
        public async Task LoadAsyncResolvesPackageAndProjectReferences()
        {
            string root = Directory.CreateTempSubdirectory("netprints-mbps-t08-").FullName;
            try
            {
                string libDir = Path.Combine(root, "OtherLib");
                Directory.CreateDirectory(libDir);
                string libCsproj = Path.Combine(libDir, "OtherLib.csproj");
                File.WriteAllText(libCsproj, """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                      </PropertyGroup>
                    </Project>

                    """);
                await File.WriteAllTextAsync(Path.Combine(libDir, "Class1.cs"), "public class Class1 { }", TestContext.Current.CancellationToken);

                string packageSrcDir = Path.Combine(root, "TestPackageSrc");
                Directory.CreateDirectory(packageSrcDir);
                string packageCsproj = Path.Combine(packageSrcDir, "NetPrints.Test.LocalPackage.csproj");
                File.WriteAllText(packageCsproj, """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net10.0</TargetFramework>
                      </PropertyGroup>
                    </Project>

                    """);
                await File.WriteAllTextAsync(Path.Combine(packageSrcDir, "Class1.cs"), "public class PackageClass1 { }", TestContext.Current.CancellationToken);

                string feedDir = Path.Combine(root, "feed");
                Directory.CreateDirectory(feedDir);
                const string packageVersion = "1.0.0-ps-t08";
                (int packExit, string packOutput) = await ExternalProcess.RunDotnetAsync(root, environment: null,
                    "pack", packageCsproj, "-c", "Release", "-o", feedDir, $"-p:Version={packageVersion}", "--nologo");
                Assert.True(packExit == 0, packOutput);

                (int libBuildExit, string libBuildOutput) = await ExternalProcess.RunDotnetAsync(root, environment: null,
                    "build", libCsproj, "-c", "Debug", "--nologo", "-tl:off");
                Assert.True(libBuildExit == 0, libBuildOutput);

                string appDir = Path.Combine(root, "App");
                Directory.CreateDirectory(appDir);
                File.WriteAllText(Path.Combine(appDir, "nuget.config"), $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <configuration>
                      <packageSources>
                        <clear />
                        <add key="local" value="{feedDir}" />
                      </packageSources>
                    </configuration>

                    """);
                string appCsproj = Path.Combine(appDir, "App.csproj");
                WriteAppProject(appCsproj, "App", $"""
                    <PackageReference Include="NetPrints.Test.LocalPackage" Version="{packageVersion}" />
                    <ProjectReference Include="../OtherLib/OtherLib.csproj" />
                    """);
                await File.WriteAllTextAsync(Path.Combine(appDir, "Program.cs"), "System.Console.WriteLine(\"App\");", TestContext.Current.CancellationToken);

                MsBuildProjectSystem system = NewSystem();
                ProjectSnapshot snapshot = await system.LoadAsync(appCsproj, TestContext.Current.CancellationToken);

                Assert.True(snapshot.References.Any(reference => reference.Path.EndsWith("OtherLib.dll", StringComparison.Ordinal)),
                    string.Join('\n', snapshot.References.Select(r => r.Path)));
                Assert.True(snapshot.References.Any(reference => reference.Path.Contains("NetPrints.Test.LocalPackage", StringComparison.Ordinal)),
                    string.Join('\n', snapshot.References.Select(r => r.Path)));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        // PS-T09: ApplyAsync: each ProjectEdit; untouched parts of the file byte-identical (comments,
        // formatting); duplicate assembly no-op.
        [Fact]
        public async Task ApplyAsyncAppliesEachEditKindAndLeavesUntouchedPartsIntact()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-mbps-t09-").FullName;
            try
            {
                string csprojPath = Path.Combine(directory, "PsT09.csproj");
                string existingAssemblyPath = Path.Combine(directory, "libs", "Existing.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(existingAssemblyPath)!);
                // A real assembly file (this test assembly's own output), not arbitrary bytes: Roslyn
                // must be able to treat it as a genuine PE reference.
                File.Copy(typeof(MsBuildProjectSystemTests).Assembly.Location, existingAssemblyPath);

                string sourceDir = Path.Combine(directory, "Extra");
                Directory.CreateDirectory(sourceDir);
                await File.WriteAllTextAsync(Path.Combine(sourceDir, "Extra.cs"), "public class Extra {}", TestContext.Current.CancellationToken);

                File.WriteAllText(csprojPath, """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <!-- a hand-written comment -->
                      <PropertyGroup>
                        <OutputType>Library</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>PsT09</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>
                    </Project>

                    """);

                MsBuildProjectSystem system = NewSystem();

                ProjectSnapshot snapshot = await system.ApplyAsync(csprojPath,
                    [
                        new ProjectEdit.SetOutputType(BinaryType.Executable),
                        new ProjectEdit.SetProfile("netprints.other"),
                        new ProjectEdit.AddAssemblyReference(existingAssemblyPath),
                        new ProjectEdit.AddAssemblyReference(existingAssemblyPath), // duplicate: no-op (PAR-17)
                        new ProjectEdit.AddSourceDirectory(sourceDir),
                        new ProjectEdit.AddNetPrintsSdk("9.9.9-test"),
                    ],
                    TestContext.Current.CancellationToken);

                Assert.Equal(BinaryType.Executable, snapshot.OutputType);
                Assert.Equal("netprints.other", snapshot.ProfileId);
                Assert.True(snapshot.ReferencesNetPrintsSdk);
                ProjectReferenceInfo assemblyRef = Assert.Single(snapshot.DeclaredReferences,
                    reference => reference.Kind == DeclaredReferenceKind.Assembly);
                Assert.Equal("Existing", assemblyRef.Include);
                ProjectReferenceInfo sourceDirRef = Assert.Single(snapshot.DeclaredReferences,
                    reference => reference.Kind == DeclaredReferenceKind.SourceDirectory);
                Assert.True(sourceDirRef.Included);

                string savedContent = await File.ReadAllTextAsync(csprojPath, TestContext.Current.CancellationToken);
                Assert.Contains("<!-- a hand-written comment -->", savedContent, StringComparison.Ordinal);

                ProjectSnapshot afterToggle = await system.ApplyAsync(csprojPath,
                    [new ProjectEdit.SetSourceDirectoryIncluded(sourceDir, Included: false)],
                    TestContext.Current.CancellationToken);
                ProjectReferenceInfo toggledSourceDir = Assert.Single(afterToggle.DeclaredReferences,
                    reference => reference.Kind == DeclaredReferenceKind.SourceDirectory);
                Assert.False(toggledSourceDir.Included);

                ProjectSnapshot afterRemove = await system.ApplyAsync(csprojPath,
                    [new ProjectEdit.RemoveReference(DeclaredReferenceKind.Assembly, "Existing")],
                    TestContext.Current.CancellationToken);
                Assert.False(afterRemove.DeclaredReferences.Any(reference => reference.Kind == DeclaredReferenceKind.Assembly));

                await Assert.ThrowsAsync<ArgumentException>(() => system.ApplyAsync(csprojPath,
                    [new ProjectEdit.RemoveReference(DeclaredReferenceKind.Package, "Whatever")],
                    TestContext.Current.CancellationToken));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // The CreateAsync part of PS-T15: writes exactly the .gitattributes lines of §1.1, and an
        // existing .csproj at that path fails with nothing (re)written.
        [Fact]
        public async Task CreateAsyncWritesProjectFileAndGitAttributesAndRejectsAnExistingProject()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-mbps-create-").FullName;
            try
            {
                MsBuildProjectSystem system = NewSystem();
                string csprojPath = await system.CreateAsync(directory, "MyProject", DefaultProjectProfile.Instance,
                    "MyProject.Core", TestContext.Current.CancellationToken);

                Assert.Equal(Path.Combine(directory, "MyProject.csproj"), csprojPath);
                string content = await File.ReadAllTextAsync(csprojPath, TestContext.Current.CancellationToken);
                Assert.Contains("<TargetFramework>net10.0</TargetFramework>", content, StringComparison.Ordinal);
                Assert.Contains("<RootNamespace>MyProject.Core</RootNamespace>", content, StringComparison.Ordinal);
                Assert.Contains("<NetPrintsProfile>netprints.default</NetPrintsProfile>", content, StringComparison.Ordinal);
                Assert.Contains("Version=\"9.9.9-test\"", content, StringComparison.Ordinal);
                Assert.DoesNotContain('{', content);

                string gitAttributesContent = await File.ReadAllTextAsync(
                    Path.Combine(directory, ".gitattributes"), TestContext.Current.CancellationToken);
                Assert.Equal("*.netpc.json text eol=lf\n*.netpc.g.cs text eol=lf\n", gitAttributesContent);

                await Assert.ThrowsAsync<IOException>(() => system.CreateAsync(directory, "MyProject",
                    DefaultProjectProfile.Instance, "MyProject.Core", TestContext.Current.CancellationToken));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // PS-T11: BuildAsync/run: a HelloWorld-equivalent project builds and prints "Hello, World!" via
        // GetRunCommand.
        [Fact]
        public async Task BuildAsyncBuildsAndGetRunCommandPrintsHelloWorld()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-mbps-t11-").FullName;
            try
            {
                string csprojPath = Path.Combine(directory, "PsT11.csproj");
                WriteAppProject(csprojPath, "PsT11");
                await File.WriteAllTextAsync(Path.Combine(directory, "Program.cs"),
                    "System.Console.WriteLine(\"Hello, World!\");", TestContext.Current.CancellationToken);

                var runner = new ProcessRunner();
                MsBuildProjectSystem system = new(new ProjectSystemOptions([], "9.9.9-test"), runner, NullLogger<MsBuildProjectSystem>.Instance);

                BuildResult buildResult = await system.BuildAsync(csprojPath, TestContext.Current.CancellationToken);

                Assert.True(buildResult.Success, buildResult.Log);
                Assert.NotNull(buildResult.OutputAssemblyPath);
                Assert.True(File.Exists(buildResult.OutputAssemblyPath));
                Assert.Empty(buildResult.Messages);

                ProcessStartRequest runCommand = system.GetRunCommand(csprojPath);
                ProcessResult runResult = await runner.RunAsync(runCommand, TestContext.Current.CancellationToken);

                Assert.Equal(0, runResult.ExitCode);
                Assert.Contains("Hello, World!", runResult.StandardOutput, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
