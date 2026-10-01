using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions;

/// <summary>One go-to-anything result.</summary>
/// <param name="Kind">The group the result belongs to.</param>
/// <param name="Title">The primary text.</param>
/// <param name="Detail">The secondary text, such as the owning class.</param>
/// <param name="Target">Where choosing the result navigates.</param>
public sealed record GoToItem(string Kind, string Title, string Detail, NavigationTarget Target);
