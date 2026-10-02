using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>showPanel.&lt;panel&gt;</c> commands.</summary>
/// <param name="panelId">The panel to show.</param>
public sealed class ShowPanelCommandHandler(string panelId) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        context.Shell.ShowPanel(panelId);
        return Task.CompletedTask;
    }
}
