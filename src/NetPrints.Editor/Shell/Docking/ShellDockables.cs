using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>A document tab: its <c>Id</c> is the <see cref="DocumentId"/> text and its <c>Context</c> the document view model.</summary>
public sealed class ShellDocument : Document
{
    /// <summary>Creates a document tab with an empty capability override, which Dock's templates bind through and which changes no capability (every flag inherits).</summary>
    public ShellDocument() => DockCapabilityOverrides = new DockCapabilityOverrides();

    /// <summary>Gets the automation id of the tab's content: <c>Shell.Document.&lt;id&gt;</c>.</summary>
    public string AutomationId => AutomationIds.ShellDocumentPrefix + Id;
}

/// <summary>A tool pane: its <c>Id</c> is the panel id and its <c>Context</c> the panel's view model.</summary>
public sealed class ShellTool : Tool
{
    /// <summary>Creates a tool pane with an empty capability override, which Dock's templates bind through and which changes no capability (every flag inherits).</summary>
    public ShellTool() => DockCapabilityOverrides = new DockCapabilityOverrides();

    /// <summary>Gets the automation id of the pane's content: <c>Shell.Panel.&lt;id&gt;</c>.</summary>
    public string AutomationId => AutomationIds.ShellPanelPrefix + Id;

    /// <summary>Gets where the panel docks by default.</summary>
    public PanelDock DefaultDock { get; init; }

    /// <summary>Gets the position among the panels of its default dock.</summary>
    public int PanelOrder { get; init; }
}
