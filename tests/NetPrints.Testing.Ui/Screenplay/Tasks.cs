using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>Opens a project through the Project pane and the file picker.</summary>
public sealed class OpenTheProject(string path, string expectedTitle) : ITask
{
    public string Description => $"open the project {Path.GetFileName(path)}";

    public static OpenTheProject At(string path) => new(path, Path.GetFileNameWithoutExtension(path));

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var editor = actor.Using<UseNetPrints>();
        var main = await editor.MainWindow.ShowProjectPaneAsync(cancellationToken);
        await editor.FileDialogs.OpenFileAsync("Open Project", path, () => main.OpenProjectButton.ClickAsync(cancellationToken), cancellationToken);
        await main.WaitForProjectAsync(expectedTitle, cancellationToken);
        if (await main.ProjectPane.IsVisibleAsync(cancellationToken))
        {
            await main.ProjectButton.ClickAsync(cancellationToken); // close the pane again
            await main.ProjectPane.WaitHiddenAsync(cancellationToken);
        }
    }
}

/// <summary>Opens a class window from the class list and a method's graph in it.</summary>
public sealed class OpenTheMethod(string classFullName, string method) : ITask
{
    public string Description => $"open {classFullName}.{method}";

    public static OpenTheMethodBuilder Named(string method) => new(method);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var editor = actor.Using<UseNetPrints>();
        var page = await editor.MainWindow.OpenClassAsync(classFullName, cancellationToken);
        await page.OpenMethodAsync(method, cancellationToken);
    }

    public sealed class OpenTheMethodBuilder(string method)
    {
        public OpenTheMethod Of(string classFullName) => new(classFullName, method);
    }
}

/// <summary>Adds a node by right-clicking empty canvas and choosing it in the search.</summary>
public sealed class AddANode(string classFullName, string searchText, string row, double x, double y) : ITask
{
    public string Description => $"add a '{row}' node to {classFullName}";

    /// <summary>A node at (280, 392) on an unpanned canvas: below the sample's nodes (y = 112).</summary>
    public static AddANode Named(string row, string classFullName) => new(classFullName, row, row, 280, 392);

    public AddANode At(double canvasX, double canvasY) => new(classFullName, searchText, row, canvasX, canvasY);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var graph = actor.Using<UseNetPrints>().ClassEditor(classFullName).Graph;
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
public sealed class ConnectThePins(string classFullName, string fromNode, string fromPin, string toNode, string toPin) : ITask
{
    public string Description => $"connect {fromNode}.{fromPin} to {toNode}.{toPin}";

    public static ConnectThePins From(string classFullName, string fromNode, string fromPin, string toNode, string toPin) =>
        new(classFullName, fromNode, fromPin, toNode, toPin);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var graph = actor.Using<UseNetPrints>().ClassEditor(classFullName).Graph;
        await graph.Node(fromNode).Output(fromPin).ConnectToAsync(graph.Node(toNode).Input(toPin), cancellationToken);
        await graph.Connection($"{fromNode}.{fromPin}->{toNode}.{toPin}").GetAsync(cancellationToken);
    }
}

/// <summary>Ticks the inline check box of an unconnected boolean input (PAR-44).</summary>
public sealed class TickThePin(string classFullName, string node, string pin) : ITask
{
    public string Description => $"tick {node}.{pin}";

    public static TickThePin Of(string classFullName, string node, string pin) => new(classFullName, node, pin);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var check = actor.Using<UseNetPrints>().ClassEditor(classFullName).Graph.Node(node).Input(pin).ValueCheck;
        await check.ClickAsync(cancellationToken);
        await check.WaitUntilAsync(e => e[AutomationPropertyNames.IsChecked] == "True", "ticked", cancellationToken);
    }
}

/// <summary>Clicks Compile in a class window and waits for the build result.</summary>
public sealed class CompileTheProject(string classFullName) : ITask
{
    public string Description => "compile the project";

    public static CompileTheProject From(string classFullName) => new(classFullName);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().ClassEditor(classFullName).CompileAsync(cancellationToken);
}

/// <summary>Clicks Run in a class window (compiles and starts the program).</summary>
public sealed class RunTheProgram(string classFullName) : ITask
{
    public string Description => "run the program";

    public static RunTheProgram From(string classFullName) => new(classFullName);

    public async Task PerformAsAsync(Actor actor, CancellationToken cancellationToken)
    {
        var page = actor.Using<UseNetPrints>().ClassEditor(classFullName);
        await page.RunButton.ClickAsync(cancellationToken);
        await page.WaitForBuildResultAsync(cancellationToken);
    }
}
