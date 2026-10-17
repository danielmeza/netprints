using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>A page of the Settings document.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Title">The non-empty page title.</param>
/// <param name="SectionKey">The id of the settings section the page edits, or <see langword="null"/> for a page that edits none.</param>
/// <param name="Order">The position among the pages of the same parent, lowest first.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record SettingsPageDescriptor(string Id, LocalizedText Title, string? SectionKey, int Order)
{
    /// <summary>Gets the id of the page this one sits under; <see langword="null"/> puts an extension page under the Extensions page.</summary>
    public string? ParentPageId { get; init; }

    /// <summary>Gets the factory of a custom page's view model, whose view is found by data template; <see langword="null"/> means the page is generated from the section's schema.</summary>
    public Func<ISettingsPageContext, object>? CreateViewModel { get; init; }
}
