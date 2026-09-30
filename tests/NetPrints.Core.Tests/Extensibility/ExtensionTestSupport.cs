using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>Records every log entry, for asserting on event ids.</summary>
public sealed class CollectingLoggerFactory : ILoggerFactory
{
    public sealed record Entry(LogLevel Level, EventId EventId, string Message);

    public List<Entry> Entries { get; } = [];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(Entries);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(List<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (entries)
            {
                entries.Add(new Entry(logLevel, eventId, formatter(state, exception)));
            }
        }
    }
}

/// <summary>An in-memory extension whose <c>Register</c> is a delegate.</summary>
public sealed class DelegateExtension(Action<IExtensionBuilder> register) : INetPrintsExtension
{
    public void Register(IExtensionBuilder builder) => register(builder);
}

/// <summary>A custom node with one exec in and out, for extension kinds.</summary>
public sealed class PingNode : Node
{
    public PingNode(NodeGraph graph)
        : base(graph)
    {
        AddInputExecPin("Exec");
        AddOutputExecPin("Then");
    }
}

public sealed record PingNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);

public sealed class PingConverter(string kind) : INodeDocumentConverter
{
    public string Kind => kind;

    public Type NodeType => typeof(PingNode);

    public Type DocumentType => typeof(PingNodeDocument);

    public NodeDocument ToDocument(Node node, NodeMappingContext context) => new PingNodeDocument(node.Id, null, null);

    public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new PingNode(graph);
}

/// <summary>A second custom node type, for tests that need two kinds.</summary>
public sealed class PongNode(NodeGraph graph) : Node(graph);

public sealed record PongNodeDocument(string Id, string? Name, IReadOnlyList<PinStateDocument>? Pins) : NodeDocument(Id, Name, Pins);

public sealed class PongConverter(string kind) : INodeDocumentConverter
{
    public string Kind => kind;

    public Type NodeType => typeof(PongNode);

    public Type DocumentType => typeof(PongNodeDocument);

    public NodeDocument ToDocument(Node node, NodeMappingContext context) => new PongNodeDocument(node.Id, null, null);

    public Node CreateNode(NodeDocument document, NodeGraph graph, NodeMappingContext context) => new PongNode(graph);
}

public sealed class PingTranslator : INodeTranslator
{
    public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex) =>
        context.AppendLine("System.Console.WriteLine(\"ping\");");
}

public sealed class NamedClassEmitter(string id) : IClassEmitter
{
    public string Id => id;

    public void EmitClass(ClassEmitContext context)
    {
    }
}

public sealed class DisposableMemberEmitter : IMemberEmitter, IDisposable
{
    public string Id => "disposable";

    public bool Disposed { get; private set; }

    public void EmitMember(MemberEmitContext context)
    {
    }

    public void Dispose() => Disposed = true;
}

/// <summary>Implements both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>, recording which
/// one was actually called (R1-10: the async one must win).</summary>
public sealed class DualDisposableMemberEmitter : IMemberEmitter, IDisposable, IAsyncDisposable
{
    public string Id => "dual-disposable";

    public bool SyncDisposed { get; private set; }

    public bool AsyncDisposed { get; private set; }

    public void EmitMember(MemberEmitContext context)
    {
    }

    public void Dispose() => SyncDisposed = true;

    public ValueTask DisposeAsync()
    {
        AsyncDisposed = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class StubProfile(string id) : IProjectProfile
{
    public string Id => id;

    public string DisplayName => id;

    public string DefaultTargetFramework => "net10.0";

    public string ProjectTemplate => "<Project />";

    public IReadOnlyList<TypeSpecifier> BaseTypes => [TypeSpecifier.FromType<object>()];

    public IReadOnlyList<ClassTemplate> ClassTemplates => [];

    public string? CatalogProfileId => null;
}

public sealed class SingleKindLibrary(string id, params NodeKindDescriptor[] kinds) : INodeLibrary
{
    public string Id => id;

    public IReadOnlyList<NodeKindDescriptor> NodeKinds => kinds;
}

/// <summary>Builders for manifests, temp folders and in-process options.</summary>
public static class ExtensionTestSupport
{
    public static ExtensionManifest Manifest(string id, string api = "1.0", params string[] dependsOn) =>
        new(id, id, "1.0.0", id + ".dll", api, dependsOn);

    public static NodeKindDescriptor PingKind(string kind, GraphKinds allowedIn = GraphKinds.Method) =>
        new(kind, typeof(PingNode), new PingConverter(kind), new PingTranslator(), allowedIn, []);

    public static NodeKindDescriptor PongKind(string kind, GraphKinds allowedIn = GraphKinds.Method) =>
        new(kind, typeof(PongNode), new PongConverter(kind), new PingTranslator(), allowedIn, []);

    public static (ExtensionManifest, INetPrintsExtension) InProcess(string id, Action<IExtensionBuilder> register, string api = "1.0", params string[] dependsOn) =>
        (Manifest(id, api, dependsOn), new DelegateExtension(register));

    public static ExtensionLoaderOptions Options(
        IEnumerable<(ExtensionManifest, INetPrintsExtension)>? inProcess = null,
        IEnumerable<string>? folders = null,
        IEnumerable<string>? searchDirectories = null) =>
        new([.. searchDirectories ?? []], [.. folders ?? []], [.. inProcess ?? []]);

    public static ExtensionRegistry Load(ExtensionLoaderOptions options, CollectingLoggerFactory? logs = null) =>
        new ExtensionLoader(options, logs ?? new CollectingLoggerFactory()).Load(TestContext.Current.CancellationToken);

    public static string NewTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "netprints-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string WriteManifest(string folder, string json)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, ExtensionManifest.FileName);
        File.WriteAllText(path, json);
        return path;
    }

    public static string ManifestJson(string id, string assembly = "missing.dll", string api = "1.0", params string[] dependsOn) =>
        $$"""{ "id": "{{id}}", "name": "{{id}}", "version": "1.0.0", "assembly": "{{assembly}}", "netprintsApi": "{{api}}", "dependsOn": [{{string.Join(", ", dependsOn.Select(d => $"\"{d}\""))}}] }""";

    public static ExtensionLoadResult.Failed SingleFailure(ExtensionRegistry registry, string id) =>
        Assert.Single(registry.Results.OfType<ExtensionLoadResult.Failed>(), r => r.Id == id);

    private static readonly string[] OptedInExperimentalIds = [ExperimentalApiIds.HostChannel, ExperimentalApiIds.Settings, ExperimentalApiIds.Emitters];

    /// <summary>Compiles <paramref name="source"/> into <c>folder/name.dll</c> against the host's assemblies.</summary>
    public static string Compile(string folder, string name, string source)
    {
        Directory.CreateDirectory(folder);
        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        var references = trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
                .WithSpecificDiagnosticOptions(OptedInExperimentalIds.ToDictionary(id => id, _ => ReportDiagnostic.Suppress)));
        string path = Path.Combine(folder, name + ".dll");
        var emitted = compilation.Emit(path);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return path;
    }
}
