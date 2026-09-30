using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Cli.Infrastructure;
using NetPrints.Serialization;
using NetPrints.Serialization.Stores;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints format [&lt;path&gt;...] [--check]</c>: rewrites each graph in the canonical form of its <see cref="IDocumentFormat"/> and
/// touches only the files whose bytes change (contracts/git.md, FR-032).
/// </summary>
internal sealed class FormatCommand(IAnsiConsole console, CliEnvironment environment) : AsyncCommand<FormatSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "format";

    private readonly DocumentFormatRegistry _formats = GraphFormats.CreateRegistry();

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, FormatSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var graphs = new SortedSet<string>(StringComparer.Ordinal);
        var unreadable = new List<string>();
        foreach (string argument in settings.Paths.Length == 0 ? [""] : settings.Paths)
        {
            string? error = Collect(argument, graphs, unreadable);
            if (error is not null)
            {
                await environment.Error.WriteLineAsync(error).ConfigureAwait(false);
                return ExitCodes.Usage;
            }
        }

        foreach (string line in unreadable)
        {
            console.WriteLineRaw(line);
        }

        int changed = 0;
        int failed = unreadable.Count;
        foreach (string graph in graphs)
        {
            FormatOutcome outcome = await FormatAsync(graph, settings.Check, cancellationToken).ConfigureAwait(false);
            changed += outcome == FormatOutcome.Changed ? 1 : 0;
            failed += outcome == FormatOutcome.Unreadable ? 1 : 0;
        }

        console.WriteLineRaw(Summary(settings.Check, graphs.Count, changed, failed));
        return failed == 0 && (!settings.Check || changed == 0) ? ExitCodes.Success : ExitCodes.Failed;
    }

    private static string Summary(bool check, int total, int changed, int failed)
    {
        string unreadable = failed == 0 ? string.Empty : $" {failed} could not be read.";
        if (!check)
        {
            return $"{changed} of {total} graph(s) formatted.{unreadable}";
        }

        return changed == 0 && failed == 0
            ? $"{total} graph(s) are canonical."
            : $"{changed} of {total} graph(s) are not canonical.{unreadable}";
    }

    private string? Collect(string argument, SortedSet<string> graphs, List<string> unreadable)
    {
        string path = Path.GetFullPath(argument.Length == 0 ? "." : argument, environment.CurrentDirectory);
        if (Directory.Exists(path))
        {
            GraphFileSearch.Collect(path, graphs, unreadable, environment.CurrentDirectory);
            return null;
        }

        if (!File.Exists(path))
        {
            return $"'{path}' does not exist.";
        }

        if (!path.EndsWith(GraphFileSearch.GraphExtension, StringComparison.OrdinalIgnoreCase))
        {
            return $"'{path}' is neither a {GraphFileSearch.GraphExtension} graph nor a directory.";
        }

        graphs.Add(path);
        return null;
    }

    private async Task<FormatOutcome> FormatAsync(string graph, bool check, CancellationToken cancellationToken)
    {
        string display = Path.GetRelativePath(environment.CurrentDirectory, graph);
        var id = new DocumentId(Path.GetFileName(graph));
        IDocumentFormat? format = _formats.Find(id, DocumentKind.Class);
        if (format is null || !format.CanWrite)
        {
            console.WriteLineRaw($"unreadable: {display}: no document format can write this file.");
            return FormatOutcome.Unreadable;
        }

        try
        {
            using var store = new FileSystemDocumentStore(Path.GetDirectoryName(graph) ?? environment.CurrentDirectory, Scheduler.Default,
                NullLogger<FileSystemDocumentStore>.Instance, watch: false);
            using var original = new MemoryStream();
            await using (Stream input = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false))
            {
                await input.CopyToAsync(original, cancellationToken).ConfigureAwait(false);
            }

            original.Position = 0;
            var document = await format.ReadClassAsync(original, id, cancellationToken).ConfigureAwait(false);
            using var canonical = new MemoryStream();
            await format.WriteClassAsync(document, canonical, cancellationToken).ConfigureAwait(false);
            if (canonical.GetBuffer().AsSpan(0, (int)canonical.Length).SequenceEqual(original.GetBuffer().AsSpan(0, (int)original.Length)))
            {
                return FormatOutcome.Canonical;
            }

            if (check)
            {
                console.WriteLineRaw($"not canonical: {display}");
                return FormatOutcome.Changed;
            }

            await store.WriteAsync(
                id,
                async (output, ct) => await output.WriteAsync(canonical.GetBuffer().AsMemory(0, (int)canonical.Length), ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
            console.WriteLineRaw($"formatted: {display}");
            return FormatOutcome.Changed;
        }
        catch (DocumentVersionException ex)
        {
            console.WriteLineRaw($"unreadable: {display}: schema {ex.Found} is not supported (this tool supports {ex.Supported})");
            return FormatOutcome.Unreadable;
        }
        catch (Exception ex) when (ex is DocumentFormatException or IOException or DocumentNotFoundException or UnauthorizedAccessException)
        {
            console.WriteLineRaw($"unreadable: {display}: {ex.Message}");
            return FormatOutcome.Unreadable;
        }
    }

    private enum FormatOutcome
    {
        Canonical,
        Changed,
        Unreadable,
    }
}
