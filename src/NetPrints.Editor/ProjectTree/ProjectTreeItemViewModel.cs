using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ProjectTree;

/// <summary>One row of the project tree: the project, a class, a group of a class, or a member.</summary>
public sealed partial class ProjectTreeItemViewModel : ObservableObject
{
    private readonly Func<string> readName;
    private readonly INotifyPropertyChanged? nameSource;
    private readonly Action<ProjectTreeItemViewModel>? open;

    internal ProjectTreeItemViewModel(
        TreeItemKind kind,
        object? model,
        Func<string> readName,
        INotifyPropertyChanged? nameSource,
        ObservableCollection<ProjectTreeItemViewModel>? children,
        Action<ProjectTreeItemViewModel>? open)
    {
        Kind = kind;
        Model = model;
        this.readName = readName;
        this.nameSource = nameSource;
        this.open = open;
        Children = children ?? [];
        Name = readName();
        IsExpanded = kind is TreeItemKind.Project or TreeItemKind.Class;
        if (nameSource is not null)
        {
            nameSource.PropertyChanged += OnNameSourceChanged;
        }
    }

    /// <summary>Gets what the row stands for.</summary>
    public TreeItemKind Kind { get; }

    /// <summary>Gets the model object the row shows (a project, class, graph or variable), or null for a group.</summary>
    public object? Model { get; }

    /// <summary>Gets the rows below this one.</summary>
    public ObservableCollection<ProjectTreeItemViewModel> Children { get; }

    /// <summary>Gets the name the row shows; it follows renames of the model.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutomationId))]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets or sets a value indicating whether the row is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets the automation id, <c>Tree.&lt;kind&gt;.&lt;name&gt;</c>.</summary>
    public string AutomationId => AutomationIds.TreePrefix + KindText + "." + Name;

    /// <summary>Gets the context menu entries of the row, those whose command can run for it; filled while the row is selected.</summary>
    public ObservableCollection<CommandEntryViewModel> MenuEntries { get; } = [];

    /// <summary>Gets the Material icon kind name of the row.</summary>
    public string IconKind => Kind switch
    {
        TreeItemKind.Project => "FolderOutline",
        TreeItemKind.Class => "CodeBraces",
        TreeItemKind.Group => "FormatListBulleted",
        TreeItemKind.Method => "FunctionVariant",
        TreeItemKind.Constructor => "Hammer",
        TreeItemKind.Variable => "Variable",
        _ => "LightningBolt",
    };

    /// <summary>Gets a value indicating whether the row opens a graph.</summary>
    public bool CanOpen => Kind is TreeItemKind.Class or TreeItemKind.Method or TreeItemKind.Constructor or TreeItemKind.EventGraph;

    /// <summary>Gets a value indicating whether the row can be dragged onto a graph canvas: a method, constructor or variable.</summary>
    public bool CanDrag => Kind is TreeItemKind.Method or TreeItemKind.Constructor or TreeItemKind.Variable;

    private string KindText => Kind switch
    {
        TreeItemKind.Project => AutomationIds.TreeKindProject,
        TreeItemKind.Class => AutomationIds.TreeKindClass,
        TreeItemKind.Group => AutomationIds.TreeKindGroup,
        TreeItemKind.Method => AutomationIds.TreeKindMethod,
        TreeItemKind.Constructor => AutomationIds.TreeKindConstructor,
        TreeItemKind.Variable => AutomationIds.TreeKindVariable,
        _ => AutomationIds.TreeKindEventGraph,
    };

    /// <summary>Reads the name of the model again, for a rename the model did not announce.</summary>
    public void Refresh() => Name = readName();

    /// <summary>Stops following the model and releases the rows below.</summary>
    public void Detach()
    {
        if (nameSource is not null)
        {
            nameSource.PropertyChanged -= OnNameSourceChanged;
        }

        foreach (ProjectTreeItemViewModel child in Children)
        {
            child.Detach();
        }

        if (Children is IDisposable owned)
        {
            owned.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open() => open?.Invoke(this);

    private void OnNameSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(Name))
        {
            Refresh();
        }
    }
}
