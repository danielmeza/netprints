using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Cli.Infrastructure;
using NetPrints.Compilation;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints generate [&lt;project&gt;] [--check] [--graph &lt;file&gt;]...</c> (alias <c>regen</c>): regenerates the <c>.netpc.g.cs</c> of
/// the project's graphs, or with <c>--check</c> reports the stale ones without writing (contracts/cli.md §4).
/// </summary>
internal sealed class GenerateCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    Lazy<IProjectSystem> projects) : ProjectCommandBase<GenerateSettings>(console, environment, msBuild, loggerFactory)
{
    /// <summary>The command name.</summary>
    public const string Name = "generate";

    /// <summary>The command's alias.</summary>
    public const string Alias = "regen";

    /// <inheritdoc/>
    protected override async Task<int> ExecuteProjectAsync(CommandContext context, string projectPath, GenerateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ProjectSnapshot snapshot = await projects.Value.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
        if (ProjectMessageFormat.WriteErrors(snapshot.Messages, Environment.Error))
        {
            return ExitCodes.Failed;
        }

        GenerateRequest request = GenerateRequestFactory.FromSnapshot(snapshot);

        if (settings.Graphs.Length > 0)
        {
            var wanted = settings.Graphs.Select(graph => Path.GetFullPath(graph, Environment.CurrentDirectory)).ToList();
            StringComparer comparer = GraphPathComparison.Default;
            string? unknown = wanted.Find(graph => !request.Graphs.Any(job => comparer.Equals(job.Input, graph)));
            if (unknown is not null)
            {
                await Environment.Error.WriteLineAsync($"'{unknown}' is not a graph of the project '{projectPath}'.").ConfigureAwait(false);
                return ExitCodes.Usage;
            }

            request = request with { Graphs = [.. request.Graphs.Where(job => wanted.Contains(job.Input, comparer))] };
        }

        (ExtensionRegistry registry, IReadOnlyList<CodeDiagnostic> extensionDiagnostics) = GraphCodeGenerator.LoadExtensions(request, cancellationToken);
        await using (registry.ConfigureAwait(false))
        {
            if (extensionDiagnostics.Count > 0)
            {
                WriteDiagnostics(extensionDiagnostics);
                return ExitCodes.Failed;
            }

            GenerationMode mode = settings.Check ? GenerationMode.Check : GenerationMode.Write;
            IReadOnlyList<GeneratedFileResult> results = await GraphCodeGenerator.Create(registry)
                .GenerateAsync(request, mode, cancellationToken).ConfigureAwait(false);
            return Report(results, Path.GetDirectoryName(projectPath) ?? Environment.CurrentDirectory, settings.Check);
        }
    }

    private int Report(IReadOnlyList<GeneratedFileResult> results, string projectDirectory, bool check)
    {
        bool hasError = false;
        int upToDate = 0;
        int written = 0;
        int stale = 0;
        foreach (GeneratedFileResult result in results)
        {
            WriteDiagnostics(result.Diagnostics);
            if (result.Diagnostics.Any(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Error))
            {
                hasError = true;
                continue;
            }

            string relative = Path.GetRelativePath(projectDirectory, result.Output);
            if (result.Written)
            {
                written++;
                Console.WriteLineRaw($"generated: {relative}");
            }
            else if (result.UpToDate)
            {
                upToDate++;
            }
            else
            {
                stale++;
                Console.WriteLineRaw($"stale: {relative}");
            }
        }

        Console.WriteLineRaw(check && stale > 0
            ? $"{stale} stale."
            : $"{upToDate} generated file(s) up to date, {written} written.");
        return hasError || stale > 0 ? ExitCodes.Failed : ExitCodes.Success;
    }

    private void WriteDiagnostics(IEnumerable<CodeDiagnostic> diagnostics)
    {
        foreach (CodeDiagnostic diagnostic in diagnostics)
        {
            Console.WriteLineRaw(CodeDiagnosticFormat.ToCanonicalLine(diagnostic));
        }
    }
}
