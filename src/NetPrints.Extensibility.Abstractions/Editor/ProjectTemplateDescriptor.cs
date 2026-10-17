using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A template offered by the New project flow.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="DisplayName">The non-empty name shown to the user.</param>
/// <param name="Description">A short description.</param>
/// <param name="ProfileId">The project profile the template creates.</param>
/// <param name="OutputType">The kind of assembly it builds.</param>
/// <param name="IconId">Icon id, or null.</param>
/// <param name="Files">The files written next to the project file, with the <see cref="TemplateFile"/> replacements; <see langword="null"/> or empty writes only the project file.</param>
[Experimental(ExperimentalApiIds.ProjectTemplates, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record ProjectTemplateDescriptor(
    string Id,
    string DisplayName,
    string Description,
    string ProfileId,
    ProjectOutputType OutputType,
    string? IconId = null,
    IReadOnlyList<TemplateFile>? Files = null);
