namespace NetPrints.Editor.Contributions;

/// <summary>A resource dictionary an extension added with <c>AddViewResources</c>.</summary>
/// <param name="Owner">The id of the extension.</param>
/// <param name="Uri">The <c>avares://</c> URI of the dictionary.</param>
public sealed record ViewResourceDeclaration(string Owner, string Uri)
{
    /// <summary>Gets the display name of the extension; the id when it has none.</summary>
    public string OwnerName { get; init; } = "";
}
