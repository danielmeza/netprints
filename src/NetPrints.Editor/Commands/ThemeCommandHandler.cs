using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Commands;

/// <summary>The <c>theme.dark</c>, <c>theme.light</c> and <c>theme.system</c> commands: switch the editor to a theme and keep the choice.</summary>
/// <param name="theme">The theme the command switches to.</param>
public sealed class ThemeCommandHandler(EditorTheme theme) : ICommandHandler
{
    /// <inheritdoc/>
    public bool CanExecute(CommandContext context) => true;

    /// <inheritdoc/>
    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) =>
        context.Shell.ProjectActions.SetThemeAsync(theme, cancellationToken);
}
