using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Reactive.Testing;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;

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
    public List<(string FileName, string? Arguments)> Started { get; } = [];

    public event Action<string>? OutputReceived;

    public void Start(string fileName, string? arguments) => Started.Add((fileName, arguments));

    /// <summary>Simulates a line of output, for tests of the Output pane wiring.</summary>
    public void Raise(string line) => OutputReceived?.Invoke(line);
}

/// <summary>
/// Editor context for one test: fresh fakes around a reflection host. Resolved per test class
/// from <see cref="Startup"/>; tests that need an unloaded host construct their own.
/// </summary>
public sealed class TestEditor
{
    public TestEditor(IReflectionHost reflection)
    {
        ArgumentNullException.ThrowIfNull(reflection);
        Reflection = reflection;
        Context = new EditorContext(FilePicker, Dialogs, Clipboard, Dispatcher, Reflection, Windows, Processes,
            Scheduler, Scheduler, () => new StrongReferenceMessenger(), NullLoggerFactory.Instance);
    }

    public FakeFilePicker FilePicker { get; } = new();
    public FakeDialogs Dialogs { get; } = new();
    public FakeClipboard Clipboard { get; } = new();
    public InlineDispatcher Dispatcher { get; } = new();
    public FakeWindowService Windows { get; } = new();
    public FakeProcessLauncher Processes { get; } = new();
    public IReflectionHost Reflection { get; }

    /// <summary>Virtual time for throttled work (the search box) and the generated-code loop.</summary>
    public TestScheduler Scheduler { get; } = new();
    public EditorContext Context { get; }
}
