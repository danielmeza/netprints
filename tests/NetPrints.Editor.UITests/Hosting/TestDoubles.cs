using System.Diagnostics;
using System.Text;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Testing.Ui.Hosting;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>Dialogs that record calls instead of opening modal windows.</summary>
public sealed class RecordingDialogs : IEditorDialogs
{
    public List<(string Title, string Message)> Errors { get; } = [];
    public List<TypeSpecifier> SelectTypeCalls { get; } = [];
    public TypeSpecifier? TypeAnswer { get; set; } = TypeSpecifier.FromType<int>();

    /// <summary>When set, the references dialog is shown for real (non-modal) instead of being skipped.</summary>
    public Func<ReferenceListVM, Task>? ShowReferences { get; set; }

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

    public Task<MethodSpecifier?> SelectMethodAsync(IEnumerable<MethodSpecifier> methods) =>
        Task.FromResult(methods.FirstOrDefault());

    public Task ShowReferencesAsync(ReferenceListVM references)
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
/// Starts processes for real with their output captured (the headless stand-in for the editor's
/// terminal), and records what was started.
/// </summary>
public sealed class CapturingProcessLauncher : IProcessLauncher, IDisposable
{
    private readonly StringBuilder output = new();
    private readonly List<Process> processes = [];

    public List<(string FileName, string? Arguments)> Started { get; } = [];

    public event Action<string>? OutputReceived;

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

    public void Start(string fileName, string? arguments)
    {
        Started.Add((fileName, arguments));
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments ?? "")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true,
        };
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        process.Exited += (_, _) => OutputReceived?.Invoke($"Process exited (code {process.ExitCode}).");
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        processes.Add(process);
    }

    private void Append(string? line)
    {
        if (line is not null)
        {
            lock (output)
            {
                output.AppendLine(line);
            }

            OutputReceived?.Invoke(line);
        }
    }

    public void Dispose()
    {
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            process.Dispose();
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

/// <summary>Copies the checked-in HelloWorld sample to a temporary folder.</summary>
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
    }

    public string Directory { get; }

    public string ProjectPath => Path.Combine(Directory, "HelloWorld.netpp");

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
