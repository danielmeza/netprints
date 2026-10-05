using NetPrints.Editor.CodeView;
using NetPrints.Editor.ErrorList;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Output;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The six built-in panels (contracts/shell.md section 3).</summary>
public static class PanelContributions
{
    /// <summary>Id of the project tree panel.</summary>
    public const string ProjectTreeId = ContributionIds.PanelPrefix + "projectTree";

    /// <summary>Id of the inspector panel.</summary>
    public const string InspectorId = ContributionIds.PanelPrefix + "inspector";

    /// <summary>Id of the Variables panel.</summary>
    public const string VariablesId = ContributionIds.PanelPrefix + "variables";

    /// <summary>Id of the Errors panel.</summary>
    public const string ErrorsId = ContributionIds.PanelPrefix + "errors";

    /// <summary>Id of the Output panel.</summary>
    public const string OutputId = ContributionIds.PanelPrefix + "output";

    /// <summary>Id of the generated C# panel.</summary>
    public const string CSharpId = ContributionIds.PanelPrefix + "csharp";

    private const int Second = 1;
    private const int Third = 2;

    /// <summary>Registers the built-in panels; each creates its own view model.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.AddPanel(new PanelDescriptor(ProjectTreeId, "Project", _ => new ProjectTreePanelViewModel(), PanelDock.Left, 0, "FileTree"));
        registry.AddPanel(new PanelDescriptor(InspectorId, "Inspector", _ => new InspectorPanelViewModel(), PanelDock.Right, 0, "Tune"));
        registry.AddPanel(new PanelDescriptor(VariablesId, "Variables", _ => new ShellVariablesPanelViewModel(), PanelDock.Right, Second, "Variable"));
        registry.AddPanel(new PanelDescriptor(ErrorsId, "Errors", _ => new ErrorsPanelViewModel(), PanelDock.Bottom, 0, "AlertCircleOutline"));
        registry.AddPanel(new PanelDescriptor(OutputId, "Output", _ => new OutputPanelViewModel(), PanelDock.Bottom, Second, "Console"));
        registry.AddPanel(new PanelDescriptor(CSharpId, "C#", _ => new CSharpPanelViewModel(), PanelDock.Bottom, Third, "LanguageCsharp"));
    }
}
