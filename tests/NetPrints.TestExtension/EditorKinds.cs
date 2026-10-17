using System.Text.Json.Nodes;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Migrations;

namespace NetPrints.TestExtension;

/// <summary>The ids of the editor contributions of the test extension, one per kind.</summary>
public static class TestEditorIds
{
    /// <summary>The command.</summary>
    public const string Command = "netprints.test.hello";

    /// <summary>The panel.</summary>
    public const string Panel = "netprints.test.panel";

    /// <summary>The start page tile.</summary>
    public const string Tile = "netprints.test.tile";

    /// <summary>The project template.</summary>
    public const string Template = "netprints.test.template";

    /// <summary>The context menu item.</summary>
    public const string ContextMenuItem = "netprints.test.menu";

    /// <summary>The status bar item.</summary>
    public const string StatusBarItem = "netprints.test.status";

    /// <summary>The settings page.</summary>
    public const string SettingsPage = "netprints.test.settings";

    /// <summary>The dialog.</summary>
    public const string Dialog = "netprints.test.dialog";

    /// <summary>The icon the command uses, with the geometry in <see cref="ViewResourcesUri"/>.</summary>
    public const string Icon = "netprints.test.mark";

    /// <summary>The node search provider.</summary>
    public const string SuggestionProvider = "netprints.test.rows";

    /// <summary>The text a go-to query must contain for the go-to provider to answer.</summary>
    public const string GoToQuery = "hello";

    /// <summary>The subject of a pin tooltip target the tooltip provider answers for.</summary>
    public const string TooltipSubject = "netprints.test.pin";

    /// <summary>The URI of the dictionary that holds the geometry of <see cref="Icon"/>.</summary>
    public const string ViewResourcesUri = "avares://NetPrints.TestExtension/TestIcons.axaml";
}

/// <summary>What a panel, tile, status bar item or dialog of the test extension shows.</summary>
/// <param name="Text">The text the contribution shows.</param>
public sealed record TestContent(string Text);

/// <summary>A command that does nothing.</summary>
public sealed class TestCommandHandler : ICommandHandler
{
    /// <inheritdoc />
    public bool CanExecute(ICommandContext context) => true;

    /// <inheritdoc />
    public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Answers a tooltip for the pin subject <see cref="TestEditorIds.TooltipSubject"/>.</summary>
public sealed class TestTooltipProvider : ITooltipProvider
{
    /// <inheritdoc />
    public int Order => -1;

    /// <inheritdoc />
    public TooltipContent? TryProvide(TooltipTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Kind == TooltipTargetKind.Pin && target.Subject is TestEditorIds.TooltipSubject
            ? new TooltipContent("Test pin", ["from the test extension"])
            : null;
    }
}

/// <summary>Finds the command of the test extension when the query contains <see cref="TestEditorIds.GoToQuery"/>.</summary>
public sealed class TestGoToProvider : IGoToProvider
{
    /// <inheritdoc />
    public string Kind => "Test";

    /// <inheritdoc />
    public async IAsyncEnumerable<GoToItem> SearchAsync(string text, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        await Task.CompletedTask.ConfigureAwait(false);
        if (text.Contains(TestEditorIds.GoToQuery, StringComparison.OrdinalIgnoreCase))
        {
            yield return new GoToItem(Kind, "Test hello", "from the test extension", null, TestEditorIds.Command);
        }
    }
}

/// <summary>Offers one row to node search.</summary>
public sealed class TestSuggestionProvider : INodeSuggestionProvider
{
    /// <inheritdoc />
    public string Id => TestEditorIds.SuggestionProvider;

    /// <inheritdoc />
    public IEnumerable<NodeSearchEntry> Suggest(NodeSearchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.GraphKind == GraphKinds.Constructor ? [new TestSearchEntry()] : [];
    }

    private sealed record TestSearchEntry() : NodeSearchEntry("Test", "Test row", "Method")
    {
        public override Task<Node?> CreateAsync(INodeCreationContext context, GraphPoint position, NodePin? pin) => Task.FromResult<Node?>(null);
    }
}

/// <summary>Upgrades <c>netprints.test/Legacy</c> nodes from version 0 by marking them with <see cref="MigratedMember"/>.</summary>
public sealed class TestNodeMigration : INodeDataMigration
{
    /// <summary>The member the migration adds to a node.</summary>
    public const string MigratedMember = "migratedByTest";

    /// <inheritdoc />
    public string NodeKind => LegacyNodeConverter.KindId;

    /// <inheritdoc />
    public int FromVersion => 0;

    /// <inheritdoc />
    public void Migrate(JsonObject node)
    {
        ArgumentNullException.ThrowIfNull(node);
        node[MigratedMember] = true;
    }
}
