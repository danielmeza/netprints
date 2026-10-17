using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace NetPrints.Core;

/// <summary>
/// A display text identified by a resource key (FR-027, research R13). It resolves through the owner's resources for
/// the UI culture, then its parent, then the neutral resources, then <paramref name="DefaultText"/>; with no default text
/// it shows the raw key.
/// </summary>
/// <param name="Key">The resource key, such as <c>Command.netprints.save.Label</c>.</param>
/// <param name="DefaultText">The text shown when no resource has the key, or null to show the key.</param>
public readonly record struct LocalizedText(string Key, string? DefaultText = null)
{
    /// <summary>Gets the text when no resource answers: the default text, or the raw key.</summary>
    public string Text => DefaultText ?? Key;

    /// <summary>Makes a text that is its own key and default, for a label that has no resource.</summary>
    /// <param name="text">The literal text.</param>
    /// <returns>The text.</returns>
    public static LocalizedText FromLiteral(string text) => new(text, text);

    /// <summary>Converts a literal to a text that is its own key and default.</summary>
    /// <param name="text">The literal text.</param>
    /// <returns>The text.</returns>
    public static implicit operator LocalizedText(string text) => FromLiteral(text);

    /// <summary>Resolves the text through one resource manager.</summary>
    /// <param name="resources">The owner's resources, or null for none.</param>
    /// <param name="culture">The UI culture, or null for the current UI culture.</param>
    /// <returns>The translated text, the default text or the raw key.</returns>
    public string Resolve(ResourceManager? resources, CultureInfo? culture)
        => resources is null ? Text : Resolve([resources], culture);

    /// <summary>Resolves the text through resource managers in order; the first that has the key answers.</summary>
    /// <param name="resources">The resources to try.</param>
    /// <param name="culture">The UI culture, or null for the current UI culture.</param>
    /// <returns>The translated text, the default text or the raw key.</returns>
    public string Resolve(IEnumerable<ResourceManager> resources, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(resources);

        foreach (ResourceManager manager in resources)
        {
            string? translated = Lookup(manager, culture ?? CultureInfo.CurrentUICulture);
            if (translated is not null)
            {
                return translated;
            }
        }

        return Text;
    }

    /// <inheritdoc/>
    public override string ToString() => Text;

    private string? Lookup(ResourceManager manager, CultureInfo culture)
    {
        try
        {
            return manager.GetString(Key, culture);
        }
        catch (MissingManifestResourceException)
        {
            return null;
        }
    }
}
