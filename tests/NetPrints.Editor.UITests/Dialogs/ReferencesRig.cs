using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>A references list over a project with four declared references, with the host services it needs, for the references dialog tests.</summary>
internal sealed class ReferencesRig : IDisposable
{
    private readonly CodeAnalysisHost codeAnalysis;

    public ReferencesRig()
    {
        var snapshot = new ProjectSnapshot("/tmp/P.csproj", "P", "N", "P",
            BinaryType.SharedLibrary, "net10.0", DefaultProjectProfile.ProfileId, true, [], [], [],
            [
                new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "System.dll", null, true, true),
                new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "System.Core", null, true, true),
                new ProjectReferenceInfo(DeclaredReferenceKind.Assembly, "mscorlib", null, true, true),
                new ProjectReferenceInfo(DeclaredReferenceKind.SourceDirectory, "/tmp/src", null, false, true),
            ],
            [], "{}", new Dictionary<string, string>(), []);
        var project = Project.FromSnapshot(snapshot);
        var dispatcher = new NetPrints.Editor.Hosting.Avalonia.AvaloniaUiDispatcher();
        var noSdkProjects = new NoSdkProjectSystem();
        var extensions = new Extensibility.Loading.ExtensionHost(Extensibility.Loading.ExtensionLoaderOptions.BuiltInOnly, NullLoggerFactory.Instance);
        var reflection = new ReflectionHost(dispatcher, extensions, NullLogger<ReflectionHost>.Instance);
        codeAnalysis = new CodeAnalysisHost(reflection, extensions, System.Reactive.Concurrency.DefaultScheduler.Instance, dispatcher, NullLogger<CodeAnalysisHost>.Instance);
        var processes = new CapturingProcessLauncher();
        var context = new EditorContext(new QueuedFilePicker(), new RecordingDialogs(), new NoClipboard(), dispatcher,
            reflection,
            new NetPrints.Editor.Hosting.Avalonia.WindowService(), processes,
            System.Reactive.Concurrency.DefaultScheduler.Instance,
            () => new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger(), NullLoggerFactory.Instance,
            noSdkProjects, TestPersistence.Create(noSdkProjects),
            extensions,
            Extensibility.Hosting.NullHostChannel.Instance,
            new Extensibility.Settings.JsonFileSettingsStore(Path.Combine(Path.GetTempPath(), "netprints-unused", "settings.json"),
                NullLogger<Extensibility.Settings.JsonFileSettingsStore>.Instance),
            codeAnalysis,
            new RunStateTracker(processes));
        References = new ReferenceListViewModel(project, context);
    }

    public ReferenceListViewModel References { get; }

    public void Dispose()
    {
        References.Dispose();
        codeAnalysis.Dispose();
    }

    private sealed class NoClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    /// <summary>A real, JSON-backed persistence over a project system this test never calls.</summary>
    private static class TestPersistence
    {
        public static Serialization.ProjectPersistence Create(IProjectSystem projects)
        {
            var nodeConverters = new Serialization.Mapping.NodeDocumentConverterRegistry(Serialization.Mapping.NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new Serialization.Mapping.DocumentMapper(nodeConverters, NullLogger<Serialization.Mapping.DocumentMapper>.Instance);
            var formats = new Serialization.DocumentFormatRegistry([
                new Serialization.Json.JsonDocumentFormat(
                    new Serialization.Json.NetPrintsJsonOptions(nodeConverters),
                    new Serialization.Migrations.DocumentMigrator([], NullLogger<Serialization.Migrations.DocumentMigrator>.Instance))]);
            return new Serialization.ProjectPersistence(projects, formats, mapper,
                (directory, watch) => new Serialization.Stores.FileSystemDocumentStore(directory,
                    System.Reactive.Concurrency.DefaultScheduler.Instance, NullLogger<Serialization.Stores.FileSystemDocumentStore>.Instance, watch),
                NullLogger<Serialization.ProjectPersistence>.Instance);
        }
    }
}
