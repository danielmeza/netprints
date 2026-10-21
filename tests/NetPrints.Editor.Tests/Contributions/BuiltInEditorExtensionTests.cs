using System.Text;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Search;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>FR-021: the built-in editor registers through the editor facet as <c>netprints.editor</c>, before any other extension, and the registry holds what P3a's held.</summary>
public class BuiltInEditorExtensionTests
{
    private static ContributionRegistry Registered()
    {
        var registry = new ContributionRegistry(new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        registry.Freeze();
        return registry;
    }

    [Fact]
    public void TheBuiltInRegistryHoldsWhatP3aHeld()
    {
        ContributionRegistry registry = Registered();
        var text = new StringBuilder();
        foreach (CommandDescriptor command in registry.Commands)
        {
            text.Append("command ").Append(command.Id).Append(" | ")
                .Append(string.Join(", ", (command.DefaultGestures ?? []).Select(gesture => gesture.ToString()))).Append(" | ")
                .Append(command.Scope).Append(" | ")
                .Append(command.Menu is null ? "-" : $"{command.Menu.Path}/{command.Menu.Group}/{command.Menu.Order}/{command.Menu.Submenu}").Append(" | ")
                .Append(command.CommandBarOrder?.ToString() ?? "-").AppendLine();
        }

        foreach (PanelDescriptor panel in registry.Panels)
        {
            text.Append("panel ").Append(panel.Id).Append(" | ").Append(panel.DefaultDock).Append(" | ").Append(panel.Order).AppendLine();
        }

        foreach (DashboardTileDescriptor tile in registry.DashboardTiles)
        {
            text.Append("tile ").Append(tile.Id).Append(" | ").Append(tile.Order).AppendLine();
        }

        foreach (ProjectTemplateDescriptor template in registry.ProjectTemplates)
        {
            text.Append("template ").Append(template.Id).Append(" | ").Append(template.ProfileId).AppendLine();
        }

        foreach (ContextMenuItemDescriptor item in registry.ContextMenuItems)
        {
            text.Append("menu ").Append(item.Id).Append(" | ").Append(item.Target).Append(" | ").Append(item.CommandId).Append(" | ").Append(item.Group).Append(" | ").Append(item.Order).AppendLine();
        }

        foreach (ITooltipProvider provider in registry.TooltipProviders)
        {
            text.Append("tooltip ").Append(provider.GetType().Name).Append(" | ").Append(provider.Order).AppendLine();
        }

        Assert.Empty(registry.Issues);
        SearchGolden.Check("Contributions", "builtin-registry", text.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void EveryBuiltInContributionIsOwnedByTheEditorExtension()
    {
        ContributionRegistry registry = Registered();

        string[] ids =
        [
            .. registry.Commands.Select(command => command.Id),
            .. registry.Panels.Select(panel => panel.Id),
            .. registry.DashboardTiles.Select(tile => tile.Id),
            .. registry.ContextMenuItems.Select(item => item.Id),
        ];

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.Equal(BuiltInContributions.OwnerId, registry.OwnerOf(id)));
        Assert.Equal("netprints.editor", BuiltInContributions.OwnerId);
    }

    [Fact]
    public void TheBuiltInExtensionRegistersThroughTheFacetBeforeAnyOtherExtension()
    {
        var registry = new ContributionRegistry(new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        int builtIn = registry.Commands.Count;

        registry.For("fx.ext").AddCommand(new CommandDescriptor("fx.ext.hello", "Hello", registry.Commands[0].Handler));

        Assert.Equal("fx.ext.hello", registry.Commands[builtIn].Id);
        Assert.Equal("fx.ext", registry.OwnerOf("fx.ext.hello"));
        Assert.Equal(BuiltInContributions.OwnerId, registry.OwnerOf(registry.Commands[0].Id));
    }

    [Fact]
    public void TheBuiltInViewsAreAddedAsViewResources()
    {
        ContributionRegistry registry = Registered();

        ViewResourceDeclaration views = Assert.Single(registry.ViewResources);

        Assert.Equal(BuiltInContributions.OwnerId, views.Owner);
        Assert.Equal(BuiltInContributions.BuiltInViewsUri, views.Uri);
    }
}
