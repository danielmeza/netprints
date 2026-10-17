using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A template offered by the New project flow.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="DisplayName">The non-empty name shown to the user. A plain string is a literal that takes the convention key <c>Template.&lt;id&gt;.Name</c> and shows itself when no resource has the key.</param>
/// <param name="Description">A short description. A plain string is a literal that takes the convention key <c>Template.&lt;id&gt;.Description</c>.</param>
/// <param name="ProfileId">The project profile the template creates.</param>
/// <param name="OutputType">The kind of assembly it builds.</param>
/// <param name="IconId">Icon id, or null.</param>
/// <param name="Files">The files written next to the project file, with the <see cref="TemplateFile"/> replacements; <see langword="null"/> or empty writes only the project file.</param>
[Experimental(ExperimentalApiIds.ProjectTemplates, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record ProjectTemplateDescriptor(
    string Id,
    LocalizedText DisplayName,
    LocalizedText Description,
    string ProfileId,
    ProjectOutputType OutputType,
    string? IconId = null,
    IReadOnlyList<TemplateFile>? Files = null)
{
    /// <summary>Gets the name with its convention key (FR-027): a literal name is keyed <c>Template.&lt;id&gt;.Name</c>.</summary>
    public LocalizedText DisplayName { get; init; } = ConventionKeys.TemplateName(Id, DisplayName);

    /// <summary>Gets the description with its convention key (FR-027): a literal description is keyed <c>Template.&lt;id&gt;.Description</c>.</summary>
    public LocalizedText Description { get; init; } = ConventionKeys.TemplateDescription(Id, Description);
}
