namespace NetPrints.Editor.Contributions;

/// <summary>A template offered by the New project flow.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="DisplayName">The non-empty name shown to the user.</param>
/// <param name="Description">A short description.</param>
/// <param name="ProfileId">The project profile the template creates.</param>
/// <param name="OutputType">The kind of assembly it builds.</param>
/// <param name="IconKind">Material icon kind name, or null.</param>
public sealed record ProjectTemplateDescriptor(
    string Id,
    string DisplayName,
    string Description,
    string ProfileId,
    ProjectOutputType OutputType,
    string? IconKind = null);
