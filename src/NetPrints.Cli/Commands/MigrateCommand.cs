using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints migrate [&lt;path&gt;...]</c>: reads each graph through its <see cref="IDocumentFormat"/> and reports its schema
/// version. While version 1 is current no migration exists, so nothing is written (contracts/cli.md §4).
/// </summary>
internal sealed class MigrateCommand(
    IAnsiConsole console,
    CliEnvironment environment,
    IMsBuildRegistration msBuild,
    ILoggerFactory loggerFactory,
    Lazy<IProjectSystem> projects) : AsyncCommand<MigrateSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "migrate";

    private const string GraphExtension = ".netpc.json";
    private const string ProjectExtension = ".csproj";
    private static readonly string[] SkippedDirectories = ["bin", "obj"];

    private readonly DocumentFormatRegistry _formats = new([
        new JsonDocumentFormat(
            new NetPrintsJsonOptions(new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, [])),
            new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance)),
    ]);

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, MigrateSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string[] arguments = settings.Paths.Length == 0 ? [""] : settings.Paths;
        var graphs = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string argument in arguments)
        {
            string? error = await CollectAsync(argument, graphs, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                console.WriteLineRaw(error);
                return error == NoSdkMessage ? ExitCodes.NoSdk : ExitCodes.Usage;
            }
        }

        int failures = 0;
        foreach (string graph in graphs)
        {
            if (!await ReportAsync(graph, cancellationToken).ConfigureAwait(false))
            {
                failures++;
            }
        }

        console.WriteLineRaw(failures == 0
            ? $"No migrations are available; {graphs.Count} graph(s) are at schema version {DocumentMigrator.CurrentSchemaVersion}."
            : $"{failures} of {graphs.Count} graph(s) could not be read.");
        return failures == 0 ? ExitCodes.Success : ExitCodes.Failed;
    }

    private const string NoSdkMessage = "No .NET SDK could be found; nothing was checked.";

    private async Task<string?> CollectAsync(string argument, SortedSet<string> graphs, CancellationToken cancellationToken)
    {
        string path = Path.GetFullPath(argument.Length == 0 ? "." : argument, environment.CurrentDirectory);

        if (argument.Length > 0 && File.Exists(path) && path.EndsWith(GraphExtension, StringComparison.OrdinalIgnoreCase))
        {
            graphs.Add(path);
            return null;
        }

        if (argument.Length > 0 && Directory.Exists(path))
        {
            foreach (string file in EnumerateGraphs(path))
            {
                graphs.Add(file);
            }

            return null;
        }

        if (argument.Length > 0 && !File.Exists(path))
        {
            return $"'{path}' does not exist.";
        }

        if (argument.Length > 0 && !path.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase))
        {
            return $"'{path}' is neither a {GraphExtension} graph, a {ProjectExtension} project nor a directory.";
        }

        ProjectLocation location = ProjectLocator.Locate(argument.Length == 0 ? null : argument, environment);
        if (location.Path is not { } projectPath)
        {
            return location.Error ?? "The project could not be resolved.";
        }

        if (!msBuild.EnsureRegistered(loggerFactory.CreateLogger(nameof(IMsBuildRegistration))))
        {
            return NoSdkMessage;
        }

        ProjectSnapshot snapshot = await projects.Value.LoadAsync(projectPath, cancellationToken).ConfigureAwait(false);
        foreach (string file in snapshot.GraphFiles)
        {
            graphs.Add(Path.GetFullPath(file, Path.GetDirectoryName(projectPath) ?? environment.CurrentDirectory));
        }

        return null;
    }

    private static IEnumerable<string> EnumerateGraphs(string directory)
    {
        foreach (string file in Directory.EnumerateFiles(directory, "*" + GraphExtension, SearchOption.TopDirectoryOnly))
        {
            if (file.EndsWith(GraphExtension, StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (string child in Directory.EnumerateDirectories(directory))
        {
            if (SkippedDirectories.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (string file in EnumerateGraphs(child))
            {
                yield return file;
            }
        }
    }

    private async Task<bool> ReportAsync(string graph, CancellationToken cancellationToken)
    {
        string display = Path.GetRelativePath(environment.CurrentDirectory, graph);
        var id = new DocumentId(Path.GetFileName(graph));
        IDocumentFormat? format = _formats.Find(id, DocumentKind.Class);
        if (format is null)
        {
            console.WriteLineRaw($"{display}: unreadable: no document format handles this file.");
            return false;
        }

        try
        {
            using var store = new FileSystemDocumentStore(Path.GetDirectoryName(graph) ?? environment.CurrentDirectory, Scheduler.Default,
                NullLogger<FileSystemDocumentStore>.Instance, watch: false);
            await using Stream input = await store.OpenReadAsync(id, cancellationToken).ConfigureAwait(false);
            await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
            console.WriteLineRaw($"{display}: schema {DocumentMigrator.CurrentSchemaVersion} (current)");
            return true;
        }
        catch (DocumentVersionException ex)
        {
            console.WriteLineRaw($"{display}: schema {ex.Found} is not supported (this tool supports {ex.Supported})");
            return false;
        }
        catch (Exception ex) when (ex is DocumentFormatException or IOException or DocumentNotFoundException or UnauthorizedAccessException)
        {
            console.WriteLineRaw($"{display}: unreadable: {ex.Message}");
            return false;
        }
    }
}
