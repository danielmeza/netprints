using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NetPrints.Editor.Contributions;

/// <summary>The id rules and the built-in prefixes of contributions (ADR-0020).</summary>
public static partial class ContributionIds
{
    /// <summary>The owner recorded for built-in contributions.</summary>
    public const string Owner = "netprints";

    /// <summary>Prefix of built-in command ids.</summary>
    public const string CommandPrefix = "netprints.command.";

    /// <summary>Prefix of built-in panel ids.</summary>
    public const string PanelPrefix = "netprints.panel.";

    /// <summary>Prefix of built-in dashboard tile ids.</summary>
    public const string TilePrefix = "netprints.tile.";

    /// <summary>Prefix of built-in project template ids.</summary>
    public const string TemplatePrefix = "netprints.template.";

    /// <summary>Prefix of built-in context-menu item ids.</summary>
    public const string MenuPrefix = "netprints.menu.";

    /// <summary>Prefix of built-in tooltip provider ids.</summary>
    public const string TooltipPrefix = "netprints.tooltip.";

    /// <summary>Prefix of built-in go-to provider ids.</summary>
    public const string GoToPrefix = "netprints.goto.";

    /// <summary>Whether <paramref name="id"/> matches <c>^[a-z0-9]+(\.[a-zA-Z0-9]+)+$</c>.</summary>
    /// <param name="id">The candidate id; may be null.</param>
    /// <returns><see langword="true"/> for a valid id.</returns>
    public static bool IsValid([NotNullWhen(true)] string? id) => id is not null && IdPattern().IsMatch(id);

    [GeneratedRegex(@"^[a-z0-9]+(\.[a-zA-Z0-9]+)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdPattern();
}
