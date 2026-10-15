using NetPrints.Core;

namespace NetPrints.Editor.Inspectors;

/// <summary>The visibility values the inspectors' visibility choosers offer.</summary>
internal static class VisibilityChoices
{
    /// <summary>Gets the visibilities, least to most visible.</summary>
    public static IReadOnlyList<MemberVisibility> All { get; } =
    [
        MemberVisibility.Internal,
        MemberVisibility.Private,
        MemberVisibility.Protected,
        MemberVisibility.Public,
    ];
}
