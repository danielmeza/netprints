using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>Dialogs that record calls instead of opening modal windows.</summary>
public sealed class RecordingDialogs : IEditorDialogs
{
    public List<(string Title, string Message)> Errors { get; } = [];
    public List<TypeSpecifier> SelectTypeCalls { get; } = [];
    public TypeSpecifier? TypeAnswer { get; set; } = TypeSpecifier.FromType<int>();

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
        references.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>Records started processes instead of starting them.</summary>
public sealed class RecordingProcessLauncher : IProcessLauncher
{
    public List<(string FileName, string? Arguments)> Started { get; } = [];

    public void Start(string fileName, string? arguments) => Started.Add((fileName, arguments));
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
