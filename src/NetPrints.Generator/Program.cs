#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Compilation;
using NetPrints.Extensibility.Loading;

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

            (ExtensionRegistry registry, IReadOnlyList<CodeDiagnostic> extensionDiagnostics) = GraphCodeGenerator.LoadExtensions(request, CancellationToken.None);
            await using (registry)
            {
                if (extensionDiagnostics.Count > 0)
                {
                    foreach (CodeDiagnostic diagnostic in extensionDiagnostics)
                    {
                        Console.WriteLine(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
                    }

                    return ExitGenerationErrors;
                }

                return await GenerateAsync(GraphCodeGenerator.Create(registry), request).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.ToString()).ConfigureAwait(false);
            return ExitInternalError;
        }
    }

    private static async Task<int> GenerateAsync(GraphCodeGenerator generator, GenerateRequest request)
    {
        IReadOnlyList<GeneratedFileResult> results = await generator.GenerateAsync(request, CancellationToken.None).ConfigureAwait(false);

        bool hasError = false;
        foreach (GeneratedFileResult result in results)
        {
            foreach (CodeDiagnostic diagnostic in result.Diagnostics)
            {
                Console.WriteLine(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
                hasError |= diagnostic.Severity == CodeDiagnosticSeverity.Error;
            }
        }

        return hasError ? ExitGenerationErrors : ExitSuccess;
    }
}
