using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>Opens a project through File > Open and the file picker.</summary>
public sealed class OpenTheProject(string path, string expectedTitle) : ITask
{
    public string Description => $"open the project {Path.GetFileName(path)}";

    public static OpenTheProject At(string path) => new(path, Path.GetFileNameWithoutExtension(path));

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var editor = actor.Using<UseNetPrints>();
        await editor.FileDialogs.OpenFileAsync("Open Project", path, () => editor.Shell.Menu.InvokeAsync("File", ShellCommands.OpenProject, cancellationToken),
            cancellationToken);
        await editor.Shell.WaitForProjectAsync(expectedTitle, cancellationToken);
    }
}

/// <summary>Opens a method's graph from the Project tree.</summary>
public sealed class OpenTheMethod(string method) : ITask
{
    public string Description => $"open {method}";

    public static OpenTheMethod Named(string method) => new(method);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var shell = actor.Using<UseNetPrints>().Shell;
        await shell.OpenMethodAsync(method, cancellationToken);
    }
}

/// <summary>Adds a node by right-clicking empty canvas and choosing it in the search.</summary>
public sealed class AddANode(string searchText, string row, double x, double y, DocumentId? document = null) : ITask
{
    public string Description => $"add a '{row}' node";

    /// <summary>A node at (280, 270) on an unpanned canvas: below the sample's nodes (y = 112) and inside the shell's smaller canvas.</summary>
    public static AddANode Named(string row) => new(row, row, 280, 270);

    public AddANode At(double canvasX, double canvasY) => new(searchText, row, canvasX, canvasY, document);

    /// <summary>The same node added on the canvas of <paramref name="graph"/>, docked or floated, instead of the selected document.</summary>
    public AddANode In(DocumentId graph) => new(searchText, row, x, y, graph);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var shell = actor.Using<UseNetPrints>().Shell;
        var graph = document is { } id ? shell.GraphOf(id) : shell.Graph;
        int before = await graph.NodeCountAsync(cancellationToken);
        var search = await (await graph.RightClickAtAsync(x, y, cancellationToken)).WaitOpenAsync(cancellationToken);
        await search.FilterAsync(searchText, row, cancellationToken);
        await search.ChooseAsync(row, cancellationToken);
        await search.WaitClosedAsync(cancellationToken);
        await UiWait.UntilAsync(graph.Driver, async () => await graph.NodeCountAsync(cancellationToken) == before + 1, $"the '{row}' node on the canvas",
            cancellationToken);
        await graph.WaitRenderedAsync(cancellationToken);
    }
}

/// <summary>Drags a cable from an output pin to an input pin.</summary>
public sealed class ConnectThePins(string fromNode, string fromPin, string toNode, string toPin) : ITask
{
    public string Description => $"connect {fromNode}.{fromPin} to {toNode}.{toPin}";

    public static ConnectThePins From(string fromNode, string fromPin, string toNode, string toPin) =>
        new(fromNode, fromPin, toNode, toPin);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var graph = actor.Using<UseNetPrints>().Shell.Graph;
        await graph.Node(fromNode).Output(fromPin).ConnectToAsync(graph.Node(toNode).Input(toPin), cancellationToken);
        await graph.Connection($"{fromNode}.{fromPin}->{toNode}.{toPin}").GetAsync(cancellationToken);
    }
}

/// <summary>Ticks the inline check box of an unconnected boolean input (PAR-44).</summary>
public sealed class TickThePin(string node, string pin) : ITask
{
    public string Description => $"tick {node}.{pin}";

    public static TickThePin Of(string node, string pin) => new(node, pin);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var check = actor.Using<UseNetPrints>().Shell.Graph.Node(node).Input(pin).ValueCheck;
        await check.ClickAsync(cancellationToken);
        await check.WaitUntilAsync(e => e[AutomationPropertyNames.IsChecked] == "True", "ticked", cancellationToken);
    }
}

/// <summary>Clicks Compile on the command bar and waits for the build result in the Output panel.</summary>
public sealed class CompileTheProject : ITask
{
    public string Description => "compile the project";

    public static CompileTheProject Now() => new();

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var shell = actor.Using<UseNetPrints>().Shell;
        await shell.Bottom.ShowAsync(PanelContributions.OutputId, cancellationToken);
        var before = await shell.Bottom.OutputLinesAsync(cancellationToken);
        await shell.Commands.InvokeAsync(ShellCommands.Compile, cancellationToken);
        await shell.Bottom.WaitForBuildResultAsync(cancellationToken, before);
    }
}

/// <summary>Clicks Run on the command bar (compiles and starts the program).</summary>
public sealed class RunTheProgram : ITask
{
    public string Description => "run the program";

    public static RunTheProgram Now() => new();

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().Shell.Commands.InvokeAsync(ShellCommands.Run, cancellationToken);
}
