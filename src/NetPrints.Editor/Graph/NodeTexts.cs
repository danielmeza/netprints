using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph;

/// <summary>
/// The display text of node titles, pin labels, search category headers and suggestion names (FR-027, research R13): each is looked up as
/// a <see cref="LocalizedText"/> in the strings the extensions added, then in <see cref="CoreStrings.Resources"/>, for the UI culture,
/// and shows its default text when no resource has the key.
/// </summary>
internal static class NodeTexts
{
    private const string NodePrefix = "Node.";
    private const string TitleSuffix = ".Title";
    private const string PinInfix = ".Pin.";
    private const string CategoryPrefix = "Category.";

    private static readonly ConditionalWeakTable<ExtensionRegistry, ResourceManager[]> ResourcesByRegistry = [];

    /// <summary>Gets the text a node's header shows.</summary>
    /// <param name="node">The node.</param>
    /// <param name="registry">The registry whose strings and node kinds apply; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>The <c>Node.&lt;kind&gt;.Title</c> resource of a node with a fixed title; the node's own text otherwise.</returns>
    public static string Title(Node node, ExtensionRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(node);
        string text = node.ToString();
        return node.TitleIsFixed && KindOf(node, registry) is { } kind
            ? Resolve(NodePrefix + kind + TitleSuffix, text, registry)
            : text;
    }

    /// <summary>Gets the text a pin's label shows: its name translated, then what the pin's own text adds after the name (a data pin's type).</summary>
    /// <param name="pin">The pin.</param>
    /// <param name="registry">The registry whose strings and node kinds apply; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>The <c>Node.&lt;kind&gt;.Pin.&lt;direction&gt;.&lt;kind&gt;.&lt;key&gt;</c> resource, or the pin's own text when none has the key.</returns>
    public static string PinLabel(NodePin pin, ExtensionRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(pin);
        string shown = pin.ToString();
        if (KindOf(pin.Node, registry) is not { } kind || !shown.StartsWith(pin.Name, StringComparison.Ordinal))
        {
            return shown;
        }

        string label = Resolve(NodePrefix + kind + PinInfix + PinKeys.For(pin), pin.Name, registry);
        return string.Concat(label, shown.AsSpan(pin.Name.Length));
    }

    /// <summary>Gets the text a search category header shows.</summary>
    /// <param name="category">The category name entries are grouped by.</param>
    /// <param name="registry">The registry whose strings apply; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>The <c>Category.&lt;name in lower case, spaces as hyphens&gt;</c> resource, or the name when none has the key.</returns>
    public static string Category(string category, ExtensionRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(category);
        return Resolve(CategoryPrefix + category.ToLowerInvariant().Replace(' ', '-'), category, registry);
    }

    /// <summary>Gets the text a node suggestion's row shows.</summary>
    /// <param name="displayName">The suggestion's display name, which is also the key of its translation.</param>
    /// <param name="registry">The registry whose strings apply; <see langword="null"/> for the built-in strings alone.</param>
    /// <returns>The resource named <paramref name="displayName"/>, or the display name when none has the key.</returns>
    public static string Suggestion(string displayName, ExtensionRegistry? registry)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        return Resolve(displayName, displayName, registry);
    }

    private static string? KindOf(Node node, ExtensionRegistry? registry) =>
        registry?.NodeConverters.FindByNodeType(node.GetType())?.Kind;

    private static string Resolve(string key, string defaultText, ExtensionRegistry? registry) =>
        new LocalizedText(key, defaultText).Resolve(ResourcesOf(registry), CultureInfo.CurrentUICulture);

    private static ResourceManager[] ResourcesOf(ExtensionRegistry? registry) =>
        registry is null ? [CoreStrings.Resources] : ResourcesByRegistry.GetValue(registry, static current => [.. current.StringResources, CoreStrings.Resources]);
}
