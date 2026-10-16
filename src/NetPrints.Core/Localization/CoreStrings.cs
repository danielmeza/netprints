using System.Resources;
using NetPrints.Graph;

namespace NetPrints.Core;

/// <summary>The neutral resources of the built-in node kinds: titles, pin labels and the built-in category (FR-027).</summary>
public static class CoreStrings
{
    /// <summary>Gets the resources that hold the <c>Node.&lt;kind&gt;.*</c> and <c>Category.&lt;id&gt;</c> keys of Core.</summary>
    public static ResourceManager Resources => NodeStrings.ResourceManager;
}
