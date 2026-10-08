using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions;

/// <summary>One go-to-anything result: it either navigates to a <see cref="Target"/> or runs the command <see cref="CommandId"/>.</summary>
/// <param name="Kind">The group the result belongs to.</param>
/// <param name="Title">The primary text.</param>
/// <param name="Detail">The secondary text, such as the owning class.</param>
/// <param name="Target">Where choosing the result navigates, or null for a command.</param>
/// <param name="CommandId">The id of the command choosing the result runs, or null for a navigation.</param>
public sealed record GoToItem(string Kind, string Title, string Detail, NavigationTarget? Target, string? CommandId = null);
