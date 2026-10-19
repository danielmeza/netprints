using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// Loads extensions the way the host does and drives the pipeline they take part in: translation and the document format
/// (contracts/extensions.md §4).
/// </summary>
public sealed class ExtensionHarness : IAsyncDisposable
{
    private const string DocumentName = "harness.netpc.json";
    private const string InProcessIdPrefix = "harness.inprocess.";

    private readonly JsonDocumentFormat format;
    private readonly DocumentMapper mapper;

    private readonly EditorContributionRecorder recorder;

    private ExtensionHarness(ExtensionRegistry registry, EditorContributionRecorder recorder, HarnessLog logs)
    {
        Registry = registry;
        this.recorder = recorder;
        Log = logs;
        format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry.NodeConverters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
        mapper = new DocumentMapper(registry.NodeConverters, NullLogger<DocumentMapper>.Instance);
    }

    /// <summary>The registry the loader built: the built-in extension, the folders' extensions and the in-process ones.</summary>
    public ExtensionRegistry Registry { get; }

    /// <summary>Gets the editor contributions the loaded extensions made, in load order.</summary>
    public IReadOnlyList<RecordedContribution> EditorContributions => recorder.Items;

    /// <summary>Gets the warnings and errors the host logged for this harness; it stays readable after the harness is disposed.</summary>
    public HarnessLog Log { get; }

    /// <summary>Loads the built-in extension, every extension folder in <paramref name="folders"/> and every in-process extension.</summary>
    /// <param name="folders">Extension folders, each holding a <c>netprints-extension.json</c>, in discovery order.</param>
    /// <param name="inProcess">Extensions created in the test process; each gets the id <c>harness.inprocess.&lt;index&gt;</c>.</param>
    /// <param name="cancellationToken">Token to cancel the load.</param>
    /// <returns>The harness; dispose it to dispose the registry and what the extensions contributed.</returns>
    public static Task<ExtensionHarness> CreateAsync(IReadOnlyList<string> folders, IReadOnlyList<INetPrintsExtension> inProcess, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inProcess);
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        var entries = new List<(ExtensionManifest, INetPrintsExtension)>();
        for (int index = 0; index < inProcess.Count; index++)
        {
            string id = InProcessIdPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            entries.Add((new ExtensionManifest(id, id, "1.0.0", id + ".dll", api, []), inProcess[index]));
        }

        return CreateWithManifestsAsync(folders, entries, cancellationToken);
    }

    /// <summary>Loads the built-in extension, every extension folder in <paramref name="folders"/> and every in-process extension under its own manifest.</summary>
    /// <param name="folders">Extension folders, each holding a <c>netprints-extension.json</c>, in discovery order.</param>
    /// <param name="inProcess">Extensions created in the test process, each with the manifest it is loaded under; the manifest id is the prefix of the kinds it contributes.</param>
    /// <param name="cancellationToken">Token to cancel the load.</param>
    /// <returns>The harness; dispose it to dispose the registry and what the extensions contributed.</returns>
    public static async Task<ExtensionHarness> CreateWithManifestsAsync(
        IReadOnlyList<string> folders, IReadOnlyList<(ExtensionManifest Manifest, INetPrintsExtension Extension)> inProcess, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(inProcess);
        var entries = new List<(ExtensionManifest, INetPrintsExtension)> { BuiltInExtension.InProcessEntry };
        entries.AddRange(inProcess.Select(entry => (entry.Manifest, entry.Extension)));

        var recorder = new EditorContributionRecorder();
        var logs = new HarnessLog();
        using ILoggerFactory loggerFactory = logs.CreateFactory();
        var options = new ExtensionLoaderOptions([], [.. folders], entries) { EditorContributions = manifest => recorder.For(manifest.Id) };
        ExtensionRegistry registry = await Task.Run(() => new ExtensionLoader(options, loggerFactory).Load(cancellationToken), cancellationToken).ConfigureAwait(false);
        return new ExtensionHarness(registry, recorder, logs);
    }

    /// <summary>Builds a public class with a static <c>Main</c> that runs one node of each kind in <paramref name="kinds"/>, in order.</summary>
    /// <param name="ns">The namespace of the class.</param>
    /// <param name="name">The class name.</param>
    /// <param name="kinds">Node kind ids; each must be loaded and offer a node-search entry.</param>
    /// <returns>The class.</returns>
    /// <exception cref="InvalidOperationException">A kind is not loaded or offers no node-search entry.</exception>
    public ClassGraph BuildClass(string ns, string name, params string[] kinds)
    {
        ArgumentException.ThrowIfNullOrEmpty(ns);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(kinds);
        Project project = NewProject(ns);
        var cls = new ClassGraph { Name = name, Namespace = ns, Visibility = MemberVisibility.Public, Project = project };
        project.Classes.Add(cls);
        var method = new MethodGraph("Main") { Class = cls, Visibility = MemberVisibility.Public, Modifiers = MethodModifiers.Static };
        cls.Methods.Add(method);

        NodeOutputExecPin previous = ((MethodEntryNode)method.EntryNode).ExecPin;
        foreach (string kind in kinds)
        {
            NodeKindDescriptor descriptor = Registry.NodeKinds.FirstOrDefault(k => k.Kind == kind)
                ?? throw new InvalidOperationException($"Node kind '{kind}' is not loaded.");
            NodeSuggestion suggestion = descriptor.Suggestions.FirstOrDefault()
                ?? throw new InvalidOperationException($"Node kind '{kind}' offers no node-search entry.");
            Node node = suggestion.Create(method);
            if (node.InputExecPins.Count > 0 && node.OutputExecPins.Count > 0)
            {
                GraphUtil.ConnectExecPins(previous, node.InputExecPins[0]);
                previous = node.OutputExecPins[0];
            }
        }

        GraphUtil.ConnectExecPins(previous, ((ReturnNode)method.MainReturnNode).ExecPin);
        return cls;
    }

    /// <summary>Translates <paramref name="graph"/> with the loaded extensions' translators and emitters.</summary>
    /// <param name="graph">The class to translate.</param>
    /// <returns>The C# text of the class.</returns>
    public string Translate(ClassGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new ClassTranslator(Registry.Translation).TranslateClass(graph);
    }

    /// <summary>Writes <paramref name="graph"/> as a class document with the loaded extensions' node converters.</summary>
    /// <param name="graph">The class to write.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The bytes the canonical writer produced.</returns>
    public async Task<byte[]> WriteAsync(ClassGraph graph, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        await using var output = new MemoryStream();
        await format.WriteClassAsync(mapper.ToDocument(graph), output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
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
            read = await format.ReadClassAsync(input, id, cancellationToken).ConfigureAwait(false);
        }

        ClassGraph graph = mapper.FromDocument(read, NewProject("Harness"), new List<DocumentIssue>(), id);
        return await WriteAsync(graph, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => Registry.DisposeAsync();

    private static Project NewProject(string name) => Project.FromSnapshot(new ProjectSnapshot(
        Path.Combine(Path.GetTempPath(), name + ".csproj"), name, name, name, BinaryType.SharedLibrary, "net10.0", DefaultProjectProfile.ProfileId,
        ReferencesNetPrintsSdk: true, GraphFiles: [], ExtensionFolders: [], References: [], DeclaredReferences: [], OtherSources: [],
        CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []));
}
