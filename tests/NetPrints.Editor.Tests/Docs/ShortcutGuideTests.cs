using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Tests.Architecture;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>The shortcut table of the commands guide is the one the command registry generates (FR-103, SC-010).</summary>
public sealed class ShortcutGuideTests
{
    /// <summary>The variable that rewrites the table of the guide from the registry.</summary>
    public const string UpdateVariable = "NETPRINTS_UPDATE_SNAPSHOTS";

    private const string Header = "| Menu | Command | Shortcut | Works in |";

    private static string GuidePath => Path.Combine(RepositoryPaths.Root(), "docs", "guide", "editor", "commands-and-shortcuts.md");

    private static ContributionRegistry Registered()
    {
        var registry = new ContributionRegistry(new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        return registry;
    }

    [Fact]
    public void TheTableInTheGuideIsTheOneGeneratedFromTheRegistry()
    {
        string table = ShortcutGuide.Generate(Registered());
        string guide = File.ReadAllText(GuidePath).ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            guide = ShortcutGuide.Replace(guide, table);
            File.WriteAllText(GuidePath, guide);
        }

        Assert.Equal(table, ShortcutGuide.TableOf(guide));
    }

    [Fact]
    public void TheTableHasARowForEveryCommandUnderTheColumnsOfTheGuide()
    {
        ContributionRegistry registry = Registered();

        string[] lines = ShortcutGuide.Generate(registry).Split('\n');

        Assert.Equal(Header, lines[0]);
        Assert.Equal("|---|---|---|---|", lines[1]);
        Assert.Equal(registry.Commands.Count, lines.Length - 2);
        foreach (CommandDescriptor command in registry.Commands)
        {
            Assert.Contains(lines.Skip(2), line => line.Split(" | ")[1].EndsWith(command.Label, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void ARowInTheMenuOrderShowsItsShortcutsAndWhereTheyWork()
    {
        string table = ShortcutGuide.Generate(Registered());

        Assert.Contains("| Edit | Redo | Ctrl+Y, Ctrl+Shift+Z | Anywhere |", table, StringComparison.Ordinal);
        Assert.Contains("| Edit | Delete | Delete | Graph canvas, Project tree |", table, StringComparison.Ordinal);
        Assert.Contains("| View | Theme › Dark | – | Anywhere |", table, StringComparison.Ordinal);
        Assert.Contains("| Other | Open | Enter | Project tree |", table, StringComparison.Ordinal);
    }

    [Fact]
    public void AStaleRowIsADifferenceAndReplacingTheTableTouchesNothingElse()
    {
        string table = ShortcutGuide.Generate(Registered());
        string guide = $"Before\n\n{ShortcutGuide.Begin}\n\n{table}\n\n{ShortcutGuide.End}\n\nAfter\n";
        string stale = guide.Replace("| File | Save | Ctrl+S | Anywhere |", "| File | Save | Ctrl+Alt+S | Anywhere |", StringComparison.Ordinal);

        Assert.NotEqual(table, ShortcutGuide.TableOf(stale));
        Assert.Equal(table, ShortcutGuide.TableOf(guide));
        Assert.Equal(guide, ShortcutGuide.Replace(stale, table));
    }
}
