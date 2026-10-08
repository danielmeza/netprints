using System.Diagnostics;
using System.Text;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.References;
using NetPrints.Projects;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Hosting;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>Dialogs that record calls instead of opening modal windows.</summary>
public sealed class RecordingDialogs : IEditorDialogs
{
    public List<(string Title, string Message)> Errors { get; } = [];
    public List<TypeSpecifier> SelectTypeCalls { get; } = [];
    public TypeSpecifier? TypeAnswer { get; set; } = TypeSpecifier.FromType<int>();

    /// <summary>When set, the references dialog is shown for real (non-modal) instead of being skipped.</summary>
    public Func<ReferenceListViewModel, Task>? ShowReferences { get; set; }

    public Task ShowErrorAsync(string title, string message)
    {
        Errors.Add((title, message));
        return Task.CompletedTask;
    }

    public void ShowNotification(string title, string message)
    {
        // Do nothing for now in the fake
    }

    public Task<TypeSpecifier?> SelectTypeAsync(IEnumerable<TypeSpecifier> types, TypeSpecifier initial)
    {
        SelectTypeCalls.Add(initial);
        return Task.FromResult(TypeAnswer);
    }

    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods) =>
        Task.FromResult(methods.FirstOrDefault());

    /// <summary>What <see cref="ConfirmTrustAsync"/> answers.</summary>
    public bool TrustAnswer { get; set; }

    /// <summary>When set, the issues dialog is shown for real (non-modal) instead of only being recorded.</summary>
    public Func<string, IReadOnlyList<CodeDiagnostic>, Task>? ShowIssues { get; set; }

    public List<(string Title, IReadOnlyList<CodeDiagnostic> Issues)> IssueDialogs { get; } = [];

    public Task<bool> ConfirmTrustAsync(string projectPath, IReadOnlyList<string> extensionFolders) => Task.FromResult(TrustAnswer);

    /// <summary>What <see cref="ConfirmAsync"/> answers.</summary>
    public bool ConfirmAnswer { get; set; } = true;

    public List<(string Title, string Message)> ConfirmCalls { get; } = [];

    /// <summary>What <see cref="ConfirmUnsavedAsync"/> answers.</summary>
    public UnloadChoice UnsavedAnswer { get; set; } = UnloadChoice.Cancel;

    public List<IReadOnlyList<UnsavedFile>> UnsavedCalls { get; } = [];

    /// <summary>What <see cref="ConfirmRecoverAsync"/> answers.</summary>
    public RecoveryChoice RecoverAnswer { get; set; } = RecoveryChoice.Later;

    public List<IReadOnlyList<RecoveryFile>> RecoverCalls { get; } = [];

    /// <summary>The paths <see cref="ConfirmRecoverAsync"/> chooses to restore with <see cref="RecoveryChoice.Restore"/>; every offered file when null.</summary>
    public IReadOnlyCollection<string>? RecoverRestorePaths { get; set; }

    public Task<RecoveryAnswer> ConfirmRecoverAsync(IReadOnlyList<RecoveryFile> files)
    {
        RecoverCalls.Add(files);
        return Task.FromResult(new RecoveryAnswer(RecoverAnswer, RecoverAnswer == RecoveryChoice.Restore ? RecoverRestorePaths ?? [.. files.Select(file => file.Path)] : []));
    }

    public Task<UnloadChoice> ConfirmUnsavedAsync(IReadOnlyList<UnsavedFile> files)
    {
        UnsavedCalls.Add(files);
        return Task.FromResult(UnsavedAnswer);
    }

    public Task<SampleTargetChoice> ConfirmSampleTargetAsync(string sampleName, string targetFolder) => Task.FromResult(SampleTargetChoice.Open);

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        ConfirmCalls.Add((title, message));
        return Task.FromResult(ConfirmAnswer);
    }

    public Task ShowIssuesAsync(string title, IReadOnlyList<CodeDiagnostic> issues)
    {
        IssueDialogs.Add((title, issues));
        return ShowIssues?.Invoke(title, issues) ?? Task.CompletedTask;
    }

    public Task ShowKeyboardShortcutsAsync(KeyboardShortcutsViewModel sheet) => Task.CompletedTask;

    public Task ShowAboutAsync(AboutViewModel about) => Task.CompletedTask;

    public Task ShowCommandPaletteAsync(NetPrints.Editor.Commands.CommandPalette.CommandPaletteViewModel palette) => Task.CompletedTask;

    /// <summary>Shows the New project dialog and answers with its result; when null the dialog is cancelled.</summary>
    public Func<NewProjectDialogViewModel, Task<string?>>? ShowNewProject { get; set; }

    public Task<string?> ShowNewProjectAsync(NewProjectDialogViewModel dialog) => ShowNewProject?.Invoke(dialog) ?? Task.FromResult<string?>(null);

    public Task ShowReferencesAsync(ReferenceListViewModel references)
    {
        if (ShowReferences is not null)
        {
            return ShowReferences(references);
        }

        references.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Starts processes for real through the editor's own <see cref="ProcessLauncher"/> (its exit follows the drain of the output),
/// with their output captured (the headless stand-in for the editor's terminal), and records what was started.
/// </summary>
public sealed class CapturingProcessLauncher : IProcessLauncher, IDisposable
{
    private readonly StringBuilder output = new();
    private readonly ProcessLauncher launcher = new();
    private readonly List<CancellationTokenSource> runs = [];

    public CapturingProcessLauncher()
    {
        launcher.OutputReceived += line =>
        {
            lock (output)
            {
                output.AppendLine(line);
            }

            OutputReceived?.Invoke(line);
        };
        launcher.ProcessStarted += (id, request) => ProcessStarted?.Invoke(id, request);
        launcher.LineReceived += (id, stream, line) => LineReceived?.Invoke(id, stream, line);
        launcher.ProcessExited += (id, code) => ProcessExited?.Invoke(id, code);
    }

    public List<ProcessStartRequest> Started { get; } = [];

    public event Action<string>? OutputReceived;

    public event Action<int, ProcessStartRequest>? ProcessStarted;

    public event Action<int, ProcessStream, string>? LineReceived;

    public event Action<int, int>? ProcessExited;

    public string Output
    {
        get
        {
            lock (output)
            {
                return output.ToString();
            }
        }
    }

    public void Start(ProcessStartRequest request, CancellationToken cancellationToken = default)
    {
        Started.Add(request);
        var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); // cancelled on dispose, which kills the program
        runs.Add(run);
        launcher.Start(request, run.Token);
    }

    public void Dispose()
    {
        foreach (var run in runs)
        {
            run.Cancel();
            run.Dispose();
        }
    }
}

/// <summary>A file picker primed with answers (the headless stand-in for the platform dialogs).</summary>
public sealed class QueuedFilePicker : IFilePickerService, IFileDialogs
{
    private readonly Queue<(string Kind, string Title, string? Path)> answers = new();

    public List<string> Requests { get; } = [];

    public void Enqueue(string kind, string title, string? path) => answers.Enqueue((kind, title, path));

    private Task<string?> Answer(string kind, string title)
    {
        Requests.Add($"{kind}:{title}");
        if (answers.Count == 0)
        {
            return Task.FromResult<string?>(null); // cancelled
        }

        var (expectedKind, expectedTitle, path) = answers.Dequeue();
        if (expectedKind != kind || expectedTitle != title)
        {
            throw new InvalidOperationException($"Expected a {expectedKind} picker '{expectedTitle}', got a {kind} picker '{title}'.");
        }

        return Task.FromResult(path);
    }

    public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters) => Answer("open", title);

    public Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters) => Answer("save", title);

    public Task<string?> OpenFolderAsync(string title) => Answer("folder", title);

    Task IFileDialogs.OpenFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken)
    {
        Enqueue("open", title, path);
        return trigger();
    }

    Task IFileDialogs.SaveFileAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken)
    {
        Enqueue("save", title, path);
        return trigger();
    }

    Task IFileDialogs.OpenFolderAsync(string title, string path, Func<Task> trigger, CancellationToken cancellationToken)
    {
        Enqueue("folder", title, path);
        return trigger();
    }
}

/// <summary>
/// Copies the checked-in <c>samples/HelloWorld</c> to a temporary folder, with a local-SDK layout
/// (project-system.md §2.1) so the copy builds against this repository's own generator instead of
/// the (unpublished) <c>NetPrints.Sdk</c> NuGet package (T059/T062a, research.md R21).
/// </summary>
public sealed class SampleCopy : IDisposable
{
    public SampleCopy()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
        Directory = Path.Combine(Path.GetTempPath(), "netprints-ui-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        foreach (string file in System.IO.Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(Directory, Path.GetFileName(file)));
        }

        LocalSdkLayout.Write(Directory);
    }

    public string Directory { get; }

    public string ProjectPath => Path.Combine(Directory, "HelloWorld.csproj");

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Directory, true);
        }
        catch (IOException)
        {
            // A compiled program may still hold a file; the temp folder is cleaned by the OS.
        }
    }
}
