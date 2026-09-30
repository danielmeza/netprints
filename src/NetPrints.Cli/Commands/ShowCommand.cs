using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints show &lt;graph&gt;</c>: prints the line-oriented summary of a graph (contracts/git.md §1), the text git diffs when
/// <c>git-install</c> configured it as the diff text conversion (FR-033).
/// </summary>
internal sealed class ShowCommand(IAnsiConsole console, CliEnvironment environment) : AsyncCommand<ShowSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "show";

    private readonly DocumentFormatRegistry _formats = GraphFormats.CreateRegistry();

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string path = Path.GetFullPath(settings.Graph, environment.CurrentDirectory);
        if (!File.Exists(path))
        {
            await environment.Error.WriteLineAsync($"'{path}' does not exist.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        // A file git hands over as a temporary path may lack the graph extension: fall back to the default format.
        var id = new DocumentId(Path.GetFileName(path));
        IDocumentFormat format = _formats.Find(id, DocumentKind.Class) ?? _formats.Default;
        try
        {
            ClassDocument document;
            await using (FileStream input = File.OpenRead(path))
            {
                document = await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
            }

            await console.Profile.Out.Writer.WriteAsync(GraphSummaryWriter.Write(document)).ConfigureAwait(false);
            return ExitCodes.Success;
        }
        catch (DocumentVersionException ex)
        {
            await environment.Error.WriteLineAsync($"{path}: schema {ex.Found} is not supported (this tool supports {ex.Supported})").ConfigureAwait(false);
            return ExitCodes.Failed;
        }
        catch (Exception ex) when (ex is DocumentFormatException or IOException or UnauthorizedAccessException)
        {
            await environment.Error.WriteLineAsync($"{path}: unreadable: {ex.Message}").ConfigureAwait(false);
            return ExitCodes.Failed;
        }
    }
}
