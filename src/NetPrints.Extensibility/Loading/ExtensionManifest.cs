using System.Text.Json;
using System.Text.RegularExpressions;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// The content of <c>netprints-extension.json</c> (extension-points.md §8).
/// </summary>
/// <param name="Id">Extension id, unique in a registry; no <c>/</c>.</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Extension version text.</param>
/// <param name="Assembly">File name of the extension assembly, relative to the manifest.</param>
/// <param name="NetprintsApi">The <c>major.minor</c> API version the extension was built for.</param>
/// <param name="DependsOn">Ids of the extensions that must load first.</param>
public sealed partial record ExtensionManifest(
    string Id,
    string Name,
    string Version,
    string Assembly,
    string NetprintsApi,
    IReadOnlyList<string> DependsOn)
{
    /// <summary>
    /// The file name of a manifest.
    /// </summary>
    public const string FileName = "netprints-extension.json";

    /// <summary>
    /// The parsed <see cref="NetprintsApi"/> as a version, or <see langword="null"/> when it is not <c>major.minor</c>.
    /// </summary>
    public System.Version? ApiVersion => ApiPattern().IsMatch(NetprintsApi) ? System.Version.Parse(NetprintsApi) : null;

    /// <summary>
    /// Parses a manifest. Unknown properties are ignored; <c>dependsOn</c> may be omitted.
    /// </summary>
    /// <param name="json">The manifest's UTF-8 JSON.</param>
    /// <param name="manifestPath">Path of the manifest, for messages.</param>
    /// <returns>The manifest.</returns>
    /// <exception cref="ExtensionManifestException">The JSON is malformed, a required string is missing or empty, the
    /// id contains a character other than letters, digits, <c>.</c>, <c>-</c> or <c>_</c>, or <c>netprintsApi</c> is
    /// not <c>major.minor</c> (<c>NPX001</c>).</exception>
    public static ExtensionManifest Parse(Stream json, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(manifestPath);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new ExtensionManifestException(manifestPath, $"not valid JSON: {ex.Message}", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ExtensionManifestException(manifestPath, "the manifest must be a JSON object.");
            }

            string id = RequiredString(root, "id", manifestPath);
            string name = RequiredString(root, "name", manifestPath);
            string version = RequiredString(root, "version", manifestPath);
            string assembly = RequiredString(root, "assembly", manifestPath);
            string api = RequiredString(root, "netprintsApi", manifestPath);

            if (!IdPattern().IsMatch(id))
            {
                throw new ExtensionManifestException(manifestPath, $"'id' must contain only letters, digits, '.', '-' and '_': '{id}'.");
            }

            if (!ApiPattern().IsMatch(api))
            {
                throw new ExtensionManifestException(manifestPath, $"'netprintsApi' must be 'major.minor': '{api}'.");
            }

            var dependsOn = new List<string>();
            if (root.TryGetProperty("dependsOn", out JsonElement dependencies) && dependencies.ValueKind != JsonValueKind.Null)
            {
                if (dependencies.ValueKind != JsonValueKind.Array)
                {
                    throw new ExtensionManifestException(manifestPath, "'dependsOn' must be an array of extension ids.");
                }

                foreach (JsonElement item in dependencies.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        throw new ExtensionManifestException(manifestPath, "'dependsOn' must contain only non-empty strings.");
                    }

                    dependsOn.Add(item.GetString() ?? string.Empty);
                }
            }

            return new ExtensionManifest(id, name, version, assembly, api, dependsOn);
        }
    }

    private static string RequiredString(JsonElement root, string property, string manifestPath)
    {
        if (!root.TryGetProperty(property, out JsonElement value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ExtensionManifestException(manifestPath, $"'{property}' is required and must be a non-empty string.");
        }

        return value.GetString() ?? string.Empty;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"^\d+\.\d+$")]
    private static partial Regex ApiPattern();
}
