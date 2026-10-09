using NetPrints.Editor.Contributions;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>The ids of the commands the page objects invoke (contracts/commands.md).</summary>
public static class ShellCommands
{
    public const string NewProject = ContributionIds.CommandPrefix + "newProject";
    public const string OpenProject = ContributionIds.CommandPrefix + "openProject";
    public const string CloseProject = ContributionIds.CommandPrefix + "closeProject";
    public const string Save = ContributionIds.CommandPrefix + "save";
    public const string SaveAll = ContributionIds.CommandPrefix + "saveAll";
    public const string References = ContributionIds.CommandPrefix + "references";
    public const string Compile = ContributionIds.CommandPrefix + "compile";
    public const string Run = ContributionIds.CommandPrefix + "run";
    public const string AddConstructor = ContributionIds.CommandPrefix + "addConstructor";
    public const string OverrideMethod = ContributionIds.CommandPrefix + "overrideMethod";
    public const string AddVariable = ContributionIds.CommandPrefix + "addVariable";
    public const string Undo = ContributionIds.CommandPrefix + "undo";
    public const string Redo = ContributionIds.CommandPrefix + "redo";
    public const string FloatDocument = ContributionIds.CommandPrefix + "floatDocument";
    public const string DockDocument = ContributionIds.CommandPrefix + "dockDocument";
    public const string ResetLayout = ContributionIds.CommandPrefix + "resetLayout";
}
