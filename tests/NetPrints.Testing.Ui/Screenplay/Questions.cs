namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>The last build result line of the Output panel ("Build succeeded", "Build failed with …").</summary>
public sealed class TheBuildStatus : IQuestion<string>
{
    public string Description => "the build status";

    public static TheBuildStatus Now() => new();

    public async Task<string> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().Shell.Bottom.BuildResultAsync(cancellationToken) ?? "";
}

/// <summary>The names of the nodes on the open graph.</summary>
public sealed class TheNodes : IQuestion<IReadOnlyList<string>>
{
    public string Description => "the nodes on the canvas";

    public static TheNodes OnTheCanvas() => new();

    public async Task<IReadOnlyList<string>> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().Shell.Graph.NodeNamesAsync(cancellationToken);
}

/// <summary>The number of nodes on the open graph.</summary>
public sealed class TheNodeCount : IQuestion<int>
{
    public string Description => "the number of nodes on the canvas";

    public static TheNodeCount OnTheCanvas() => new();

    public async Task<int> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        await actor.Using<UseNetPrints>().Shell.Graph.NodeCountAsync(cancellationToken);
}

/// <summary>
/// What the started program wrote, once the Output panel contains <paramref name="expected"/> (or
/// the wait times out): the panel's lines joined by new lines.
/// </summary>
public sealed class TheProgramOutput(string expected) : IQuestion<string>
{
    public string Description => "the program's output";

    public static TheProgramOutput Containing(string expected) => new(expected);

    public async Task<string> AnsweredByAsync(Actor actor, CancellationToken cancellationToken) =>
        string.Join('\n', await actor.Using<UseNetPrints>().Shell.Bottom.WaitForOutputContainingAsync(expected, cancellationToken));
}
