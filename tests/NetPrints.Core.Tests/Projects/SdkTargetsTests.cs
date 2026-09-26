using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// PS-T01…PS-T04 (project-system.md §7): <c>NetPrints.Sdk.targets</c> against real, temporary
    /// projects built with the in-repo development mode <see cref="LocalSdkLayout"/> sets up.
    /// </summary>
    public class SdkTargetsTests
    {
        private static readonly NodeDocumentConverterRegistry Registry = new(NodeDocumentConverterRegistry.BuiltIn, []);

        private static async Task WriteEmptyClassGraphAsync(string path, string ns, string name)
        {
            Project project = Project.CreateNew(ns, ns, addDefaultReferences: false);
            var cls = new ClassGraph { Name = name, Namespace = ns, Visibility = MemberVisibility.Public, Project = project };
            var mapper = new DocumentMapper(Registry);
            ClassDocument document = mapper.ToDocument(cls);
            var format = new JsonDocumentFormat(new NetPrintsJsonOptions(Registry), new DocumentMigrator([]));
            await using FileStream output = File.Create(path);
            await format.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        }

        private static void WriteProjectFile(string csprojPath, string rootNamespace, string extraProperties = "", string extraItems = "")
        {
            File.WriteAllText(csprojPath, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>{rootNamespace}</RootNamespace>
                    <NetPrintsProfile>netprints.default</NetPrintsProfile>
                    {extraProperties}
                  </PropertyGroup>
                  <ItemGroup>
                    {extraItems}
                  </ItemGroup>
                </Project>

                """);
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
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask, process.WaitForExitAsync(TestContext.Current.CancellationToken));
            return (process.ExitCode, stdOutTask.Result + stdErrTask.Result);
        }

        // PS-T01: first build generates all graphs and compiles; second build logs "Skipping target
        // NetPrintsGenerate"; touching one graph regenerates only it.
        [Fact]
        public async Task FirstBuildGeneratesSecondSkipsThirdRegeneratesOnlyTheTouchedGraph()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-sdk-targets-").FullName;
            try
            {
                LocalSdkLayout.Write(directory);
                string graphAPath = Path.Combine(directory, "PsT01.A.netpc.json");
                string graphBPath = Path.Combine(directory, "PsT01.B.netpc.json");
                await WriteEmptyClassGraphAsync(graphAPath, "PsT01", "A");
                await WriteEmptyClassGraphAsync(graphBPath, "PsT01", "B");
                string csprojPath = Path.Combine(directory, "PsT01.csproj");
                WriteProjectFile(csprojPath, "PsT01");

                (int firstExit, string _) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, firstExit);
                string genAPath = Path.Combine(directory, "PsT01.A.netpc.g.cs");
                string genBPath = Path.Combine(directory, "PsT01.B.netpc.g.cs");
                Assert.True(File.Exists(genAPath));
                Assert.True(File.Exists(genBPath));

                (int secondExit, string secondOutput) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, secondExit);
                Assert.Contains("Skipping target \"NetPrintsGenerate\"", secondOutput, StringComparison.Ordinal);

                DateTime genATimeBefore = File.GetLastWriteTimeUtc(genAPath);
                DateTime genBTimeBefore = File.GetLastWriteTimeUtc(genBPath);
                // Set strictly (and unambiguously, regardless of filesystem timestamp resolution)
                // newer than the generated files' current write time, instead of sleeping.
                File.SetLastWriteTimeUtc(graphAPath, DateTime.UtcNow.AddSeconds(5));

                (int thirdExit, string thirdOutput) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, thirdExit);
                Assert.Contains("Building target \"NetPrintsGenerate\" partially", thirdOutput, StringComparison.Ordinal);
                Assert.True(File.GetLastWriteTimeUtc(genAPath) > genATimeBefore);
                Assert.Equal(genBTimeBefore, File.GetLastWriteTimeUtc(genBPath));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // PS-T02: with the generated file already present before evaluation, -getItem:Compile lists it
        // exactly once with the right DependentUpon, and the build has no duplicate-source errors.
        [Fact]
        public async Task GeneratedFileIsListedOnceWithDependentUponAndNoDuplicateSourceErrors()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-sdk-targets-").FullName;
            try
            {
                LocalSdkLayout.Write(directory);
                await WriteEmptyClassGraphAsync(Path.Combine(directory, "PsT02.A.netpc.json"), "PsT02", "A");
                string csprojPath = Path.Combine(directory, "PsT02.csproj");
                WriteProjectFile(csprojPath, "PsT02");

                (int buildExit, string buildOutput) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, buildExit);
                Assert.DoesNotContain("CS2002", buildOutput, StringComparison.Ordinal);
                Assert.DoesNotContain("CS0101", buildOutput, StringComparison.Ordinal);

                // The generated file already exists on disk at this point (written by the build
                // above): exactly the "before evaluation" condition PS-T02 exercises.
                (int getItemExit, string getItemOutput) = await RunDotnetAsync(directory, "msbuild", csprojPath, "-getItem:Compile", "-tl:off", "-nologo");
                Assert.Equal(0, getItemExit);

                using JsonDocument document = JsonDocument.Parse(getItemOutput);
                JsonElement compileItems = document.RootElement.GetProperty("Items").GetProperty("Compile");
                List<JsonElement> generated = compileItems.EnumerateArray()
                    .Where(item => item.GetProperty("Identity").GetString() is string identity
                        && identity.EndsWith("PsT02.A.netpc.g.cs", StringComparison.Ordinal))
                    .ToList();

                JsonElement generatedItem = Assert.Single(generated);
                Assert.Equal("PsT02.A.netpc.json", generatedItem.GetProperty("DependentUpon").GetString());
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        // PS-T03: EnableDefaultNetPrintsGraphItems=false plus an explicit NetPrintsGraph item outside
        // the project folder still builds and generates (next to the graph file, not the project).
        [Fact]
        public async Task ExplicitGraphOutsideProjectFolderWorksWithDefaultItemsDisabled()
        {
            string root = Directory.CreateTempSubdirectory("netprints-sdk-targets-").FullName;
            try
            {
                string projectDir = Path.Combine(root, "Proj");
                string sharedDir = Path.Combine(root, "Shared");
                Directory.CreateDirectory(projectDir);
                Directory.CreateDirectory(sharedDir);

                LocalSdkLayout.Write(projectDir);
                await WriteEmptyClassGraphAsync(Path.Combine(sharedDir, "Outside.netpc.json"), "PsT03", "Outside");

                string csprojPath = Path.Combine(projectDir, "PsT03.csproj");
                WriteProjectFile(csprojPath, "PsT03",
                    extraProperties: "<EnableDefaultNetPrintsGraphItems>false</EnableDefaultNetPrintsGraphItems>",
                    extraItems: """<NetPrintsGraph Include="../Shared/Outside.netpc.json" />""");

                (int exitCode, string _) = await RunDotnetAsync(projectDir, "build", csprojPath, "-v:n", "-tl:off", "--nologo");

                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(Path.Combine(sharedDir, "Outside.netpc.g.cs")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        // PS-T04: an invalid graph fails the build with an NPD error pointing at the graph file, and
        // the previous .g.cs is kept (not deleted).
        [Fact]
        public async Task InvalidGraphFailsTheBuildAndKeepsThePreviousGeneratedFile()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-sdk-targets-").FullName;
            try
            {
                LocalSdkLayout.Write(directory);
                string graphPath = Path.Combine(directory, "PsT04.A.netpc.json");
                await WriteEmptyClassGraphAsync(graphPath, "PsT04", "A");
                string csprojPath = Path.Combine(directory, "PsT04.csproj");
                WriteProjectFile(csprojPath, "PsT04");

                (int firstExit, string _) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");
                Assert.Equal(0, firstExit);
                string generatedPath = Path.Combine(directory, "PsT04.A.netpc.g.cs");
                string previousContent = await File.ReadAllTextAsync(generatedPath, TestContext.Current.CancellationToken);

                await File.WriteAllTextAsync(graphPath, "{ not valid json", TestContext.Current.CancellationToken);

                (int secondExit, string secondOutput) = await RunDotnetAsync(directory, "build", csprojPath, "-v:n", "-tl:off", "--nologo");

                Assert.NotEqual(0, secondExit);
                Assert.Contains($"{graphPath}(", secondOutput, StringComparison.Ordinal);
                Assert.Matches(@"error NPD\d{3}", secondOutput);
                string contentAfterFailure = await File.ReadAllTextAsync(generatedPath, TestContext.Current.CancellationToken);
                Assert.Equal(previousContent, contentAfterFailure);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
