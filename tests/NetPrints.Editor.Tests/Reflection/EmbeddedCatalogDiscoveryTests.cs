using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Reflection;
using NetPrints.Testing;
using NetPrints.Translator;
using Project = NetPrints.Core.Project;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>AN-T11: the catalog a referenced library embeds reaches the editor's reflection host, in place of the live members of that library.</summary>
public sealed class EmbeddedCatalogDiscoveryTests : IDisposable
{
    private static readonly TypeSpecifier Greeter = new("AnnotatedFixture.Greeter");
    private static readonly TypeSpecifier Unlisted = new("AnnotatedFixture.Unlisted");

    private readonly string scratch = Directory.CreateTempSubdirectory("np-embedded-").FullName;

    public void Dispose() => Directory.Delete(scratch, recursive: true);

    [Fact(Timeout = 120000)]
    public async Task ThePackagedCatalogOffersTheAnnotatedNodesAndNotTheOtherPublicMembers()
    {
        var host = NewHost(out _);

        await host.ReloadAsync(ProjectReferencing(FixtureExtensions.AnnotatedLibraryAssembly()), TestContext.Current.CancellationToken);

        Assert.Contains(host.NonStaticTypes, t => t == Greeter);
        Assert.DoesNotContain(host.NonStaticTypes, t => t == Unlisted);

        // Type-less queries enumerate the offered nodes; a query that names a type still resolves it in the live compilation.
        var offered = host.Provider.GetMethods(new ReflectionProviderMethodQuery()).Where(m => m.DeclaringType.Name.StartsWith("AnnotatedFixture.", StringComparison.Ordinal)).Select(m => m.Name).ToList();
        Assert.Contains("Greet", offered);
        Assert.Contains("Count", offered);
        Assert.DoesNotContain("Undeclared", offered);
        Assert.DoesNotContain("Value", offered);
    }

    [Fact(Timeout = 120000)]
    public async Task TheSameCatalogFromTwoReferencesIsLoadedOnceAndTheSecondIsReportedAsNpc103()
    {
        string copy = Path.Combine(scratch, "Copy.dll");
        File.Copy(FixtureExtensions.AnnotatedLibraryAssembly(), copy);
        var host = NewHost(out CollectingLogger logger);

        await host.ReloadAsync(ProjectReferencing(FixtureExtensions.AnnotatedLibraryAssembly(), copy), TestContext.Current.CancellationToken);

        Assert.Single(host.Provider.GetMethods(new ReflectionProviderMethodQuery()), m => m.DeclaringType == Greeter && m.Name == "Greet");
        Assert.Single(logger.Messages, m => m.Contains("NPC103", StringComparison.Ordinal));
    }

    [Fact(Timeout = 120000)]
    public async Task ACatalogWithANewerSchemaIsSkippedAndTheOtherReferencesStillContribute()
    {
        string newer = EmitAssemblyEmbedding("newer", 99, """{"schemaVersion":99}""");
        var host = NewHost(out CollectingLogger logger);

        await host.ReloadAsync(ProjectReferencing(newer, FixtureExtensions.AnnotatedLibraryAssembly()), TestContext.Current.CancellationToken);

        Assert.Contains(host.NonStaticTypes, t => t == Greeter);
        Assert.Single(logger.Messages, m => m.Contains("NPC101", StringComparison.Ordinal) && m.Contains("newer.dll", StringComparison.Ordinal));
    }

    [Fact(Timeout = 120000)]
    public async Task AnExtensionCatalogWinsOverAnEmbeddedCatalogOfTheSameId() // E-R10, NPC103
    {
        var manifest = new ExtensionManifest("editor.test", "Editor test", "1.0.0", string.Empty, "1.0", []);
        await using var extensions = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new SameIdCatalogExtension())]), NullLoggerFactory.Instance);
        var logger = new CollectingLogger();
        var host = new ReflectionHost(new InlineDispatcher(), extensions, logger);

        await host.ReloadAsync(ProjectReferencing(FixtureExtensions.AnnotatedLibraryAssembly()), TestContext.Current.CancellationToken);

        // The embedded catalog lost, so its assembly is not covered and its other public types come from the live compilation.
        Assert.Contains(host.NonStaticTypes, t => t.Name == "Acme.Widget");
        Assert.Contains(host.NonStaticTypes, t => t == Unlisted);
        Assert.Single(logger.Messages, m => m.Contains("NPC103", StringComparison.Ordinal));
    }

    [Fact(Timeout = 120000)]
    public async Task AReferenceThatDoesNotExistIsSkippedWithoutAWarning() // E-R9
    {
        var host = NewHost(out CollectingLogger logger);

        await host.ReloadAsync(ProjectReferencing(Path.Combine(scratch, "NotBuiltYet.dll"), FixtureExtensions.AnnotatedLibraryAssembly()), TestContext.Current.CancellationToken);

        Assert.Contains(host.NonStaticTypes, t => t == Greeter);
        Assert.DoesNotContain(logger.Messages, m => m.Contains("NotBuiltYet.dll", StringComparison.Ordinal));
    }

    [Fact]
    public void AnUnreadableReferenceIsSkippedWith1014AndTheOthersStillContribute() // E-R9
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string locked = Path.Combine(scratch, "Locked.dll");
        File.Copy(FixtureExtensions.AnnotatedLibraryAssembly(), locked);
        File.SetUnixFileMode(locked, UnixFileMode.None);
        var host = NewHost(out CollectingLogger logger);

        try
        {
            var catalogs = host.LoadEmbeddedCatalogs([new ResolvedAssembly(locked, null), new ResolvedAssembly(FixtureExtensions.AnnotatedLibraryAssembly(), null)], TestContext.Current.CancellationToken);

            Assert.Single(catalogs);
        }
        finally
        {
            File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Assert.Single(logger.Messages, m => m.Contains("Locked.dll", StringComparison.Ordinal));
    }

    private static ReflectionHost NewHost(out CollectingLogger logger)
    {
        logger = new CollectingLogger();
        return new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), logger);
    }

    private static Project ProjectReferencing(params string[] assemblies)
    {
        ProjectSnapshot runtime = TestSnapshots.WithRuntimeAssemblies("P", "N");
        return Project.FromSnapshot(runtime with
        {
            References = [.. runtime.References, .. assemblies.Select(path => new ResolvedAssembly(path, null))],
        });
    }

    private string EmitAssemblyEmbedding(string name, int schemaVersion, string json)
    {
        string source = $$"""
            [assembly: NetPrints.Annotations.NetPrintsEmbeddedCatalog("{{name}}", {{schemaVersion}}, "{{json.Replace("\"", "\\\"", StringComparison.Ordinal)}}")]
            namespace NetPrints.Annotations
            {
                [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
                internal sealed class NetPrintsEmbeddedCatalogAttribute : System.Attribute
                {
                    public NetPrintsEmbeddedCatalogAttribute(string id, int schemaVersion, string json) { }
                }
            }
            """;
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            TestSnapshots.RuntimeAssemblyPaths().Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        string path = Path.Combine(scratch, name + ".dll");
        var result = compilation.Emit(path, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics));
        return path;
    }

    private sealed class SameIdCatalogExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddTypeCatalog(new InMemoryTypeCatalog(
            new CatalogInfo("catalogannotatedlib", "1.0.0", []),
            [new TypeSpecifier("Acme.Widget")], [], [], [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(), new Dictionary<MethodSpecifier, string>()));
    }

    private sealed class CollectingLogger : ILogger<ReflectionHost>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }
}
