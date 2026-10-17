namespace NetPrints.Editor.Contributions;

/// <summary>Fills one session group while it is being added; the group is published as a whole when the fill ends.</summary>
public interface IContributionGroupBuilder
{
    /// <summary>Gets the editor facet of one extension: what it adds is recorded as owned by <paramref name="owner"/>.</summary>
    /// <param name="owner">The extension id.</param>
    /// <param name="ownerName">The extension's <c>name</c>, shown beside its items; <see langword="null"/> to show the id.</param>
    /// <returns>The facet.</returns>
    IEditorContributions For(string owner, string? ownerName = null);

    /// <summary>Adds a project template to the group; an invalid or duplicate one is ignored.</summary>
    /// <param name="template">The template.</param>
    /// <param name="owner">The extension id that owns it; <see langword="null"/> for the built-in owner.</param>
    void AddProjectTemplate(ProjectTemplateDescriptor template, string? owner = null);
}
