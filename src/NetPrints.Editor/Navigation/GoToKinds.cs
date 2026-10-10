namespace NetPrints.Editor.Navigation;

/// <summary>The kinds (result groups) of the built-in go-to providers.</summary>
public static class GoToKinds
{
    /// <summary>The class graph, constructors and event graphs of the open project.</summary>
    public const string Graphs = "Graphs";

    /// <summary>The nodes of every graph of the open project.</summary>
    public const string Nodes = "Nodes";

    /// <summary>The variables of the open project.</summary>
    public const string Variables = "Variables";

    /// <summary>The methods of the open project.</summary>
    public const string Methods = "Methods";

    /// <summary>The registered commands; only searched after a leading <see cref="CommandMarker"/>.</summary>
    public const string Commands = "Commands";

    /// <summary>The character that restricts go to anything to commands when it starts the text.</summary>
    public const char CommandMarker = '>';
}
