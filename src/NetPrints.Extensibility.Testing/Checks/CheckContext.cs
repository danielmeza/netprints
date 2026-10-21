using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>What a check works with: the harness, the options and the extension under test.</summary>
internal sealed class CheckContext
{
    private const string ManifestFileName = ExtensionManifest.FileName;

    public CheckContext(ExtensionHarness harness, ConformanceOptions options)
    {
        Harness = harness;
        Options = options;
        SubjectId = options.ExtensionId
            ?? harness.Registry.Results.Select(result => result.Id).FirstOrDefault(id => !string.Equals(id, BuiltInExtension.Manifest.Id, StringComparison.Ordinal));
    }

    public ExtensionHarness Harness { get; }

    public ConformanceOptions Options { get; }

    /// <summary>Gets the id of the extension under test, or <see langword="null"/> when nothing but the built-in extension was loaded.</summary>
    public string? SubjectId { get; }

    /// <summary>Gets the node kinds of the extension under test that offer a node-search entry, so a graph can be built with them.</summary>
    public IReadOnlyList<NodeKindDescriptor> SubjectKinds =>
        [.. Harness.Registry.NodeKinds.Where(kind => SubjectId is not null
            && kind.Kind.StartsWith(SubjectId + "/", StringComparison.Ordinal)
            && kind.Suggestions.Count > 0)];

    /// <summary>Gets the node kinds of the extension under test that offer no node-search entry, so no graph can be built with them.</summary>
    public IReadOnlyList<NodeKindDescriptor> UnexercisedKinds =>
        [.. Harness.Registry.NodeKinds.Where(kind => SubjectId is not null
            && kind.Kind.StartsWith(SubjectId + "/", StringComparison.Ordinal)
            && kind.Suggestions.Count == 0)];

    /// <summary>Gets the reason a node check gives for kinds it could not exercise.</summary>
    /// <param name="kinds">The kinds.</param>
    /// <returns>The text naming them.</returns>
    public static string NotExercised(IReadOnlyList<NodeKindDescriptor> kinds) =>
        "not exercised (no node-search entry): " + string.Join(", ", kinds.Select(kind => kind.Kind));

    /// <summary>Reads the manifest in <see cref="ConformanceOptions.ExtensionFolder"/>.</summary>
    /// <param name="manifest">The manifest, or <see langword="null"/> when it could not be read.</param>
    /// <param name="problem">Why it could not be read, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the manifest was read.</returns>
    public bool TryReadManifest(out ExtensionManifest? manifest, out string? problem)
    {
        manifest = null;
        string folder = Options.ExtensionFolder ?? string.Empty;
        string path = Path.Combine(folder, ManifestFileName);
        if (!File.Exists(path))
        {
            problem = $"{ManifestFileName} is not in {Path.GetFileName(folder)}";
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            manifest = ExtensionManifest.Parse(stream, path);
            problem = null;
            return true;
        }
        catch (ExtensionManifestException ex)
        {
            problem = ex.Message;
            return false;
        }
    }
}
