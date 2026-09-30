using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NetPrints.Catalog;

/// <summary>
/// Reads catalog profile files (<c>*.npprofile.json</c>) with a minimal JSON reader (no System.Text.Json), so the
/// source generator can use it too. Unknown properties are ignored; every defect, including a newer
/// <c>schemaVersion</c>, is reported as NPC003.
/// </summary>
public static class ProfileJson
{
    private const string SchemaVersionProperty = "schemaVersion";

    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9._-]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Reads a profile from JSON text.</summary>
    /// <param name="json">The profile text.</param>
    /// <returns>The profile.</returns>
    /// <exception cref="CatalogFormatException">The text is malformed, invalid or its schema version is newer than supported (NPC003).</exception>
    public static CatalogProfile Parse(string json)
    {
        Guard.NotNull(json, nameof(json));

        object? root;
        try
        {
            root = MiniJson.Parse(json);
        }
        catch (FormatException exception)
        {
            throw Invalid(exception.Message, exception);
        }

        if (root is not Dictionary<string, object?> properties)
        {
            throw Invalid("the profile must be a JSON object");
        }

        int schemaVersion = ReadSchemaVersion(properties);
        if (schemaVersion > CatalogProfile.CurrentSchemaVersion)
        {
            throw Invalid(string.Format(
                CultureInfo.InvariantCulture,
                "schemaVersion {0} is newer than the supported version {1}",
                schemaVersion,
                CatalogProfile.CurrentSchemaVersion));
        }

        return new CatalogProfile(
            ReadId(properties),
            ReadBase(properties),
            ReadStrings(properties, "includeNamespaces"),
            ReadStrings(properties, "excludeNamespaces"),
            ReadStrings(properties, "includeTypes"),
            ReadStrings(properties, "excludeTypes"),
            ReadRules(properties, "typeAttributes"),
            ReadRules(properties, "memberAttributes"),
            ReadObsolete(properties),
            schemaVersion);
    }

    private static CatalogFormatException Invalid(string message, Exception? inner = null) =>
        new(CatalogDiagnosticCodes.InvalidProfileFile, "Invalid catalog profile: " + message + ".", inner);

    private static int ReadSchemaVersion(Dictionary<string, object?> properties)
    {
        if (!properties.TryGetValue(SchemaVersionProperty, out object? value))
        {
            return CatalogProfile.CurrentSchemaVersion;
        }

        if (value is MiniJsonNumber number && int.TryParse(number.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int version) && version >= 1)
        {
            return version;
        }

        throw Invalid("schemaVersion must be a positive integer");
    }

    private static string ReadId(Dictionary<string, object?> properties)
    {
        if (!properties.TryGetValue("id", out object? value) || value is not string id || !IdPattern.IsMatch(id))
        {
            throw Invalid("id is required and must match [a-z0-9][a-z0-9._-]*");
        }

        if (id == CatalogProfile.PublicApiId || id == CatalogProfile.AnnotatedId)
        {
            throw Invalid("the id '" + id + "' is reserved for a built-in profile");
        }

        return id;
    }

    private static CatalogProfileBase ReadBase(Dictionary<string, object?> properties)
    {
        switch (ReadOptionalString(properties, "base"))
        {
            case null:
            case "public-api":
                return CatalogProfileBase.PublicApi;
            case "annotated":
                return CatalogProfileBase.Annotated;
            case "none":
                return CatalogProfileBase.None;
            default:
                throw Invalid("base must be public-api, annotated or none");
        }
    }

    private static CatalogObsoleteMode ReadObsolete(Dictionary<string, object?> properties)
    {
        switch (ReadOptionalString(properties, "obsolete"))
        {
            case null:
            case "excludeErrors":
                return CatalogObsoleteMode.ExcludeErrors;
            case "include":
                return CatalogObsoleteMode.Include;
            case "exclude":
                return CatalogObsoleteMode.Exclude;
            default:
                throw Invalid("obsolete must be include, exclude or excludeErrors");
        }
    }

    private static string? ReadOptionalString(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out object? value) || value is null)
        {
            return null;
        }

        return value as string ?? throw Invalid(name + " must be a string");
    }

    private static IReadOnlyList<string>? ReadStrings(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out object? value) || value is null)
        {
            return null;
        }

        if (value is not List<object?> items)
        {
            throw Invalid(name + " must be an array of strings");
        }

        List<string> result = new(items.Count);
        foreach (object? item in items)
        {
            result.Add(item as string ?? throw Invalid(name + " must be an array of strings"));
        }

        return result;
    }

    private static IReadOnlyList<CatalogAttributeRule>? ReadRules(Dictionary<string, object?> properties, string name)
    {
        if (!properties.TryGetValue(name, out object? value) || value is null)
        {
            return null;
        }

        if (value is not List<object?> items)
        {
            throw Invalid(name + " must be an array of rules");
        }

        List<CatalogAttributeRule> result = new(items.Count);
        foreach (object? item in items)
        {
            result.Add(ReadRule(item as Dictionary<string, object?> ?? throw Invalid(name + " must hold rule objects"), name));
        }

        return result;
    }

    private static CatalogAttributeRule ReadRule(Dictionary<string, object?> rule, string listName)
    {
        CatalogAttributeRuleKind kind;
        switch (ReadOptionalString(rule, "rule"))
        {
            case "require":
                kind = CatalogAttributeRuleKind.Require;
                break;
            case "exclude":
                kind = CatalogAttributeRuleKind.Exclude;
                break;
            default:
                throw Invalid(listName + " rule must be require or exclude");
        }

        string attribute = ReadOptionalString(rule, "attribute") ?? throw Invalid(listName + " rule needs an attribute");
        CatalogArgumentMatch? argument = rule.TryGetValue("argument", out object? value) && value is not null
            ? ReadArgument(value as Dictionary<string, object?> ?? throw Invalid("argument must be an object"))
            : null;
        return new CatalogAttributeRule(kind, attribute, argument);
    }

    private static CatalogArgumentMatch ReadArgument(Dictionary<string, object?> argument)
    {
        string? name = ReadOptionalString(argument, "name");
        int? position = ReadPosition(argument);
        string? equalsValue = ReadOptionalString(argument, "equals");
        string? containsValue = ReadOptionalString(argument, "contains");

        if ((name is null) == (position is null))
        {
            throw Invalid("argument needs exactly one of name and position");
        }

        if ((equalsValue is null) == (containsValue is null))
        {
            throw Invalid("argument needs exactly one of equals and contains");
        }

        return new CatalogArgumentMatch(name, position, equalsValue, containsValue);
    }

    private static int? ReadPosition(Dictionary<string, object?> argument)
    {
        if (!argument.TryGetValue("position", out object? value) || value is null)
        {
            return null;
        }

        if (value is MiniJsonNumber number && int.TryParse(number.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int position))
        {
            return position;
        }

        throw Invalid("argument position must be a non-negative integer");
    }
}
