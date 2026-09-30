using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Extensibility;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>AP-T02 (extensions contract §5, ADR-0017): an external project must opt in to every experimental API it uses.</summary>
    public class ExperimentalApiTests
    {
        private const string HelpLinkAnchor = "#api-stability";

        public static TheoryData<string, string> MarkedApis() => new()
        {
            { "NPXE0001", "using NetPrints.Extensibility.Hosting; class Consumer { IHostChannelFactory? Factory; }" },
            { "NPXE0002", "using NetPrints.Extensibility.Settings; class Consumer { ISettingsStore? Store; }" },
            { "NPXE0003", "using NetPrints.Translator; class Consumer { IClassEmitter? Emitter; }" },
        };

        [Theory]
        [MemberData(nameof(MarkedApis))]
        public void UsingAMarkedApiWithoutOptInIsAnErrorThatLinksToTheGuide(string id, string source)
        {
            ImmutableArray<Diagnostic> diagnostics = Compile(source, optIn: null);

            Diagnostic[] errors = [.. diagnostics.Where(diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Error)];

            Assert.NotEmpty(errors);
            Assert.All(errors, error => Assert.Contains(HelpLinkAnchor, error.Descriptor.HelpLinkUri, StringComparison.Ordinal));
        }

        [Theory]
        [MemberData(nameof(MarkedApis))]
        public void UsingAMarkedApiWithItsIdSuppressedCompiles(string id, string source)
        {
            ImmutableArray<Diagnostic> diagnostics = Compile(source, optIn: id);

            Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        }

        private static ImmutableArray<Diagnostic> Compile(string source, string? optIn)
        {
            string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
            List<MetadataReference> references =
            [
                .. trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => MetadataReference.CreateFromFile(path)),
                MetadataReference.CreateFromFile(typeof(IExtensionBuilder).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IClassEmitter).Assembly.Location),
            ];
            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable);
            if (optIn is not null)
            {
                options = options.WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic> { [optIn] = ReportDiagnostic.Suppress });
            }

            CSharpCompilation compilation = CSharpCompilation.Create(
                "External",
                [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
                references,
                options);

            return compilation.GetDiagnostics(TestContext.Current.CancellationToken);
        }
    }
}
