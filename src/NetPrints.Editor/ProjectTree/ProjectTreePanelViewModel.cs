using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Events;
using NetPrints.Editor.ModelSync;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ProjectTree;

/// <summary>The project tree panel: project, classes, and per class its Methods, Constructors, Variables and Event graphs.</summary>
public sealed partial class ProjectTreePanelViewModel : ObservableObject, IShellPanelContent
{
    private const string MethodsName = "Methods";
    private const string ConstructorsName = "Constructors";
    private const string VariablesName = "Variables";
    private const string EventGraphsName = "Event graphs";
    private const string BlankNameMessage = "A name cannot be blank";
    private const string NoProjectMessage = "No project is open";

    private PanelContext? context;

    /// <summary>Gets the top rows: the open project, or none while no project is open.</summary>
    public ObservableCollection<ProjectTreeItemViewModel> Roots { get; } = [];

    /// <summary>Gets or sets the selected row, or null.</summary>
    [ObservableProperty]
    public partial ProjectTreeItemViewModel? SelectedItem { get; set; }

    /// <summary>Gets the invoker whose <c>ProjectTree</c> commands the tree's keys run, or null before the panel is attached.</summary>
    public CommandInvoker? Invoker => context?.Commands;

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        OnPropertyChanged(nameof(Invoker));
        context.Shell.PropertyChanged += OnShellChanged;
        context.Shell.ModelRenamed += OnModelRenamed;
        context.Shell.InlineRenameRequested += OnInlineRenameRequested;
        context.Commands.CommandStatesChanged += OnCommandStatesChanged;
        Rebuild();
    }

    /// <summary>Selects the row of a model object.</summary>
    /// <param name="model">A class, graph or variable of the open project.</param>
    /// <returns>Whether a row was found and selected.</returns>
    public bool Select(object model)
    {
        ArgumentNullException.ThrowIfNull(model);
        List<ProjectTreeItemViewModel>? path = FindPath(Roots, model);
        if (path is null)
        {
            return false;
        }

        foreach (ProjectTreeItemViewModel ancestor in path.SkipLast(1))
        {
            ancestor.IsExpanded = true;
        }

        SelectedItem = path[^1];
        return true;
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (context is { } attached)
        {
            attached.Shell.PropertyChanged -= OnShellChanged;
            attached.Shell.ModelRenamed -= OnModelRenamed;
            attached.Shell.InlineRenameRequested -= OnInlineRenameRequested;
            attached.Commands.CommandStatesChanged -= OnCommandStatesChanged;
        }

        SelectedItem = null;
        Release();
        context = null;
    }

    partial void OnSelectedItemChanged(ProjectTreeItemViewModel? oldValue, ProjectTreeItemViewModel? newValue)
    {
        oldValue?.MenuEntries.Clear();
        oldValue?.CancelEditCommand.Execute(null);
        if (context is { } attached)
        {
            attached.Shell.TreeSelection = newValue is { Kind: not (TreeItemKind.Project or TreeItemKind.Group) } ? newValue.Model : null;
        }

        RefreshMenu();
    }

    private static List<ProjectTreeItemViewModel>? FindPath(IEnumerable<ProjectTreeItemViewModel> items, object model)
    {
        foreach (ProjectTreeItemViewModel item in items)
        {
            if (ReferenceEquals(item.Model, model))
            {
                return [item];
            }

            if (FindPath(item.Children, model) is { } below)
            {
                below.Insert(0, item);
                return below;
            }
        }

        return null;
    }

    private static ProjectTreeItemViewModel Group(string name, ObservableCollection<ProjectTreeItemViewModel> children) =>
        new(TreeItemKind.Group, null, () => name, null, children, null);

    private static ContextMenuTarget? MenuTargetOf(TreeItemKind kind) => kind switch
    {
        TreeItemKind.Class => ContextMenuTarget.TreeClass,
        TreeItemKind.Method or TreeItemKind.Constructor or TreeItemKind.Variable => ContextMenuTarget.TreeMember,
        TreeItemKind.EventGraph => ContextMenuTarget.TreeEventGraph,
        _ => null,
    };

    private static IEnumerable<ProjectTreeItemViewModel> Descendants(ProjectTreeItemViewModel item) =>
        new[] { item }.Concat(item.Children.SelectMany(Descendants));

    private void Rebuild()
    {
        SelectedItem = null;
        Release();
        if (context?.Shell.Session is { } session)
        {
            Roots.Add(new ProjectTreeItemViewModel(
                TreeItemKind.Project,
                session.Project,
                () => session.Project.Name,
                session.Project,
                new ObservableViewModelCollection<ProjectTreeItemViewModel, ClassGraph>(session.Project.Classes, CreateClass, ReleaseItem),
                null));
            RefreshUnsaved();
            SelectActiveDocument();
        }
    }

    private void Release()
    {
        foreach (ProjectTreeItemViewModel root in Roots)
        {
            root.Detach();
        }

        Roots.Clear();
    }

    private void ReleaseItem(ProjectTreeItemViewModel item)
    {
        if (SelectedItem is { } selected && Descendants(item).Contains(selected))
        {
            SelectedItem = null;
        }

        item.Detach();
    }

    private ProjectTreeItemViewModel CreateClass(ClassGraph cls)
    {
        var item = new ProjectTreeItemViewModel(
            TreeItemKind.Class,
            cls,
            () => cls.Name,
            cls as INotifyPropertyChanged,
            [
                Group(MethodsName, new ObservableViewModelCollection<ProjectTreeItemViewModel, MethodGraph>(cls.Methods, CreateMethod, ReleaseItem)),
                Group(ConstructorsName, new ObservableViewModelCollection<ProjectTreeItemViewModel, ConstructorGraph>(cls.Constructors, CreateConstructor, ReleaseItem)),
                Group(VariablesName, new ObservableViewModelCollection<ProjectTreeItemViewModel, Variable>(cls.Variables, CreateVariable, ReleaseItem)),
                Group(EventGraphsName, new ObservableViewModelCollection<ProjectTreeItemViewModel, EventGraph>(cls.EventGraphs, CreateEventGraph, ReleaseItem)),
            ],
            Open);
        item.IsUnsaved = context?.Shell.Session?.Unsaved.IsUnsaved(cls) ?? false;
        return item;
    }

    private ProjectTreeItemViewModel CreateMethod(MethodGraph method) =>
        new(TreeItemKind.Method, method, () => method.Name, method as INotifyPropertyChanged, null, Open,
            name => Rename(method.Class, name, true, (classContext, text) => classContext.RenameMethod(method, text)));

    private ProjectTreeItemViewModel CreateConstructor(ConstructorGraph constructor) =>
        new(TreeItemKind.Constructor, constructor, () => constructor.ToString() ?? string.Empty, null, null, Open);

    private ProjectTreeItemViewModel CreateVariable(Variable variable) =>
        new(TreeItemKind.Variable, variable, () => variable.Name, variable as INotifyPropertyChanged, null, null,
            name => Rename(variable.Class, name, true, (classContext, text) => classContext.RenameVariable(variable, text)));

    private ProjectTreeItemViewModel CreateEventGraph(EventGraph graph) =>
        new(TreeItemKind.EventGraph, graph, () => graph.Name, null, null, Open,
            name => Rename(graph.Class, name, false, (classContext, text) => classContext.RenameEventGraph(graph, text)));

    private string? Rename(ClassGraph? owner, string name, bool refuseBlank, Action<ClassContext, string> rename)
    {
        if (context is not { Shell: { Session: { } session } shell } || owner is null)
        {
            return NoProjectMessage;
        }

        if (refuseBlank && string.IsNullOrWhiteSpace(name))
        {
            return BlankNameMessage;
        }

        try
        {
            rename(session.ContextFor(owner), name);
        }
        catch (ArgumentException refused)
        {
            return RefusalMessage.Of(refused);
        }

        shell.NotifyModelRenamed();
        return null;
    }

    private void OnInlineRenameRequested(object? sender, InlineRenameRequestedEventArgs e)
    {
        if (Select(e.Item) && SelectedItem is { CanRename: true } row)
        {
            row.BeginEdit();
            e.Handled = row.IsEditing;
        }
    }

    /// <summary>Opens the graph of a double-tapped row; a row that opens nothing (a group, a variable, the project) is ignored.</summary>
    /// <param name="item">Row that was double-tapped.</param>
    [RelayCommand]
    private void OpenItem(ProjectTreeItemViewModel? item)
    {
        if (item is { OpenCommand: var open } && open.CanExecute(null))
        {
            open.Execute(null);
        }
    }

    private void Open(ProjectTreeItemViewModel item)
    {
        if (context?.Shell.Session is { } session && CommandTargets.GraphDocumentOf(session, item.Model) is { } id)
        {
            context.Api.OpenDocument(id);
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.Session):
                Rebuild();
                break;
            case nameof(ShellViewModel.ActiveDocument):
                SelectActiveDocument();
                break;
            case nameof(ShellViewModel.TreeSelection):
                FollowTreeSelection();
                break;
        }
    }

    private void FollowTreeSelection()
    {
        if (context?.Shell.TreeSelection is { } model && !ReferenceEquals(SelectedItem?.Model, model))
        {
            Select(model);
        }
    }

    private void SelectActiveDocument()
    {
        if (context is { Shell: { Session: { } session, ActiveDocument: { } document } }
            && CommandTargets.GraphOf(session, document.Id) is { } graph)
        {
            Select(graph);
        }
    }

    private void OnModelRenamed(object? sender, EventArgs e)
    {
        foreach (ProjectTreeItemViewModel item in Roots.SelectMany(Descendants))
        {
            item.Refresh();
        }
    }

    private void OnCommandStatesChanged(object? sender, EventArgs e)
    {
        RefreshUnsaved();
        RefreshMenu();
    }

    private void RefreshUnsaved()
    {
        if (context?.Shell.Session is not { } session)
        {
            return;
        }

        foreach (ProjectTreeItemViewModel item in Roots.SelectMany(root => root.Children).Where(item => item is { Kind: TreeItemKind.Class, Model: ClassGraph }))
        {
            item.IsUnsaved = item.Model is ClassGraph cls && session.Unsaved.IsUnsaved(cls);
        }
    }

    private void RefreshMenu()
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        List<CommandEntryViewModel> wanted = [];
        if (context is { } attached && MenuTargetOf(item.Kind) is { } target)
        {
            foreach (CommandDescriptor command in attached.Commands.ContextMenuCommands(target))
            {
                if (attached.Commands.CanRun(command, CommandScope.ProjectTree))
                {
                    CommandEntryViewModel? kept = item.MenuEntries.FirstOrDefault(entry => entry.Id == command.Id);
                    kept?.Refresh();
                    wanted.Add(kept ?? new CommandEntryViewModel(command, attached.Commands, AutomationIds.TreeMenuPrefix, CommandScope.ProjectTree));
                }
            }
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            if (i < item.MenuEntries.Count && ReferenceEquals(item.MenuEntries[i], wanted[i]))
            {
                continue;
            }

            int existing = item.MenuEntries.IndexOf(wanted[i]);
            if (existing >= 0)
            {
                item.MenuEntries.Move(existing, i);
            }
            else
            {
                item.MenuEntries.Insert(i, wanted[i]);
            }
        }

        while (item.MenuEntries.Count > wanted.Count)
        {
            item.MenuEntries.RemoveAt(item.MenuEntries.Count - 1);
        }
    }
}
