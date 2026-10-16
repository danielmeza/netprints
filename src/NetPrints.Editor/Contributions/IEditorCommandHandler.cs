using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions;

/// <summary>
/// A built-in command handler: it works on the editor's own <see cref="CommandContext"/> (view models and the shell services) and
/// reaches the typed <see cref="ICommandHandler"/> contract through the explicit members below.
/// </summary>
internal interface IEditorCommandHandler : ICommandHandler
{
    /// <summary>Whether the command can run in <paramref name="context"/>.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns><see langword="true"/> when <see cref="ExecuteAsync(CommandContext, CancellationToken)"/> may be called.</returns>
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

    /// <summary>Whether the command is the chosen one of a group of choices.</summary>
    /// <param name="context">The invocation context.</param>
    /// <returns>Whether it is chosen, or null when the command is not a choice.</returns>
    bool? IsChecked(CommandContext context) => null;

    bool ICommandHandler.CanExecute(ICommandContext context) => CanExecute(CommandContext.From(context));

    Task ICommandHandler.ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) =>
        ExecuteAsync(CommandContext.From(context), cancellationToken);

    string? ICommandHandler.DynamicLabel(ICommandContext context) => DynamicLabel(CommandContext.From(context));

    bool? ICommandHandler.IsChecked(ICommandContext context) => IsChecked(CommandContext.From(context));
}
