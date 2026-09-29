using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Projects;
using NetPrints.Reflection;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Serialization.Stores;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>File picker returning queued answers (null = cancelled).</summary>
public sealed class FakeFilePicker : IFilePickerService
{
    public Queue<string?> OpenFileAnswers { get; } = new();
    public Queue<string?> SaveFileAnswers { get; } = new();
    public Queue<string?> FolderAnswers { get; } = new();
    public List<string> Calls { get; } = [];

    public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters)
    {
        Calls.Add($"open:{title}:{string.Join(";", filters.SelectMany(f => f.Patterns))}");
        return Task.FromResult(OpenFileAnswers.Count > 0 ? OpenFileAnswers.Dequeue() : null);
    }

    public Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters)
    {
        Calls.Add($"save:{title}:{suggestedName}:{string.Join(";", filters.SelectMany(f => f.Patterns))}");
        return Task.FromResult(SaveFileAnswers.Count > 0 ? SaveFileAnswers.Dequeue() : null);
    }

    public Task<string?> OpenFolderAsync(string title)
    {
        Calls.Add($"folder:{title}");
        return Task.FromResult(FolderAnswers.Count > 0 ? FolderAnswers.Dequeue() : null);
    }
}

/// <summary>Dialogs that record calls and return canned values.</summary>
public sealed class FakeDialogs : IEditorDialogs
{
    public List<(string Title, string Message)> Errors { get; } = [];
    public List<TypeSpecifier> SelectTypeCalls { get; } = [];
    public int SelectMethodCalls { get; private set; }
    public List<MethodSpecifier> LastMethods { get; private set; } = [];
    public List<ReferenceListVM> ReferenceDialogs { get; } = [];
    public TypeSpecifier? TypeAnswer { get; set; } = TypeSpecifier.FromType<int>();
    public Func<IReadOnlyList<MethodSpecifier>, MethodSpecifier?> MethodAnswer { get; set; } = m => m.FirstOrDefault();
    public List<(string ProjectPath, IReadOnlyList<string> Folders)> TrustCalls { get; } = [];
    public bool TrustAnswer { get; set; }
    public List<(string Title, IReadOnlyList<CodeDiagnostic> Issues)> IssueDialogs { get; } = [];

    public Task<bool> ConfirmTrustAsync(string projectPath, IReadOnlyList<string> extensionFolders)
    {
        TrustCalls.Add((projectPath, extensionFolders));
        return Task.FromResult(TrustAnswer);
    }

    public Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues)
    {
        IssueDialogs.Add((title, issues));
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        Errors.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial)
    {
        SelectTypeCalls.Add(initial);
        return Task.FromResult(TypeAnswer);
    }

    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods)
    {
        SelectMethodCalls++;
        LastMethods = methods.ToList();
        return Task.FromResult(MethodAnswer(LastMethods));
    }

    public Task ShowReferencesAsync(ReferenceListVM references)
    {
        ReferenceDialogs.Add(references);
        return Task.CompletedTask;
    }
}

public sealed class FakeClipboard : IClipboardService
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}

/// <summary>Settings kept in memory.</summary>
public sealed class FakeSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    public int Writes { get; private set; }

    public T Get<T>(ExtensionSettingsDescriptor<T> descriptor) =>
        values.TryGetValue(descriptor.ExtensionId, out object? value) && value is T typed ? typed : descriptor.Default;

    public ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken)
    {
        values[descriptor.ExtensionId] = value;
        Writes++;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Runs everything inline on the calling thread.</summary>
public sealed class InlineDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public bool CheckAccess() => true;
}

public sealed class FakeWindowService : IWindowService
{
    public Dictionary<ClassGraph, ClassEditorVM> Open { get; } = new(ReferenceEqualityComparer.Instance);
    public List<ClassGraph> Activated { get; } = [];
    public List<ClassGraph> Closed { get; } = [];
    public int CloseAllCount { get; private set; }

    public bool TryActivateClassEditor(ClassGraph cls)
    {
        if (Open.ContainsKey(cls))
        {
            Activated.Add(cls);
            return true;
        }

        return false;
    }

    public void OpenClassEditor(ClassGraph cls, EditorContext context) => Open[cls] = new ClassEditorVM(cls, context);

    public void CloseClassEditor(ClassGraph cls)
    {
        if (Open.Remove(cls, out var editor))
        {
            editor.Dispose();
            Closed.Add(cls);
        }
    }

    public void CloseAllClassEditors()
    {
        CloseAllCount++;
        foreach (var cls in Open.Keys.ToList())
        {
            CloseClassEditor(cls);
        }
    }
}

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<ProcessStartRequest> Started { get; } = [];

    public event Action<string>? OutputReceived;

    public void Start(ProcessStartRequest request) => Started.Add(request);

    /// <summary>Simulates a line of output, for tests of the Output pane wiring.</summary>
    public void Raise(string line) => OutputReceived?.Invoke(line);
}

/// <summary>
/// <see cref="IProjectSystem"/> fake, stateful per project path: <see cref="LoadAsync"/> synthesizes
/// a snapshot from the file on disk (<c>RootNamespace</c>/<c>OutputType</c>/<c>NetPrintsProfile</c>
/// read from the <c>.csproj</c> XML, graph files listed by scanning for <c>*.netpc.json</c>) unless
/// a test <see cref="Seed"/>s one first; <see cref="ApplyAsync"/> mutates <see cref="ProjectSnapshot.DeclaredReferences"/>
/// and <see cref="ProjectSnapshot.OutputType"/> per the project-system.md §4 contract (duplicate
/// assembly/source-directory references are no-ops; removing a non-editable reference throws).
/// </summary>
public sealed class FakeProjectSystem : IProjectSystem
{
    private readonly Dictionary<string, ProjectSnapshot> snapshots = new(StringComparer.Ordinal);

    public List<string> LoadCalls { get; } = [];
    public Func<string, BuildResult>? BuildResultFactory { get; set; }
    public Func<string, ProcessStartRequest>? RunCommandFactory { get; set; }

    public void Seed(ProjectSnapshot snapshot) => snapshots[snapshot.ProjectFilePath] = snapshot;

    public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        LoadCalls.Add(projectFilePath);
        if (!snapshots.TryGetValue(projectFilePath, out ProjectSnapshot? snapshot))
        {
            snapshot = SynthesizeSnapshot(projectFilePath);
            snapshots[projectFilePath] = snapshot;
        }

        return Task.FromResult(snapshot);
    }

    public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken)
    {
        ProjectSnapshot snapshot = snapshots.TryGetValue(projectFilePath, out ProjectSnapshot? existing)
            ? existing
            : SynthesizeSnapshot(projectFilePath);
        var declared = snapshot.DeclaredReferences.ToList();

        foreach (ProjectEdit edit in edits)
        {
            switch (edit)
            {
                case ProjectEdit.SetOutputType set:
                    snapshot = snapshot with { OutputType = set.Value };
                    break;
                case ProjectEdit.SetProfile setProfile:
                    snapshot = snapshot with { ProfileId = setProfile.ProfileId };
                    break;
                case ProjectEdit.AddAssemblyReference add:
                    string fullAssemblyPath = Path.GetFullPath(add.AssemblyPath);
                    if (!declared.Any(r => r.Kind == DeclaredReferenceKind.Assembly && string.Equals(Path.GetFullPath(r.Include), fullAssemblyPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        declared.Add(new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, add.AssemblyPath, null, true, true));
                    }

                    break;
                case ProjectEdit.AddSourceDirectory addDir:
                    string fullDirPath = Path.GetFullPath(addDir.DirectoryPath);
                    if (!declared.Any(r => r.Kind == DeclaredReferenceKind.SourceDirectory && string.Equals(Path.GetFullPath(r.Include), fullDirPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        declared.Add(new ProjectReferenceInfo(DeclaredReferenceKind.SourceDirectory, addDir.DirectoryPath, null, true, true));
                    }

                    break;
                case ProjectEdit.SetSourceDirectoryIncluded setIncluded:
                    for (int i = 0; i < declared.Count; i++)
                    {
                        if (declared[i].Kind == DeclaredReferenceKind.SourceDirectory
                            && string.Equals(declared[i].Include, setIncluded.DirectoryPath, StringComparison.OrdinalIgnoreCase))
                        {
                            declared[i] = declared[i] with { Included = setIncluded.Included };
                        }
                    }

                    break;
                case ProjectEdit.RemoveReference remove:
                    ProjectReferenceInfo? match = declared.FirstOrDefault(r => r.Kind == remove.Kind && string.Equals(r.Include, remove.Include, StringComparison.Ordinal));
                    if (match is not null)
                    {
                        if (!match.Editable)
                        {
                            throw new ArgumentException($"Reference '{match.Include}' is not editable.");
                        }

                        declared.Remove(match);
                    }

                    break;
                case ProjectEdit.AddNetPrintsSdk:
                    snapshot = snapshot with { ReferencesNetPrintsSdk = true };
                    break;
            }
        }

        snapshot = snapshot with { DeclaredReferences = declared };
        snapshots[projectFilePath] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken)
    {
        string path = Path.Combine(directory, $"{projectName}.csproj");
        if (File.Exists(path))
        {
            throw new IOException($"'{path}' already exists.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <RootNamespace>{rootNamespace}</RootNamespace>
                <NetPrintsProfile>{profile.Id}</NetPrintsProfile>
              </PropertyGroup>
            </Project>
            """);

        var snapshot = new ProjectSnapshot(path, projectName, rootNamespace, projectName, BinaryType.Executable,
            profile.DefaultTargetFramework, profile.Id, true, [], [], [], [], [], "{}", new Dictionary<string, string>(), []);
        snapshots[path] = snapshot;
        return Task.FromResult(path);
    }

    public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
        Task.FromResult(BuildResultFactory?.Invoke(projectFilePath) ?? new BuildResult(true, [], null, ""));

    public ProcessStartRequest GetRunCommand(string projectFilePath) =>
        RunCommandFactory?.Invoke(projectFilePath) ?? new ProcessStartRequest("dotnet", ["run"], Path.GetDirectoryName(projectFilePath) ?? "");

    private static ProjectSnapshot SynthesizeSnapshot(string projectFilePath)
    {
        string directory = Path.GetDirectoryName(projectFilePath) ?? throw new InvalidOperationException($"'{projectFilePath}' has no directory.");
        string xml = File.ReadAllText(projectFilePath);
        string name = Path.GetFileNameWithoutExtension(projectFilePath);
        string rootNamespace = ExtractXmlValue(xml, "RootNamespace") ?? name;
        bool isExecutable = string.Equals(ExtractXmlValue(xml, "OutputType"), "Exe", StringComparison.OrdinalIgnoreCase);
        string profileId = ExtractXmlValue(xml, "NetPrintsProfile") ?? DefaultProjectProfile.ProfileId;
        var graphFiles = Directory.GetFiles(directory, "*.netpc.json", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        var references = TestSnapshots.RuntimeAssemblyPaths()
            .Select(path => new ResolvedAssembly(path, null)).ToList();

        return new ProjectSnapshot(projectFilePath, name, rootNamespace, name,
            isExecutable ? BinaryType.Executable : BinaryType.SharedLibrary, "net10.0", profileId, true,
            graphFiles, [], references, [], [], "{}", new Dictionary<string, string>(), []);
    }

    private static string? ExtractXmlValue(string xml, string tag)
    {
        var match = System.Text.RegularExpressions.Regex.Match(xml, $"<{tag}>(.*?)</{tag}>");
        return match.Success ? match.Groups[1].Value : null;
    }
}

/// <summary>
/// Editor context for one test: fresh fakes around a reflection host, a <see cref="FakeProjectSystem"/>
/// and a real <see cref="ProjectPersistence"/> (JSON graphs on the real file system). Resolved per
/// test class from <see cref="Startup"/>; tests that need an unloaded host construct their own.
/// </summary>
public sealed class TestEditor : IDisposable
{
    public TestEditor(IReflectionHost reflection)
        : this(reflection, TestExtensions.CreateBuiltIn(), NullHostChannel.Instance)
    {
    }

    private TestEditor(IReflectionHost reflection, ExtensionHost extensions, IHostChannel hostChannel)
    {
        ArgumentNullException.ThrowIfNull(reflection);
        Reflection = reflection;
        Persistence = CreatePersistence(Projects);
        Extensions = extensions;
        _ = PersistenceBinding.Bind(Persistence, Extensions, NullLoggerFactory.Instance);
        CodeAnalysis = new CodeAnalysisHost(Reflection, Extensions, Scheduler, Dispatcher, NullLogger<CodeAnalysisHost>.Instance);

        Context = new EditorContext(FilePicker, Dialogs, Clipboard, Dispatcher, Reflection, Windows, Processes,
            Scheduler, () => new StrongReferenceMessenger(), NullLoggerFactory.Instance, Projects, Persistence,
            Extensions, hostChannel, Settings, CodeAnalysis);
    }

    /// <summary>
    /// Creates an editor whose reflection host is built over the editor's own extension host, so a project's extensions
    /// (catalogs, translators) reach it; <paramref name="hostChannel"/> defaults to the null channel.
    /// </summary>
    public static TestEditor Create(Func<IExtensionHost, IReflectionHost> createReflection, ExtensionHost? extensions = null, IHostChannel? hostChannel = null)
    {
        ExtensionHost host = extensions ?? TestExtensions.CreateBuiltIn();
        return new TestEditor(createReflection(host), host, hostChannel ?? NullHostChannel.Instance);
    }

    /// <summary>A real reflection host over <paramref name="extensions"/> that publishes inline.</summary>
    public static ReflectionHost CreateReflectionHost(IExtensionHost extensions) =>
        new(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);

    /// <summary>Builds a real, JSON-backed <see cref="ProjectPersistence"/> over any <see cref="IProjectSystem"/>.</summary>
    public static ProjectPersistence CreatePersistence(IProjectSystem projects)
    {
        var nodeConverters = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
        var mapper = new DocumentMapper(nodeConverters, NullLogger<DocumentMapper>.Instance);
        var formats = new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(nodeConverters), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance))]);
        return new ProjectPersistence(projects, formats, mapper,
            directory => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, NullLogger<FileSystemDocumentStore>.Instance),
            NullLogger<ProjectPersistence>.Instance);
    }

    public FakeFilePicker FilePicker { get; } = new();
    public FakeDialogs Dialogs { get; } = new();
    public FakeClipboard Clipboard { get; } = new();
    public InlineDispatcher Dispatcher { get; } = new();
    public FakeWindowService Windows { get; } = new();
    public FakeProcessLauncher Processes { get; } = new();
    public IReflectionHost Reflection { get; }
    public FakeProjectSystem Projects { get; } = new();
    public ProjectPersistence Persistence { get; }

    /// <summary>The real extension host with only the built-in extension; project folders load through it.</summary>
    public ExtensionHost Extensions { get; }

    public FakeSettingsStore Settings { get; } = new();

    /// <summary>Virtual time for throttled work (the search box, the generated-code loop and live analysis debounce).</summary>
    public TestScheduler Scheduler { get; } = new();

    /// <summary>Debounced live analysis, over the same <see cref="Reflection"/> and <see cref="Scheduler"/> (ED-T02).</summary>
    public ICodeAnalysisHost CodeAnalysis { get; }

    public EditorContext Context { get; }

    /// <summary>Disposes <see cref="CodeAnalysis"/>.</summary>
    public void Dispose() => CodeAnalysis.Dispose();
}

/// <summary>
/// Wraps a real, loaded <see cref="IReflectionProvider"/>, letting a test block
/// <see cref="GetPublicMethodOverloads"/> and <see cref="GetConstructors"/> on a gate it controls
/// (R2-05): <see cref="ClassEditorVM"/>'s real seam for warming a graph's overload lookups before
/// opening it, used instead of a test-only hook on the production view model itself.
/// </summary>
public sealed class GatedReflectionProvider(IReflectionProvider inner) : IReflectionProvider
{
    /// <summary>
    /// Set by a test to hold <see cref="GetPublicMethodOverloads"/>/<see cref="GetConstructors"/> "in
    /// flight" until completed, deterministically instead of racing real background work. Null (the
    /// default) does not gate at all.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b) => inner.TypeSpecifierIsSubclassOf(a, b);

    public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType) => inner.HasImplicitCast(fromType, toType);

    public IEnumerable<TypeSpecifier> GetNonStaticTypes() => inner.GetNonStaticTypes();

    public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier) => inner.GetOverridableMethodsForType(typeSpecifier);

    public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
    {
        Gate?.Task.GetAwaiter().GetResult();
        return inner.GetPublicMethodOverloads(methodSpecifier);
    }

    public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
    {
        Gate?.Task.GetAwaiter().GetResult();
        return inner.GetConstructors(typeSpecifier);
    }

    public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier) => inner.GetEnumNames(typeSpecifier);

    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query) => inner.GetMethods(query);

    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query) => inner.GetVariables(query);

    public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) => inner.GetMethodDocumentation(methodSpecifier);

    public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) =>
        inner.GetMethodParameterDocumentation(methodSpecifier, parameterIndex);

    public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) =>
        inner.GetMethodReturnDocumentation(methodSpecifier, returnIndex);
}

/// <summary>Wraps a real, already-loaded <see cref="IReflectionHost"/>, exposing its provider through a
/// <see cref="GatedReflectionProvider"/> a test can gate (see <see cref="GatedProvider"/>).</summary>
public sealed class GatedReflectionHost : IReflectionHost
{
    private readonly IReflectionHost inner;

    public GatedReflectionHost(IReflectionHost inner)
    {
        this.inner = inner;
        GatedProvider = new GatedReflectionProvider(inner.Provider);
    }

    /// <summary>The gate a test sets to hold a warm-up lookup "in flight" (see <see cref="GatedReflectionProvider.Gate"/>).</summary>
    public GatedReflectionProvider GatedProvider { get; }

    public bool IsLoaded => inner.IsLoaded;

    public Task Loaded => inner.Loaded;

    public IReflectionProvider Provider => GatedProvider;

    public ProjectSnapshot? Snapshot => inner.Snapshot;

    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

    public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

    public event EventHandler? Reloaded
    {
        add => inner.Reloaded += value;
        remove => inner.Reloaded -= value;
    }

    public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => inner.ReloadAsync(project, cancellationToken);
}

/// <summary>Extension hosts for tests.</summary>
public static class TestExtensions
{
    /// <summary>A real extension host with only the built-in extension.</summary>
    public static ExtensionHost CreateBuiltIn() => new(ExtensionLoaderOptions.BuiltInOnly, NullLoggerFactory.Instance);
}

/// <summary>Where the built <c>NetPrints.TestExtension</c> asset (T071) lands for the running configuration.</summary>
public static class TestExtensionFolder
{
    /// <summary>The extension's output folder: its dll, its manifest and nothing the host already supplies.</summary>
    public static string Folder { get; } = Path.Combine(FindRepositoryRoot(), "tests", "NetPrints.TestExtension", "bin", DetectConfiguration(), "extensions", "netprints.test");

    /// <summary>Copies <see cref="Folder"/> into <paramref name="destinationRoot"/>/ext and returns the copy.</summary>
    public static string CopyTo(string destinationRoot)
    {
        string destination = Path.Combine(destinationRoot, "ext");
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(Folder))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        return destination;
    }

    /// <summary>A real extension host with the built-in extension and the test extension loaded from its output folder.</summary>
    public static ExtensionHost CreateHost() =>
        new(ExtensionLoaderOptions.BuiltInOnly with { ExtensionFolders = [Folder] }, NullLoggerFactory.Instance);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NetPrints.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("NetPrints.slnx was not found above the test output.");
    }

    private static string DetectConfiguration()
    {
        string[] segments = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int frameworkIndex = Array.LastIndexOf(segments, "net10.0");
        return frameworkIndex > 0 ? segments[frameworkIndex - 1] : "Release";
    }
}
