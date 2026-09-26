#nullable enable
using System.Collections.Generic;
using NetPrints.Compilation;
using NetPrints.Core;

namespace NetPrints.Projects;

/// <summary>
/// Everything <see cref="IProjectSystem.LoadAsync"/> and <see cref="IProjectSystem.ApplyAsync"/> read
/// from a project file and its MSBuild evaluation (project-system.md §4). Immutable; a new snapshot
/// replaces the previous one after every load or edit.
/// </summary>
/// <param name="ProjectFilePath">Full path of the <c>.csproj</c> file.</param>
/// <param name="Name">Project name (<c>$(MSBuildProjectName)</c>).</param>
/// <param name="RootNamespace">The project's <c>RootNamespace</c>.</param>
/// <param name="AssemblyName">The project's <c>AssemblyName</c>.</param>
/// <param name="OutputType">The project's <c>OutputType</c>.</param>
/// <param name="TargetFramework">The project's (first, if multi-targeted) target framework
/// moniker.</param>
/// <param name="ProfileId">The project's <c>NetPrintsProfile</c>.</param>
/// <param name="ReferencesNetPrintsSdk">Whether the project has a <c>PackageReference</c> to
/// <c>NetPrints.Sdk</c>.</param>
/// <param name="GraphFiles">Full paths of the project's graph documents (<c>NetPrintsGraph</c>
/// items), ordinal-sorted.</param>
/// <param name="ExtensionFolders">Full paths of the project's <c>NetPrintsExtension</c> items, in
/// project order.</param>
/// <param name="References">Resolved reference assemblies, ordinal-sorted by path.</param>
/// <param name="DeclaredReferences">Every reference item as declared in the project file, for the
/// References dialog.</param>
/// <param name="OtherSources">The project's <c>Compile</c> documents other than generated
/// <c>*.netpc.g.cs</c> files (including files generated into <c>obj/</c>, such as global usings).</param>
/// <param name="CompilationOptionsJson">Serialized language version, nullable context and global
/// usings, consumed by <c>CodeAnalysisSession</c>.</param>
/// <param name="Properties">Evaluated MSBuild properties requested via
/// <c>ProjectSystemOptions.ExtraProperties</c>, keyed by property name.</param>
/// <param name="Messages">Messages produced while loading the project.</param>
public sealed record ProjectSnapshot(
    string ProjectFilePath,
    string Name,
    string RootNamespace,
    string AssemblyName,
    BinaryType OutputType,
    string TargetFramework,
    string ProfileId,
    bool ReferencesNetPrintsSdk,
    IReadOnlyList<string> GraphFiles,
    IReadOnlyList<string> ExtensionFolders,
    IReadOnlyList<ResolvedAssembly> References,
    IReadOnlyList<ProjectReferenceInfo> DeclaredReferences,
    IReadOnlyList<SourceFile> OtherSources,
    string CompilationOptionsJson,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<ProjectMessage> Messages)
{
    /// <summary>
    /// Returns the evaluated value of the MSBuild property <paramref name="name"/>, or
    /// <see langword="null"/> if it was not requested via
    /// <c>ProjectSystemOptions.ExtraProperties</c> or is not set.
    /// </summary>
    /// <param name="name">Name of the MSBuild property to look up.</param>
    /// <returns>The property's evaluated value, or <see langword="null"/>.</returns>
    public string? GetProperty(string name) => Properties.TryGetValue(name, out string? value) ? value : null;
}
