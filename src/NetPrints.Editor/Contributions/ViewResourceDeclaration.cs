namespace NetPrints.Editor.Contributions;

/// <summary>A resource dictionary an extension added with <c>AddViewResources</c>.</summary>
/// <param name="Owner">The id of the extension.</param>
/// <param name="Uri">The <c>avares://</c> URI of the dictionary.</param>
public sealed record ViewResourceDeclaration(string Owner, string Uri)
{
    /// <summary>Gets the session key of the group that declared the resource; empty for the app group. With the URI it identifies the loaded resource, so two copies of an extension never share one.</summary>
    public string Scope { get; init; } = "";

    /// <summary>Gets the assembly the resource is loaded from, or <see langword="null"/> to resolve the URI by assembly name.</summary>
    public System.Reflection.Assembly? Source { get; init; }

    /// <summary>Gets the display name of the extension; the id when it has none.</summary>
    public string OwnerName { get; init; } = "";
}
