using NetPrints.Editor.NodeSearch.Providers;

namespace NetPrints.Editor.NodeSearch;

/// <summary>The editor's own search providers and the rules they share.</summary>
internal static class NodeSearchProviders
{
    /// <summary>The owner of a provider that serves the node kinds of the extension registry rather than one kind.</summary>
    public const string AnyKind = "*";

    /// <summary>Creates the providers in the fixed display order of the categories.</summary>
    /// <returns>A new list of the providers.</returns>
    public static IReadOnlyList<INodeSearchProvider> CreateDefault() =>
    [
        new MakeDelegateProvider(),
        new NodeKindsProvider(),
        new PinVariablesProvider(),
        new PinMethodsProvider(),
        new ThisVariablesProvider(),
        new ThisMethodsProvider(),
        new StaticMethodsProvider(),
        new StaticVariablesProvider(),
        new LocalVariablesProvider(),
        new TypesProvider(),
        new TypePinProvider(),
        new EventEntriesProvider(),
        new ExtensionNodesProvider(),
    ];

    /// <summary>Whether a node kind id comes from an extension (<c>manifest/Name</c>) rather than the editor.</summary>
    /// <param name="kind">The kind id.</param>
    /// <returns><see langword="true"/> for an extension's kind.</returns>
    public static bool IsExtensionKind(string kind) => kind.Contains('/', StringComparison.Ordinal);
}
