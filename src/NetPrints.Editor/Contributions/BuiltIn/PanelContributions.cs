using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The five built-in panels (contracts/shell.md section 3).</summary>
public static class PanelContributions
{
    /// <summary>Id of the project tree panel.</summary>
    public const string ProjectTreeId = ContributionIds.PanelPrefix + "projectTree";

    /// <summary>Id of the inspector panel.</summary>
    public const string InspectorId = ContributionIds.PanelPrefix + "inspector";

    /// <summary>Id of the Errors panel.</summary>
    public const string ErrorsId = ContributionIds.PanelPrefix + "errors";

    /// <summary>Id of the Output panel.</summary>
    public const string OutputId = ContributionIds.PanelPrefix + "output";

    /// <summary>Id of the generated C# panel.</summary>
    public const string CSharpId = ContributionIds.PanelPrefix + "csharp";

    private const int Second = 1;
    private const int Third = 2;

    /// <summary>Registers the built-in panels; each shows a placeholder until its own view model replaces the factory.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Add(registry, ProjectTreeId, "Project", PanelDock.Left, 0, "FileTree");
        Add(registry, InspectorId, "Inspector", PanelDock.Right, 0, "Tune");
        Add(registry, ErrorsId, "Errors", PanelDock.Bottom, 0, "AlertCircleOutline");
        Add(registry, OutputId, "Output", PanelDock.Bottom, Second, "Console");
        Add(registry, CSharpId, "C#", PanelDock.Bottom, Third, "LanguageCsharp");
    }

    private static void Add(IContributionRegistry registry, string id, string title, PanelDock dock, int order, string icon) =>
        registry.AddPanel(new PanelDescriptor(id, title, _ => new PanelPlaceholderViewModel(title), dock, order, icon));
}
