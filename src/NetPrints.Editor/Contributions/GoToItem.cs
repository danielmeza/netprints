using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions;

/// <summary>One go-to-anything result: it either navigates to a <see cref="Target"/> or runs the command <see cref="CommandId"/>, never both and never neither.</summary>
public sealed record GoToItem
{
    /// <summary>Creates a result.</summary>
    /// <param name="kind">The group the result belongs to.</param>
    /// <param name="title">The primary text.</param>
    /// <param name="detail">The secondary text, such as the owning class.</param>
    /// <param name="target">Where choosing the result navigates, or null for a command.</param>
    /// <param name="commandId">The id of the command choosing the result runs, or null for a navigation.</param>
    /// <exception cref="ArgumentException">Both or neither of <paramref name="target"/> and <paramref name="commandId"/> are given.</exception>
    public GoToItem(string kind, string title, string detail, NavigationTarget? target, string? commandId = null)
    {
        if ((target is null) == (commandId is null))
        {
            throw new ArgumentException("A go-to item carries exactly one of a target and a command id.", nameof(commandId));
        }

        Kind = kind;
        Title = title;
        Detail = detail;
        Target = target;
        CommandId = commandId;
    }

    /// <summary>Gets the group the result belongs to.</summary>
    public string Kind { get; }

    /// <summary>Gets the primary text.</summary>
    public string Title { get; }

    /// <summary>Gets the secondary text, such as the owning class.</summary>
    public string Detail { get; }

    /// <summary>Gets where choosing the result navigates, or null for a command.</summary>
    public NavigationTarget? Target { get; }

    /// <summary>Gets the id of the command choosing the result runs, or null for a navigation.</summary>
    public string? CommandId { get; }
}
