using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Translator;

namespace NetPrints.Testing.Extensions;

/// <summary>
/// Loads extensions the way the host does and drives the pipeline they take part in: translation, the document format and
/// the generator (contracts/extensions.md §4). Internal test support; not packaged.
/// </summary>
public sealed class ExtensionHarness : IAsyncDisposable
{
    private const string DocumentName = "harness.netpc.json";
    private const string GeneratedSuffix = ".g.cs";

    private readonly JsonDocumentFormat format;
    private readonly DocumentMapper mapper;

    private ExtensionHarness(ExtensionRegistry registry)
    {
        Registry = registry;
        format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry.NodeConverters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
        mapper = new DocumentMapper(registry.NodeConverters, NullLogger<DocumentMapper>.Instance);
    }

    /// <summary>The registry the loader built: the built-in extension, the folders' extensions and the in-process ones.</summary>
    public ExtensionRegistry Registry { get; }

    /// <summary>Loads the built-in extension, every extension folder in <paramref name="folders"/> and every in-process extension.</summary>
    /// <param name="folders">Extension folders, each holding a <c>netprints-extension.json</c>, in discovery order.</param>
    /// <param name="inProcess">Extensions created in the test process; each gets the id <c>harness.inprocess.&lt;index&gt;</c>.</param>
    /// <param name="cancellationToken">Token to cancel the load.</param>
    /// <returns>The harness; dispose it to dispose the registry and what the extensions contributed.</returns>
    public static async Task<ExtensionHarness> CreateAsync(IReadOnlyList<string> folders, IReadOnlyList<INetPrintsExtension> inProcess, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(inProcess);
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        var entries = new List<(ExtensionManifest, INetPrintsExtension)> { BuiltInExtension.InProcessEntry };
        for (int index = 0; index < inProcess.Count; index++)
        {
            string id = $"harness.inprocess.{index}";
            entries.Add((new ExtensionManifest(id, id, "1.0.0", id + ".dll", api, []), inProcess[index]));
        }

        var options = new ExtensionLoaderOptions([], [.. folders], entries);
        ExtensionRegistry registry = await Task.Run(() => new ExtensionLoader(options, NullLoggerFactory.Instance).Load(cancellationToken), cancellationToken);
        return new ExtensionHarness(registry);
    }

    /// <summary>Translates <paramref name="graph"/> with the loaded extensions' translators and emitters.</summary>
    /// <param name="graph">The class to translate.</param>
    /// <returns>The C# text of the class.</returns>
    public string Translate(ClassGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new ClassTranslator(Registry.Translation).TranslateClass(graph);
    }

    /// <summary>Reads <paramref name="document"/> with the loaded extensions' node kinds and writes it back; nodes of an extension that is not loaded are preserved.</summary>
    /// <param name="document">The bytes of a <c>.netpc.json</c> class document.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bytes the canonical writer produced.</returns>
    public async Task<byte[]> RoundTripAsync(byte[] document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var id = new DocumentId(DocumentName);
        ClassDocument read;
        using (var input = new MemoryStream(document))
        {
            read = await format.ReadClassAsync(input, id, cancellationToken);
        }

        ClassGraph graph = mapper.FromDocument(read, NewProject("Harness"), new List<DocumentIssue>(), id);
        await using var output = new MemoryStream();
        await format.WriteClassAsync(mapper.ToDocument(graph), output, cancellationToken);
        return output.ToArray();
    }

    /// <summary>Generates the <c>.netpc.g.cs</c> of every <c>.netpc.json</c> under <paramref name="projectDirectory"/>.</summary>
    /// <param name="projectDirectory">The directory to scan recursively; it names the project.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One result per graph, in ordinal path order.</returns>
    public async Task<IReadOnlyList<GeneratedFileResult>> GenerateAsync(string projectDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        string name = new DirectoryInfo(projectDirectory).Name;
        GraphJob[] jobs =
        [
            .. Directory.EnumerateFiles(projectDirectory, "*" + DocumentNameSuffix, SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => new GraphJob(path, Path.ChangeExtension(path, null) + GeneratedSuffix)),
        ];
        var request = new GenerateRequest(Path.Combine(projectDirectory, name + ".csproj"), name, DefaultProjectProfile.ProfileId, jobs, []);
        return await GraphCodeGenerator.Create(Registry).GenerateAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Registry.DisposeAsync();

    private static string DocumentNameSuffix => ".netpc.json";

    private static Project NewProject(string name) => Project.FromSnapshot(new ProjectSnapshot(
        Path.Combine(Path.GetTempPath(), name + ".csproj"), name, name, name, BinaryType.SharedLibrary, "net10.0", DefaultProjectProfile.ProfileId,
        ReferencesNetPrintsSdk: true, GraphFiles: [], ExtensionFolders: [], References: [], DeclaredReferences: [], OtherSources: [],
        CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []));
}
