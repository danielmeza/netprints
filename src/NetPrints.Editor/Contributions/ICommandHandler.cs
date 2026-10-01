using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions;

/// <summary>The behaviour behind a command.</summary>
public interface ICommandHandler
{
    /// <summary>Whether the command can run in <paramref name="context"/>.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns><see langword="true"/> when <see cref="ExecuteAsync"/> may be called.</returns>
    bool CanExecute(CommandContext context);

    /// <summary>Runs the command.</summary>
    /// <param name="context">The invocation context.</param>
    /// <param name="cancellationToken">Cancels a long-running command.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken);

    /// <summary>A label that replaces the descriptor's, such as "Undo Add node".</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>The label, or null to keep the descriptor's.</returns>
    string? DynamicLabel(CommandContext context) => null;
}
