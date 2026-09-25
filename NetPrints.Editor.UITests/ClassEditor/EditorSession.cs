using NetPrints.Editor.UITests.Graph;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Main;

namespace NetPrints.Editor.UITests.ClassEditor;

/// <summary>The HelloWorld sample open in the editor with <c>Program.Main</c> on the canvas.</summary>
public sealed class EditorSession : IDisposable
{
    private EditorSession(SampleCopy sample, MainWindowPage main, ClassEditorPage classEditor, GraphCanvasPage graph)
    {
        Sample = sample;
        Main = main;
        ClassEditor = classEditor;
        Graph = graph;
    }

    public SampleCopy Sample { get; }
    public MainWindowPage Main { get; }
    public ClassEditorPage ClassEditor { get; }
    public GraphCanvasPage Graph { get; }

    public static async Task<EditorSession> OpenSampleMainAsync()
    {
        var sample = new SampleCopy();
        var main = MainWindowPage.Start();
        await main.OpenStartupProjectAsync(sample.ProjectPath);
        var classEditor = await main.OpenClassAsync("HelloWorld.Program");
        classEditor.UseFixedSize();
        var graph = await classEditor.OpenMethodAsync("Main");
        return new EditorSession(sample, main, classEditor, graph);
    }

    public void Dispose()
    {
        Main.Dispose();
        Sample.Dispose();
    }
}
