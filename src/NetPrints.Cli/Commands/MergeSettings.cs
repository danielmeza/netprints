using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>merge</c>: the three files git hands a merge driver, the conflict marker size and the display path.</summary>
internal sealed class MergeSettings : CommandSettingsBase
{
    /// <summary>The marker size git passes when it does not pass one.</summary>
    public const int DefaultMarkerSize = 7;

    private const int TheirsPosition = 2;

    /// <summary>Gets the common ancestor file (<c>%O</c>).</summary>
    [CommandArgument(0, "<base>")]
    [Description("The common ancestor version (git's %O).")]
    public string Base { get; init; } = string.Empty;

    /// <summary>Gets the current branch's file (<c>%A</c>), which also receives the result.</summary>
    [CommandArgument(1, "<ours>")]
    [Description("The current branch's version (git's %A); the merge result is written over it.")]
    public string Ours { get; init; } = string.Empty;

    /// <summary>Gets the other branch's file (<c>%B</c>).</summary>
    [CommandArgument(TheirsPosition, "<theirs>")]
    [Description("The other branch's version (git's %B).")]
    public string Theirs { get; init; } = string.Empty;

    /// <summary>Gets the length of the conflict markers written by the text fallback.</summary>
    [CommandOption("--marker-size")]
    [DefaultValue(DefaultMarkerSize)]
    [Description("Length of the conflict markers (git's %L).")]
    public int MarkerSize { get; init; } = DefaultMarkerSize;

    /// <summary>Gets the name of the merged file in the work tree, used to pick the document format and in messages.</summary>
    [CommandOption("--path")]
    [Description("The file's path in the work tree (git's %P); picks the document format and names the file in messages.")]
    public string? Path { get; init; }
}
