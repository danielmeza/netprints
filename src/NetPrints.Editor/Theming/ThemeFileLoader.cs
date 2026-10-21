using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NetPrints.Editor.Theming;

/// <summary>Reads a <c>*.nptheme.json</c> file (ADR-0035 decision 8): the embedded presets and, later, the user and extension themes share this loader.</summary>
public static partial class ThemeFileLoader
{
    /// <summary>The schema version this loader reads; a newer file is skipped.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The longest file, in characters, the loader reads.</summary>
    public const int MaxLength = 262144;

    private const string SchemaVersionKey = "schemaVersion";
    private const string IdKey = "id";
    private const string LabelKey = "label";
    private const string BaseKey = "base";
    private const string SemiVariantKey = "semiVariant";
    private const string TokensKey = "tokens";
    private const string DarkBase = "dark";
    private const string LightBase = "light";

    private static readonly string[] SemiVariants = ["Aquatic", "Dusk", "Desert", "NightSky"];

    /// <summary>Gets the names of Semi's high-contrast variants a theme may run on.</summary>
    public static IReadOnlyList<string> KnownSemiVariants => SemiVariants;

    /// <summary>Loads one theme file.</summary>
    /// <param name="json">The text of the file.</param>
    /// <returns>
    /// The theme and its warnings. An unknown token is warned about and ignored, a bad value is warned about and keeps the base value,
    /// and a file that cannot be used at all (too large, not JSON, a newer schema, a bad id, label or base) gives no theme and one warning.
    /// </returns>
    public static ThemeLoadResult Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > MaxLength)
        {
            return Skipped("the file is larger than " + MaxLength + " characters");
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException e)
        {
            return Skipped("the file is not valid JSON: " + e.Message);
        }

        if (root is null)
        {
            return Skipped("the file is not a JSON object");
        }

        if (ReadInt(root, SchemaVersionKey) is not { } version || version != SchemaVersion)
        {
            return Skipped("schemaVersion must be " + SchemaVersion);
        }

        string? id = ReadString(root, IdKey);
        string? label = ReadString(root, LabelKey);
        if (id is null || !IdPattern().IsMatch(id))
        {
            return Skipped("id must be lower case words joined by '.', '-' or '_'");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return Skipped("label must be a non-empty string");
        }

        ThemeBase? themeBase = ReadString(root, BaseKey) switch
        {
            DarkBase => ThemeBase.Dark,
            LightBase => ThemeBase.Light,
            _ => null,
        };
        if (themeBase is not { } variant)
        {
            return Skipped("base must be \"dark\" or \"light\"");
        }

        List<string> warnings = [];
        string? semi = ReadString(root, SemiVariantKey);
        if (semi is not null && !SemiVariants.Contains(semi, StringComparer.Ordinal))
        {
            warnings.Add($"semiVariant \"{semi}\" is not one of {string.Join(", ", SemiVariants)}; the plain variant is used");
            semi = null;
        }

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root[TokensKey] is JsonObject entries)
        {
            foreach ((string name, JsonNode? node) in entries)
            {
                ReadToken(name, node, tokens, warnings);
            }
        }

        return new ThemeLoadResult(new ThemePreset(id, label, variant, semi, tokens), warnings);
    }

    private static void ReadToken(string name, JsonNode? node, Dictionary<string, string> tokens, List<string> warnings)
    {
        if (ThemeTokenCatalogue.Find(name) is not { } token)
        {
            warnings.Add($"{name}: no such token; it is ignored");
        }
        else if (node is JsonValue value && value.TryGetValue(out string? text) && ThemeValueParser.TryParse(token, text, out _))
        {
            tokens[name] = text;
        }
        else
        {
            warnings.Add($"{name}: the value is not a valid {token.Kind}; the base value is kept");
        }
    }

    private static ThemeLoadResult Skipped(string reason) => new(null, [reason]);

    private static string? ReadString(JsonObject root, string key) =>
        root[key] is JsonValue value && value.TryGetValue(out string? text) ? text : null;

    private static int? ReadInt(JsonObject root, string key) =>
        root[key] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    [GeneratedRegex("^[a-z0-9]+([._-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
