using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>AP-T01 (extensions contract §5): every tracked project declares its public API in both PublicAPI files.</summary>
    public class PublicApiTrackingTests
    {
        private const string RuleUndeclaredApi = "RS0016";
        private const string AnalyzerPathMetadataKey = "PublicApiAnalyzersPath";

        public static TheoryData<string> TrackedProjects() =>
            ["NetPrints.Extensibility", "NetPrints.Core", "NetPrints.Reflection", "NetPrints.Serialization", "NetPrints.Catalog"];

        [Theory]
        [MemberData(nameof(TrackedProjects))]
        public void TrackedProjectHasBothApiFilesStartingWithNullableEnable(string project)
        {
            string directory = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", project);

            foreach (string name in new[] { "PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt" })
            {
                string path = Path.Combine(directory, name);
                Assert.True(File.Exists(path), $"{project} is missing {name}");
                Assert.Equal("#nullable enable", File.ReadLines(path).First().TrimStart('﻿'));
            }

            string csproj = File.ReadAllText(Path.Combine(directory, project + ".csproj"));
            Assert.Contains("<NetPrintsTrackPublicApi>true</NetPrintsTrackPublicApi>", csproj, StringComparison.Ordinal);
        }

        [Fact]
        public void SourcePropsReferencesTheAnalyzerAndAddsBothFilesForTrackedProjects()
        {
            string props = File.ReadAllText(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src", "Directory.Build.props"));

            Assert.Contains("'$(NetPrintsTrackPublicApi)' == 'true'", props, StringComparison.Ordinal);
            Assert.Contains("Microsoft.CodeAnalysis.PublicApiAnalyzers", props, StringComparison.Ordinal);
            Assert.Contains("PublicAPI.Shipped.txt", props, StringComparison.Ordinal);
            Assert.Contains("PublicAPI.Unshipped.txt", props, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UndeclaredPublicTypeReportsRs0016()
        {
            string analyzerDirectory = typeof(PublicApiTrackingTests).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == AnalyzerPathMetadataKey).Value
                ?? throw new InvalidOperationException("Analyzer path metadata has no value.");
            ImmutableArray<DiagnosticAnalyzer> analyzers =
            [
                .. Directory.EnumerateFiles(Path.Combine(analyzerDirectory, "analyzers", "dotnet"), "Microsoft.CodeAnalysis.PublicApiAnalyzers.dll")
                    .Select(Assembly.LoadFrom)
                    .SelectMany(LoadableTypes)
                    .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                        && type.GetCustomAttributes<DiagnosticAnalyzerAttribute>().Any(attribute => attribute.Languages.Contains(LanguageNames.CSharp)))
                    .Select(type => (DiagnosticAnalyzer)(Activator.CreateInstance(type)
                        ?? throw new InvalidOperationException($"Cannot create {type}.")))
            ];

            CSharpCompilation compilation = CSharpCompilation.Create(
                "Tracked",
                [CSharpSyntaxTree.ParseText("#nullable enable\nnamespace Tracked { public class Undeclared { } }", cancellationToken: TestContext.Current.CancellationToken)],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            ImmutableArray<AdditionalText> files =
            [
                new InMemoryText("PublicAPI.Shipped.txt", "#nullable enable\n"),
                new InMemoryText("PublicAPI.Unshipped.txt", "#nullable enable\n"),
            ];

            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers(analyzers, new AnalyzerOptions(files))
                .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

            Assert.Contains(diagnostics, diagnostic => diagnostic.Id == RuleUndeclaredApi && diagnostic.GetMessage(null).Contains("Undeclared", StringComparison.Ordinal));
        }

        private static Type[] LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return [.. exception.Types.OfType<Type>()];
            }
        }

        private sealed class InMemoryText(string path, string text) : AdditionalText
        {
            public override string Path { get; } = path;

            public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => SourceText.From(text);
        }
    }
}
