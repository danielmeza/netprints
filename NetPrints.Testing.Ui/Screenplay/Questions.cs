using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>The build status line of a class window ("Build succeeded", "Build failed with …").</summary>
public sealed class TheBuildStatus(string classFullName) : IQuestion<string>
{
    public string Description => "the build status";

    public static TheBuildStatus In(string classFullName) => new(classFullName);

    public async Task<string> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().ClassEditor(classFullName).StatusText.TextAsync(cancellationToken) ?? "";
}

/// <summary>The names of the nodes on the open graph of a class window.</summary>
public sealed class TheNodes(string classFullName) : IQuestion<IReadOnlyList<string>>
{
    public string Description => "the nodes on the canvas";

    public static TheNodes In(string classFullName) => new(classFullName);

    public async Task<IReadOnlyList<string>> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().ClassEditor(classFullName).Graph.NodeNamesAsync(cancellationToken);
}

/// <summary>The number of nodes on the open graph of a class window.</summary>
public sealed class TheNodeCount(string classFullName) : IQuestion<int>
{
    public string Description => "the number of nodes on the canvas";

    public static TheNodeCount In(string classFullName) => new(classFullName);

    public async Task<int> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().ClassEditor(classFullName).Graph.NodeCountAsync(cancellationToken);
}

/// <summary>What the started program wrote, once it contains <paramref name="expected"/> (or the wait times out).</summary>
public sealed class TheProgramOutput(string expected) : IQuestion<string>
{
    public string Description => "the program's output";

    public static TheProgramOutput Containing(string expected) => new(expected);

    public async Task<string> AnsweredByAsync(Actor actor, CancellationToken cancellationToken)
    {
        var driver = actor.Using<UseNetPrints>().Driver;
        return await UiWait.ForAsync(driver, () => driver.ProgramOutputAsync(cancellationToken), o => o.Contains(expected, StringComparison.Ordinal),
            $"program output containing '{expected}'", cancellationToken, TimeSpan.FromSeconds(60));
    }
}
