using NetPrints.Core;
using NetPrints.Editor.Icons;
using NetPrints.Workspace.Templates;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in project templates: Console app and Class library, both on the default project profile.</summary>
public static class ProjectTemplateContributions
{
    /// <summary>Registers the templates.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        registry.AddProjectTemplate(new ProjectTemplateDescriptor(
            ContributionIds.TemplatePrefix + "console",
            "Console app",
            "A program with an entry point: a Program class with an empty Main method.",
            DefaultProjectProfile.ProfileId,
            ProjectOutputType.Console,
            IconId: IconIds.TemplateConsole,
            Files: Files(BuiltInTemplateTrees.Console)));
        registry.AddProjectTemplate(new ProjectTemplateDescriptor(
            ContributionIds.TemplatePrefix + "library",
            "Class library",
            "A library with no entry point and no classes yet.",
            DefaultProjectProfile.ProfileId,
            ProjectOutputType.Library,
            IconId: IconIds.TemplateLibrary,
            Files: Files(BuiltInTemplateTrees.Library)));
    }

    private static TemplateFile[] Files(string tree) =>
        [.. BuiltInTemplateTrees.Read(tree).Select(file => new TemplateFile(file.Path, file.Content))];
}
