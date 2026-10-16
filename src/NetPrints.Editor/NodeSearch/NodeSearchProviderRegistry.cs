using System.Runtime.CompilerServices;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.NodeSearch;

/// <summary>Builds the providers once for each extension registry and hands the same list out for every search opened under it.</summary>
internal sealed class NodeSearchProviderRegistry
{
    private readonly Func<IReadOnlyList<INodeSearchProvider>> factory;
    private readonly ConditionalWeakTable<ExtensionRegistry, IReadOnlyList<INodeSearchProvider>> built = new();

    /// <summary>Initializes a new instance of the <see cref="NodeSearchProviderRegistry"/> class.</summary>
    /// <param name="factory">Creates the providers in display order; called once for each extension registry.</param>
    public NodeSearchProviderRegistry(Func<IReadOnlyList<INodeSearchProvider>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        this.factory = factory;
    }

    /// <summary>Gets the registry of the editor's own providers.</summary>
    public static NodeSearchProviderRegistry Default { get; } = new(NodeSearchProviders.CreateDefault);

    /// <summary>Gets the providers for an extension registry.</summary>
    /// <param name="registry">The registry in use.</param>
    /// <returns>The providers in display order, built on the first call for <paramref name="registry"/>.</returns>
    public IReadOnlyList<INodeSearchProvider> For(ExtensionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return built.GetValue(registry, _ => factory());
    }
}
