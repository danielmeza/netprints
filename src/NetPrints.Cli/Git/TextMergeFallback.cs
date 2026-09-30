using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Cli.Git;

/// <summary>The outcome of a text merge.</summary>
/// <param name="Content">The merged bytes, with conflict markers where the versions disagree; empty when the merge did not run.</param>
/// <param name="Error">Why <c>git merge-file</c> could not merge, or <see langword="null"/> when it did.</param>
internal sealed record TextMergeResult(byte[] Content, string? Error);

/// <summary>Merges three versions of a file as text with <c>git merge-file</c>, byte for byte, leaving conflict markers where they disagree (contracts/git.md §2 step 5).</summary>
/// <param name="processes">Runs <c>git</c>.</param>
internal sealed class TextMergeFallback(IProcessRunner processes)
{
    private const int MaxConflictCount = 127;

    /// <summary>Merges the three versions.</summary>
    /// <param name="ours">The current branch's content.</param>
    /// <param name="baseContent">The common ancestor's content.</param>
    /// <param name="theirs">The other branch's content.</param>
    /// <param name="markerSize">The length of the conflict markers.</param>
    /// <param name="cancellationToken">Cancels the merge.</param>
    /// <returns>The merged bytes, or the reason <c>git merge-file</c> failed.</returns>
    public async Task<TextMergeResult> MergeAsync(byte[] ours, byte[] baseContent, byte[] theirs, int markerSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ours);
        ArgumentNullException.ThrowIfNull(baseContent);
        ArgumentNullException.ThrowIfNull(theirs);
        string directory = Directory.CreateTempSubdirectory("netprints-merge-").FullName;
        try
        {
            string oursFile = Path.Combine(directory, "ours");
            string baseFile = Path.Combine(directory, "base");
            string theirsFile = Path.Combine(directory, "theirs");
            await File.WriteAllBytesAsync(oursFile, ours, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(baseFile, baseContent, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(theirsFile, theirs, cancellationToken).ConfigureAwait(false);

            var request = new ProcessStartRequest(
                "git",
                ["merge-file", "--marker-size", markerSize.ToString(CultureInfo.InvariantCulture), "-L", "ours", "-L", "base", "-L", "theirs", oursFile, baseFile, theirsFile],
                directory);
            ProcessResult result = await processes.RunAsync(request, cancellationToken).ConfigureAwait(false);
            return result.ExitCode is >= 0 and <= MaxConflictCount
                ? new TextMergeResult(await File.ReadAllBytesAsync(oursFile, cancellationToken).ConfigureAwait(false), null)
                : new TextMergeResult([], $"git merge-file failed with exit code {result.ExitCode}: {result.StandardError.Trim()}");
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            return new TextMergeResult([], $"git merge-file could not run: {ex.Message}");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
