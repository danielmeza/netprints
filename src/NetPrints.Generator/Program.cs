#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Compilation;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;

namespace NetPrints.Generator;

/// <summary>
/// Entry point of the <c>NetPrints.Sdk</c> build-time generator: <c>generate &lt;request.rsp&gt;</c>
/// reads a request file (written by the SDK's MSBuild targets) and writes the <c>.netpc.g.cs</c> next
/// to each graph document (project-system.md §3). The <c>convert</c> command is added in T054, once
/// <c>ProjectConverter</c> exists.
/// </summary>
internal static class Program
{
    /// <summary>Exit code: generation ran with no errors (warnings are allowed).</summary>
    private const int ExitSuccess = 0;

    /// <summary>Exit code: at least one diagnostic had <see cref="CodeDiagnosticSeverity.Error"/>.</summary>
    private const int ExitGenerationErrors = 1;

    /// <summary>Exit code: bad command-line arguments, or a malformed request file.</summary>
    private const int ExitBadRequest = 2;

    /// <summary>Exit code: an unhandled exception (its stack trace is written to stderr).</summary>
    private const int ExitInternalError = 3;

    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is not ["generate", string requestPath])
            {
                await Console.Error.WriteLineAsync("Usage: NetPrints.Generator generate <request.rsp>").ConfigureAwait(false);
                return ExitBadRequest;
            }

            GenerateRequest request;
            try
            {
                request = GenerateRequestFile.Parse(requestPath);
            }
            catch (FormatException ex)
            {
                await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return ExitBadRequest;
            }

            GraphCodeGenerator generator = CreateGenerator();
            IReadOnlyList<GeneratedFileResult> results = await generator.GenerateAsync(request, CancellationToken.None).ConfigureAwait(false);

            bool hasError = false;
            foreach (GeneratedFileResult result in results)
            {
                foreach (CodeDiagnostic diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(FormatCanonical(diagnostic));
                    hasError |= diagnostic.Severity == CodeDiagnosticSeverity.Error;
                }
            }

            return hasError ? ExitGenerationErrors : ExitSuccess;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
            return ExitInternalError;
        }
    }

    private static GraphCodeGenerator CreateGenerator()
    {
        var nodeConverters = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
        var mapper = new DocumentMapper(nodeConverters);
        var jsonFormat = new JsonDocumentFormat(new NetPrintsJsonOptions(nodeConverters), new DocumentMigrator([]));
        var formats = new DocumentFormatRegistry([jsonFormat]);
        return new GraphCodeGenerator(formats, mapper);
    }

    /// <summary>
    /// Formats <paramref name="diagnostic"/> as one MSBuild canonical-format line (project-system.md
    /// §3): with a <see cref="CodeDiagnostic.Span"/>, <c>&lt;path&gt;(&lt;line&gt;,&lt;col&gt;): …</c>;
    /// without one but with a <see cref="CodeDiagnostic.GraphKey"/>, <c>&lt;path&gt;: … (graph
    /// &lt;key&gt;, node &lt;id&gt;)</c>; otherwise just <c>&lt;path&gt;: …</c>.
    /// </summary>
    /// <param name="diagnostic">Diagnostic to format.</param>
    /// <returns>The formatted line.</returns>
    private static string FormatCanonical(CodeDiagnostic diagnostic)
    {
        string severity = diagnostic.Severity switch
        {
            CodeDiagnosticSeverity.Error => "error",
            CodeDiagnosticSeverity.Warning => "warning",
            _ => "info",
        };

        string path = diagnostic.SourcePath ?? "<unknown>";
        string location = diagnostic.Span is { } span
            ? $"{path}({span.Start.Line + 1},{span.Start.Character + 1})"
            : path;

        string suffix = diagnostic.GraphKey is not null
            ? $" (graph {diagnostic.GraphKey}, node {diagnostic.NodeId})"
            : string.Empty;

        return $"{location}: {severity} {diagnostic.Id}: {diagnostic.Message}{suffix}";
    }
}
