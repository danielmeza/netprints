using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Generator;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>What the real test extension contributes, end to end: EX-T02, EX-T03, EX-T07 and DF-T17.</summary>
public sealed class ContributionTests : IAsyncLifetime
{
    private static readonly ExtensionLoaderOptions WithTestExtension =
        new([], [TestExtensionLocation.Folder], [BuiltInExtension.InProcessEntry]);

    private readonly string directory = Directory.CreateTempSubdirectory("netprints-contribution-").FullName;
    private readonly ExtensionRegistry registry = ExtensionTestSupport.Load(WithTestExtension);
    private readonly ExtensionRegistry builtIn = ExtensionTestSupport.Load(ExtensionLoaderOptions.BuiltInOnly);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await registry.DisposeAsync();
        await builtIn.DisposeAsync();
        Directory.Delete(directory, recursive: true);
    }

    private async Task<string> WriteJsonAsync(ClassGraph cls)
    {
        string path = Path.Combine(directory, cls.Name + ".netpc.json");
        await ExtensionGraphs.WriteAsync(registry, cls, path);
        return path;
    }

    private async Task<(ClassGraph Class, List<DocumentIssue> Issues)> ReadAsync(ExtensionRegistry from, string path)
    {
        var id = new DocumentId(Path.GetFileName(path));
        ClassDocument document;
        await using (FileStream input = File.OpenRead(path))
        {
            document = await ExtensionGraphs.Format(from).ReadClassAsync(input, id, TestContext.Current.CancellationToken);
        }

        var issues = new List<DocumentIssue>();
        ClassGraph cls = new DocumentMapper(from.NodeConverters, NullLogger<DocumentMapper>.Instance).FromDocument(document, TestProjects.Create("P", "P"), issues, id);
        return (cls, issues);
    }

    [Fact]
    public void AnExtensionKindIsRegisteredAndOfferedOnlyInTheGraphKindsItIsAllowedIn()
    {
        NodeKindDescriptor kind = Assert.Single(registry.NodeKinds, k => k.Kind == "netprints.test/Log");
        Assert.DoesNotContain(builtIn.NodeKinds, k => k.Kind == kind.Kind);

        (ClassGraph cls, _) = ExtensionGraphs.BuildLogClass(registry, "P", "C");
        MethodGraph method = cls.Methods.Single();
        IEnumerable<string> OfferedIn(NodeGraph graph) => registry.NodeKinds
            .Where(k => (k.AllowedIn & NodeGraphKinds.Of(graph)) != GraphKinds.None)
            .SelectMany(k => k.Suggestions)
            .Select(s => s.DisplayName);

        Assert.Contains("Log", OfferedIn(method));
        Assert.Contains("Log", OfferedIn(new ConstructorGraph { Class = cls }));
        Assert.DoesNotContain("Log", OfferedIn(cls));
        Assert.DoesNotContain("Log", OfferedIn(new TypeGraph()));

        NodeSuggestion suggestion = Assert.Single(kind.Suggestions);
        Node created = suggestion.Create(method);
        Assert.Same(kind.NodeType, created.GetType());
    }

    [Fact]
    public async Task AnExtensionNodeIsSavedReloadedAndTranslatedWithTheExtensionsCode()
    {
        (ClassGraph cls, Node log) = ExtensionGraphs.BuildLogClass(registry, "PsEx", "Logs");
        string path = await WriteJsonAsync(cls);
        Assert.Contains("\"$kind\": \"netprints.test/Log\"", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken), StringComparison.Ordinal);

        (ClassGraph reloaded, List<DocumentIssue> issues) = await ReadAsync(registry, path);

        Assert.Empty(issues);
        Node reloadedLog = Assert.Single(reloaded.Methods.Single().Nodes, n => n.GetType() == log.GetType());
        Assert.Equal(log.Id, reloadedLog.Id);
        Assert.NotNull(reloadedLog.InputDataPins[0].IncomingPin);

        string code = new ClassTranslator(registry.Translation).TranslateClass(reloaded);
        Assert.Contains("System.Console.WriteLine(varValue);", code, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")]", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADocumentWithAnExtensionNodeLoadsWithoutTheExtensionAndTheGeneratorReportsNpt003()
    {
        (ClassGraph cls, Node log) = ExtensionGraphs.BuildLogClass(registry, "PsEx", "Logs");
        string path = await WriteJsonAsync(cls);
        byte[] original = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);

        (ClassGraph withoutExtension, List<DocumentIssue> issues) = await ReadAsync(builtIn, path);

        DocumentIssue issue = Assert.Single(issues, i => i.Code == DocumentIssue.UnknownNodeKind);
        Assert.Contains("netprints.test/Log", issue.Message, StringComparison.Ordinal);
        Assert.Contains(log.Id, issue.Message, StringComparison.Ordinal);
        Assert.NotNull(withoutExtension.Methods.Single().PreservedDocumentState);

        ClassDocument saved = new DocumentMapper(builtIn.NodeConverters, NullLogger<DocumentMapper>.Instance).ToDocument(withoutExtension);
        Assert.NotNull(saved.Methods);
        UnknownNodeDocument preserved = Assert.IsType<UnknownNodeDocument>(saved.Methods.Single().Graph.Nodes.Single(n => n.Id == log.Id));
        Assert.Equal("netprints.test/Log", preserved.Kind);

        string output = Path.Combine(directory, "Logs.netpc.g.cs");
        var request = new GenerateRequest(Path.Combine(directory, "P.csproj"), "PsEx", "netprints.default", [new GraphJob(path, output)], []);
        IReadOnlyList<GeneratedFileResult> results = await GraphCodeGenerator.Create(builtIn).GenerateAsync(request, TestContext.Current.CancellationToken);

        GeneratedFileResult result = Assert.Single(results);
        CodeDiagnostic diagnostic = Assert.Single(result.Diagnostics, d => d.Severity == CodeDiagnosticSeverity.Error);
        Assert.Equal(GraphCodeGenerator.MissingExtensionCode, diagnostic.Id);
        Assert.Equal(CodeDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.False(result.Written);
        Assert.False(File.Exists(output));
        Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheProfileTemplateIsWrittenByCreateAndAnUnknownProfileIdHasNoRegistryEntry()
    {
        IProjectProfile profile = registry.FindProfile("netprints.test") ?? throw new InvalidOperationException("Profile missing.");
        var system = new MsBuildProjectSystem(new ProjectSystemOptions(registry.ProjectProperties, "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        string csproj = await system.CreateAsync(directory, "Made", profile, "Made.Ns", TestContext.Current.CancellationToken);

        string text = await File.ReadAllTextAsync(csproj, TestContext.Current.CancellationToken);
        Assert.Contains("<NetPrintsProfile>netprints.test</NetPrintsProfile>", text, StringComparison.Ordinal);
        Assert.Contains("<NetPrintsTestMode>on</NetPrintsTestMode>", text, StringComparison.Ordinal);
        Assert.Contains("<RootNamespace>Made.Ns</RootNamespace>", text, StringComparison.Ordinal);
        Assert.Contains("Version=\"9.9.9-test\"", text, StringComparison.Ordinal);

        Assert.Null(registry.FindProfile("unknown.profile"));
        Assert.Same(DefaultProjectProfile.Instance, registry.FindProfile(DefaultProjectProfile.ProfileId));
    }

    [Fact]
    public async Task AnExtensionNodeRoundTripsInCanonicalFormThroughItsOwnJsonContext()
    {
        (ClassGraph cls, Node plain) = ExtensionGraphs.BuildLogClass(registry, "PsEx", "Logs");
        NodeKindDescriptor kind = registry.NodeKinds.Single(k => k.Kind == "netprints.test/Log");
        Node named = kind.Suggestions[0].Create(cls.Methods.Single());
        named.Name = "My log";
        kind.NodeType.GetProperty("Note")?.SetValue(named, "> ");

        string path = await WriteJsonAsync(cls);
        string text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        string[] lines = text.Split('\n').Select(l => l.Trim()).ToArray();

        // Default name omitted and only common fields: one inline line.
        Assert.Contains($"{{ \"$kind\": \"netprints.test/Log\", \"id\": \"{plain.Id}\" }},", lines);

        // A name and an object property named "default": a block, the property inline.
        int open = Array.FindIndex(lines, l => l == $"\"id\": \"{named.Id}\",") - 2;
        Assert.Equal(
            [
                "{",
                "\"$kind\": \"netprints.test/Log\",",
                $"\"id\": \"{named.Id}\",",
                "\"name\": \"My log\",",
                "\"default\": { \"type\": \"System.String\", \"value\": \"> \" }",
                "}",
            ],
            lines[open..(open + 6)]);

        // Pins are referenced by key ("<direction>.<kind>.<key name>") in connections.
        Assert.Contains($"{{ \"from\": \"{plain.Id}/out.exec.Then\", \"to\":", string.Join('\n', lines), StringComparison.Ordinal);
        Assert.Contains($"\"to\": \"{plain.Id}/in.data.Value\" }}", string.Join('\n', lines), StringComparison.Ordinal);

        (ClassGraph reloaded, List<DocumentIssue> issues) = await ReadAsync(registry, path);
        Assert.Empty(issues);
        Node reloadedNamed = reloaded.Methods.Single().Nodes.Single(n => n.Id == named.Id);
        Assert.Equal("My log", reloadedNamed.Name);
        Assert.Equal("> ", kind.NodeType.GetProperty("Note")?.GetValue(reloadedNamed));
        Assert.Equal(plain.Name, reloaded.Methods.Single().Nodes.Single(n => n.Id == plain.Id).Name);

        string second = Path.Combine(directory, "second.netpc.json");
        await ExtensionGraphs.WriteAsync(registry, reloaded, second);
        Assert.Equal(text, await File.ReadAllTextAsync(second, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheTestEmittersMakeAPartialClassItsPropertiesPartialAndLeaveOtherClassesAlone() // SC-004, EX-T04 with the real asset
    {
        Project project = TestProjects.Create("PsEm", "PsEm");
        ClassGraph Class(string name)
        {
            var cls = new ClassGraph { Name = name, Namespace = "PsEm", Visibility = MemberVisibility.Public, Project = project };
            project.Classes.Add(cls);
            cls.Variables.Add(new Variable(cls, "Size", TypeSpecifier.FromType<int>(), new MethodGraph("get_Size") { Class = cls }, null, VariableModifiers.None));
            return cls;
        }

        string partial = Regex.Replace(new ClassTranslator(registry.Translation).TranslateClass(Class("PartialWidget")), @"\s+", " ");
        string plain = Regex.Replace(new ClassTranslator(registry.Translation).TranslateClass(Class("Ordinary")), @"\s+", " ");

        Assert.Contains("using System.Linq;", partial, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")] public partial class PartialWidget", partial, StringComparison.Ordinal);
        Assert.Contains("private partial System.Int32 Size { get; }", partial, StringComparison.Ordinal);

        Assert.Contains("using System.Linq;", plain, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")] public class Ordinary", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("partial", plain, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTestExtensionsSettingsSectionDefaultsRoundTripsAndSurvivesARestart() // SC-004, EX-T09 with the real asset
    {
        ExtensionSettingsDescriptor descriptor = Assert.Single(registry.Settings, d => d.ExtensionId == "netprints.test");
        string file = Path.Combine(directory, "settings.json");
        MethodInfo get = typeof(ISettingsStore).GetMethod(nameof(ISettingsStore.Get))?.MakeGenericMethod(descriptor.ValueType)
            ?? throw new InvalidOperationException("Get not found.");
        MethodInfo set = typeof(ISettingsStore).GetMethod(nameof(ISettingsStore.SetAsync))?.MakeGenericMethod(descriptor.ValueType)
            ?? throw new InvalidOperationException("SetAsync not found.");
        PropertyInfo greeting = descriptor.ValueType.GetProperty("Greeting") ?? throw new InvalidOperationException("Greeting not found.");

        string Read() => (string?)greeting.GetValue(get.Invoke(new JsonFileSettingsStore(file, NullLogger<JsonFileSettingsStore>.Instance), [descriptor])) ?? string.Empty;

        Assert.Equal("hello", Read());

        object value = Activator.CreateInstance(descriptor.ValueType, "world") ?? throw new InvalidOperationException("Settings not created.");
        await (ValueTask)(set.Invoke(new JsonFileSettingsStore(file, NullLogger<JsonFileSettingsStore>.Instance), [descriptor, value, TestContext.Current.CancellationToken])
            ?? throw new InvalidOperationException("SetAsync returned nothing."));

        Assert.Equal("world", Read());
        Assert.Contains("\"netprints.test\"", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
