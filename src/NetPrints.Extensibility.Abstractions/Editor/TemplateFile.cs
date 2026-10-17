using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>
/// A file of a project template. Like <c>dotnet new</c>, the literal <see cref="SourceName"/> is replaced by the project name, and
/// <see cref="RootNamespaceToken"/> and <see cref="ProfileIdToken"/> by the root namespace and the profile id, in the path and in the text.
/// </summary>
/// <param name="Path">The path relative to the project folder, with <c>/</c> separators; it may not leave the folder.</param>
/// <param name="Content">The text of the file.</param>
[Experimental(ExperimentalApiIds.ProjectTemplates, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record TemplateFile(string Path, string Content)
{
    /// <summary>The literal replaced by the project name.</summary>
    public const string SourceName = "NetPrintsProject";

    /// <summary>The literal replaced by the project's root namespace.</summary>
    public const string RootNamespaceToken = "NetPrintsRootNamespace";

    /// <summary>The literal replaced by the id of the template's project profile.</summary>
    public const string ProfileIdToken = "NetPrintsProfileId";
}
