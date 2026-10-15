using System.Text;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>The shortcut table of <c>docs/guide/editor/commands-and-shortcuts.md</c>, generated from the command registry.</summary>
internal static class ShortcutGuide
{
    public const string Begin = "<!-- shortcuts:begin -->";

    public const string End = "<!-- shortcuts:end -->";

    private const string NoShortcut = "–";
    private const string Anywhere = "Anywhere";
    private const string GraphCanvas = "Graph canvas";
    private const string ProjectTree = "Project tree";
    private const string SubmenuSeparator = " › ";

    /// <summary>Builds the table, one row for each registered command, in the order of the keyboard shortcuts sheet.</summary>
    /// <param name="registry">The registry whose commands are listed.</param>
    /// <returns>The Markdown table without a trailing newline.</returns>
    public static string Generate(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Dictionary<string, CommandDescriptor> commands = registry.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);
        StringBuilder table = new();
        table.Append("| Menu | Command | Shortcut | Works in |\n|---|---|---|---|");
        foreach (ShortcutGroup group in new KeyboardShortcutsViewModel(registry).Groups)
        {
            foreach (ShortcutRow row in group.Rows)
            {
                CommandDescriptor command = commands[row.CommandId];
                string name = command.Menu?.Submenu is { } submenu ? submenu + SubmenuSeparator + row.Label : row.Label;
                string shortcuts = row.Shortcuts.Length == 0 ? NoShortcut : row.Shortcuts;
                table.Append($"\n| {group.Title} | {name} | {shortcuts} | {WorksIn(command.Scope)} |");
            }
        }

        return table.ToString();
    }

    /// <summary>Gets the table between the markers of a guide.</summary>
    /// <param name="guide">The guide's text, with <c>\n</c> line ends.</param>
    /// <returns>The table without a trailing newline.</returns>
    public static string TableOf(string guide)
    {
        (int start, int end) = Span(guide);
        return guide[start..end].Trim('\n');
    }

    /// <summary>Puts a table between the markers of a guide.</summary>
    /// <param name="guide">The guide's text, with <c>\n</c> line ends.</param>
    /// <param name="table">The table.</param>
    /// <returns>The guide with its table replaced and the rest untouched.</returns>
    public static string Replace(string guide, string table)
    {
        (int start, int end) = Span(guide);
        return guide[..start] + "\n\n" + table + "\n\n" + guide[end..];
    }

    private static string WorksIn(CommandScope scope)
    {
        List<string> places = [];
        if (scope.HasFlag(CommandScope.Graph))
        {
            places.Add(GraphCanvas);
        }

        if (scope.HasFlag(CommandScope.ProjectTree))
        {
            places.Add(ProjectTree);
        }

        return places.Count == 0 ? Anywhere : string.Join(", ", places);
    }

    private static (int Start, int End) Span(string guide)
    {
        int begin = guide.IndexOf(Begin, StringComparison.Ordinal);
        int end = guide.IndexOf(End, StringComparison.Ordinal);
        Assert.True(begin >= 0 && end > begin, $"The guide needs {Begin} above {End}.");
        return (begin + Begin.Length, end);
    }
}
