using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>nextTab</c> and <c>previousTab</c> commands.</summary>
/// <param name="step">1 for the next tab, -1 for the previous one.</param>
public sealed class CycleTabCommandHandler(int step) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => context.Shell.OpenDocuments.Count > 1;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<DocumentId> open = context.Shell.OpenDocuments;
        if (open.Count > 1)
        {
            int current = context.ActiveDocument is { } active ? open.ToList().IndexOf(active) : -1;
            int target = current < 0 ? (step > 0 ? 0 : open.Count - 1) : (current + step + open.Count) % open.Count;
            context.Shell.ActivateDocument(open[target]);
        }

        return Task.CompletedTask;
    }
}
