using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Projects;
using NetPrints.Translator;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Compilation
{
    /// <summary>
    /// <see cref="CodeAnalysisSession"/> (compilation-and-diagnostics.md §3), against a real, temporary
    /// project loaded through <see cref="MsBuildProjectSystem"/> so its references carry real
    /// <see cref="ResolvedAssembly.DocumentationPath"/>s, the same way the editor's would: RC-T08.
    /// </summary>
    public class CodeAnalysisSessionTests
    {
        private static void WriteLibraryProject(string csprojPath, string rootNamespace) =>
            File.WriteAllText(csprojPath, $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <RootNamespace>{rootNamespace}</RootNamespace>
                    <NetPrintsProfile>netprints.default</NetPrintsProfile>
                  </PropertyGroup>
                </Project>

                """);

        private static async Task<ProjectSnapshot> LoadSnapshotAsync(string directory)
        {
            string csprojPath = Path.Combine(directory, "Fixture.csproj");
            WriteLibraryProject(csprojPath, "Fixture");

            var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
            return await system.LoadAsync(csprojPath, TestContext.Current.CancellationToken);
        }

        [Fact(Timeout = 120000)]
        public async Task QuickInfoForConsoleWriteLineReturnsSignatureAndPackSummary()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-cas-t08-").FullName;
            try
            {
                ProjectSnapshot snapshot = await LoadSnapshotAsync(directory);
                Assert.Contains(snapshot.References, reference => reference.Path.EndsWith("System.Console.dll", StringComparison.Ordinal) && reference.DocumentationPath is not null);

                var session = new CodeAnalysisSession(snapshot.References, snapshot.OtherSources, snapshot.CompilationOptionsJson);

                const string code = "namespace Fixture { public class Program { public static void Run() { System.Console.WriteLine(\"hi\"); } } }";
                var translated = new TranslatedClass("Fixture.Program", code, SourceMap.Empty);

                IReadOnlyList<CodeDiagnostic> diagnostics = await session.AnalyzeAsync([translated], TestContext.Current.CancellationToken);
                Assert.Empty(diagnostics.Where(d => d.Severity == CodeDiagnosticSeverity.Error));

                int position = code.IndexOf("WriteLine", StringComparison.Ordinal) + 2;
                QuickInfo? quickInfo = await session.GetQuickInfoAsync("Fixture.Program", position, TestContext.Current.CancellationToken);

                Assert.NotNull(quickInfo);
                Assert.Contains("WriteLine", quickInfo.Signature, StringComparison.Ordinal);
                Assert.False(string.IsNullOrWhiteSpace(quickInfo.Summary));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact(Timeout = 120000)]
        public async Task AnalyzeAsyncReportsACompileErrorWithNoSourceMapEntry()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-cas-diag-").FullName;
            try
            {
                ProjectSnapshot snapshot = await LoadSnapshotAsync(directory);
                var session = new CodeAnalysisSession(snapshot.References, snapshot.OtherSources, snapshot.CompilationOptionsJson);

                const string code = "namespace Fixture { public class Broken { public static void Run() { Undefined(); } } }";
                var translated = new TranslatedClass("Fixture.Broken", code, SourceMap.Empty);

                IReadOnlyList<CodeDiagnostic> diagnostics = await session.AnalyzeAsync([translated], TestContext.Current.CancellationToken);

                CodeDiagnostic error = Assert.Single(diagnostics, d => d.Id == "CS0103");
                Assert.Equal("Fixture.Broken", error.ClassFullName);
                Assert.Null(error.GraphKey);
                Assert.Null(error.NodeId);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
