using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Projects;
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

    public void OpenClassEditor(ClassEditorVM editor) => Open[editor.Class] = editor;

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
        var references = ReferenceAssemblyResolver.GetRuntimeAssemblyPaths()
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
public sealed class TestEditor
{
    public TestEditor(IReflectionHost reflection)
    {
        ArgumentNullException.ThrowIfNull(reflection);
        Reflection = reflection;
        Persistence = CreatePersistence(Projects);

        Context = new EditorContext(FilePicker, Dialogs, Clipboard, Dispatcher, Reflection, Windows, Processes,
            Scheduler, Scheduler, () => new StrongReferenceMessenger(), NullLoggerFactory.Instance, Projects, Persistence);
    }

    /// <summary>Builds a real, JSON-backed <see cref="ProjectPersistence"/> over any <see cref="IProjectSystem"/>.</summary>
    public static ProjectPersistence CreatePersistence(IProjectSystem projects)
    {
        var nodeConverters = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
        var mapper = new DocumentMapper(nodeConverters);
        var formats = new DocumentFormatRegistry([new JsonDocumentFormat(new NetPrintsJsonOptions(nodeConverters), new DocumentMigrator([]))]);
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

    /// <summary>Virtual time for throttled work (the search box) and the generated-code loop.</summary>
    public TestScheduler Scheduler { get; } = new();
    public EditorContext Context { get; }
}
