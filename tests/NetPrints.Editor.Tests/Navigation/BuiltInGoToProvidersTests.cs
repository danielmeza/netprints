using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;

namespace NetPrints.Editor.Tests.Navigation;

public sealed class BuiltInGoToProvidersTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private static async Task<List<GoToItem>> SearchAsync(IGoToProvider provider, string text)
    {
        List<GoToItem> items = [];
        await foreach (GoToItem item in provider.SearchAsync(text, TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        return items;
    }

    private ContributionRegistry RegistryFor(ProjectSessionViewModel? session)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        GoToProviderContributions.Register(registry, () => session);
        return registry;
    }

    [Fact]
    public async Task EachBuiltInKindIsRegisteredInOrder()
    {
        ContributionRegistry registry = RegistryFor(null);

        Assert.Equal([GoToKinds.Graphs, GoToKinds.Nodes, GoToKinds.Variables, GoToKinds.Methods, GoToKinds.Commands], registry.GoToProviders.Select(provider => provider.Kind));
        foreach (IGoToProvider provider in registry.GoToProviders.Where(provider => provider.Kind != GoToKinds.Commands))
        {
            Assert.Empty(await SearchAsync(provider, "a"));
        }
    }

    [Fact]
    public async Task GraphsMethodsAndVariablesOfTheOpenProjectAreFoundByNameWithTheirDocument()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph method = cls.Methods.First();
        ContributionRegistry registry = RegistryFor(session);
        IGoToProvider methods = registry.GoToProviders.Single(provider => provider.Kind == GoToKinds.Methods);
        IGoToProvider graphs = registry.GoToProviders.Single(provider => provider.Kind == GoToKinds.Graphs);

        GoToItem found = (await SearchAsync(methods, method.Name)).First(item => item.Title == method.Name);

        Assert.Equal(GoToKinds.Methods, found.Kind);
        Assert.Equal(cls.FullName, found.Detail);
        Assert.Equal(DocumentId.Graph(session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), found.Target?.Document);
        Assert.Null(found.Target?.NodeId);
        Assert.Contains(await SearchAsync(graphs, cls.Name), item => item.Target?.Document == DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey));
        Assert.Empty(await SearchAsync(methods, "zzzNoSuchMethod"));
    }

    [Fact]
    public async Task NodesCarryTheirGraphAndNodeIdAndNeedSomeText()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph method = cls.Methods.First();
        Node node = method.Nodes.First();
        IGoToProvider provider = RegistryFor(session).GoToProviders.Single(p => p.Kind == GoToKinds.Nodes);

        GoToItem found = (await SearchAsync(provider, node.Name)).First(item => item.Target?.NodeId == node.Id);

        Assert.Equal(DocumentId.Graph(session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + method.Id), found.Target?.Document);
        Assert.Empty(await SearchAsync(provider, ""));
    }

    [Fact]
    public async Task VariablesOpenTheirGetterGraphOrTheClassGraph()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var variable = new Variable(cls, "Counter", TypeSpecifier.FromType<int>(), null, null, VariableModifiers.None);
        cls.Variables.Add(variable);
        IGoToProvider provider = RegistryFor(session).GoToProviders.Single(p => p.Kind == GoToKinds.Variables);

        GoToItem found = Assert.Single(await SearchAsync(provider, "count"));

        Assert.Equal("Counter", found.Title);
        Assert.Equal(DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey), found.Target?.Document);
    }

    [Fact]
    public async Task CommandsAreFoundByLabelAndCarryTheirId()
    {
        ContributionRegistry registry = RegistryFor(null);
        IGoToProvider provider = registry.GoToProviders.Single(p => p.Kind == GoToKinds.Commands);

        GoToItem save = (await SearchAsync(provider, "save all")).Single();

        Assert.Equal(ContributionIds.CommandPrefix + "saveAll", save.CommandId);
        Assert.Null(save.Target);
        Assert.Equal("File", save.Detail.Split(',')[0].Trim());
    }
}
