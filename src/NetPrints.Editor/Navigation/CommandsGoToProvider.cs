using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Navigation;

/// <summary>The go-to provider of the registered commands, searched after a leading <see cref="GoToKinds.CommandMarker"/>.</summary>
/// <param name="registry">The registry whose commands are listed; read when a search runs.</param>
public sealed class CommandsGoToProvider(IContributionRegistry registry) : IGoToProvider
{
    /// <inheritdoc/>
    public string Kind => GoToKinds.Commands;

    /// <inheritdoc/>
    public IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        string query = text.Trim();
        return registry.Commands
            .Where(command => MatchRanking.Rank(command.Label, query) != MatchRanking.NoMatch)
            .Select(command => new GoToItem(Kind, command.Label, Detail(command), null, command.Id))
            .ToAsyncEnumerable();
    }

    private static string Detail(CommandDescriptor command)
    {
        string shortcuts = string.Join(", ", command.DefaultGestures ?? []);
        string menu = command.Menu?.Path ?? "";
        return menu.Length > 0 && shortcuts.Length > 0 ? menu + ", " + shortcuts : menu + shortcuts;
    }
}
