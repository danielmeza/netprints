using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>
/// <c>netprints merge &lt;base&gt; &lt;ours&gt; &lt;theirs&gt;</c>: the git merge driver for graphs (contracts/git.md §2, FR-034). It merges by node, pin and
/// member identity and writes the canonical result over <c>ours</c>; when that is not possible it merges the texts with conflict markers and exits 1.
/// </summary>
internal sealed class MergeCommand(CliEnvironment environment, IProcessRunner processes) : AsyncCommand<MergeSettings>
{
    /// <summary>The command name.</summary>
    public const string Name = "merge";

    private readonly DocumentFormatRegistry _formats = GraphFormats.CreateRegistry();

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, MergeSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string baseFile = Path.GetFullPath(settings.Base, environment.CurrentDirectory);
        string oursFile = Path.GetFullPath(settings.Ours, environment.CurrentDirectory);
        string theirsFile = Path.GetFullPath(settings.Theirs, environment.CurrentDirectory);
        foreach (string file in new[] { baseFile, oursFile, theirsFile })
        {
            if (!File.Exists(file))
            {
                await environment.Error.WriteLineAsync($"'{file}' does not exist.").ConfigureAwait(false);
                return ExitCodes.Usage;
            }
        }

        if (settings.MarkerSize < 1)
        {
            await environment.Error.WriteLineAsync($"--marker-size must be at least 1, not {settings.MarkerSize}.").ConfigureAwait(false);
            return ExitCodes.Usage;
        }

        byte[] baseBytes = await File.ReadAllBytesAsync(baseFile, cancellationToken).ConfigureAwait(false);
        byte[] oursBytes = await File.ReadAllBytesAsync(oursFile, cancellationToken).ConfigureAwait(false);
        byte[] theirsBytes = await File.ReadAllBytesAsync(theirsFile, cancellationToken).ConfigureAwait(false);

        string display = settings.Path ?? settings.Ours;
        var id = new DocumentId(System.IO.Path.GetFileName(display));
        IDocumentFormat format = _formats.Find(id, DocumentKind.Class) ?? _formats.Default;

        ClassDocument baseDocument;
        ClassDocument oursDocument;
        ClassDocument theirsDocument;
        try
        {
            baseDocument = await ReadAsync(format, baseBytes, id, cancellationToken).ConfigureAwait(false);
            oursDocument = await ReadAsync(format, oursBytes, id, cancellationToken).ConfigureAwait(false);
            theirsDocument = await ReadAsync(format, theirsBytes, id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DocumentFormatException or DocumentVersionException)
        {
            await environment.Error.WriteLineAsync($"{display}: unreadable: {ex.Message}; merging as text").ConfigureAwait(false);
            return await FallbackAsync(oursFile, oursBytes, baseBytes, theirsBytes, settings.MarkerSize, cancellationToken).ConfigureAwait(false);
        }

        MergeOutcome outcome = await new GraphMerger(format).MergeAsync(baseDocument, oursDocument, theirsDocument, cancellationToken).ConfigureAwait(false);
        if (outcome is MergeOutcome.Clean clean)
        {
            await File.WriteAllBytesAsync(oursFile, await WriteAsync(format, clean.Document, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            return ExitCodes.Success;
        }

        if (outcome is MergeOutcome.Conflicted conflicted)
        {
            foreach (MergeConflict conflict in conflicted.Conflicts)
            {
                await environment.Error.WriteLineAsync($"{display}: conflict: {conflict.Kind} at {conflict.Path}").ConfigureAwait(false);
            }
        }

        byte[] canonicalBase = await WriteAsync(format, baseDocument, cancellationToken).ConfigureAwait(false);
        byte[] canonicalOurs = await WriteAsync(format, oursDocument, cancellationToken).ConfigureAwait(false);
        byte[] canonicalTheirs = await WriteAsync(format, theirsDocument, cancellationToken).ConfigureAwait(false);
        return await FallbackAsync(oursFile, canonicalOurs, canonicalBase, canonicalTheirs, settings.MarkerSize, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ClassDocument> ReadAsync(IDocumentFormat format, byte[] content, DocumentId id, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(content, writable: false);
        return await format.ReadClassAsync(stream, id, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> WriteAsync(IDocumentFormat format, ClassDocument document, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await format.WriteClassAsync(document, stream, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    private async Task<int> FallbackAsync(string oursFile, byte[] ours, byte[] baseContent, byte[] theirs, int markerSize, CancellationToken cancellationToken)
    {
        TextMergeResult result = await new TextMergeFallback(processes).MergeAsync(ours, baseContent, theirs, markerSize, cancellationToken).ConfigureAwait(false);
        if (result.Error is null)
        {
            await File.WriteAllTextAsync(oursFile, result.Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await environment.Error.WriteLineAsync(result.Error).ConfigureAwait(false);
        }

        return ExitCodes.Failed;
    }
}
