using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ProjectTree;

/// <summary>One row of the project tree: the project, a class, a group of a class, or a member.</summary>
public sealed partial class ProjectTreeItemViewModel : ObservableObject
{
    private const string UnsavedMark = "*";
    private const string UnsavedStatus = "Unsaved";

    private readonly Func<string> readName;
    private readonly INotifyPropertyChanged? nameSource;
    private readonly Action<ProjectTreeItemViewModel>? open;
    private readonly Func<string, string?>? rename;

    internal ProjectTreeItemViewModel(
        TreeItemKind kind,
        object? model,
        Func<string> readName,
        INotifyPropertyChanged? nameSource,
        ObservableCollection<ProjectTreeItemViewModel>? children,
        Action<ProjectTreeItemViewModel>? open,
        Func<string, string?>? rename = null)
    {
        Kind = kind;
        Model = model;
        this.readName = readName;
        this.nameSource = nameSource;
        this.open = open;
        this.rename = rename;
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
    [NotifyPropertyChangedFor(nameof(RenameBoxAutomationId))]
    [NotifyPropertyChangedFor(nameof(RenameErrorAutomationId))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Name { get; private set; } = "";

    /// <summary>Gets a value indicating whether the file the row stands for has unsaved changes (a class row only).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(ItemStatus))]
    public partial bool IsUnsaved { get; internal set; }

    /// <summary>Gets the status screen readers announce after the name: <c>Unsaved</c> while <see cref="IsUnsaved"/>, otherwise empty.</summary>
    public string ItemStatus => IsUnsaved ? UnsavedStatus : "";

    /// <summary>Gets the text the row shows: <see cref="Name"/>, with a trailing <c>*</c> while <see cref="IsUnsaved"/>.</summary>
    public string DisplayName => IsUnsaved ? Name + UnsavedMark : Name;

    /// <summary>Gets or sets a value indicating whether the row is expanded.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets the automation id, <c>Tree.&lt;kind&gt;.&lt;name&gt;</c>.</summary>
    public string AutomationId => AutomationIds.TreePrefix + KindText + "." + Name;

    /// <summary>Gets a value indicating whether the row can be renamed in place: a method, variable or event graph.</summary>
    public bool CanRename => rename is not null;

    /// <summary>Gets a value indicating whether the row shows the name in a text box (<see cref="EditText"/>) instead of as a label.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    public partial bool IsEditing { get; private set; }

    /// <summary>Gets a value indicating whether the row shows its name as a label.</summary>
    public bool IsNotEditing => !IsEditing;

    /// <summary>Gets or sets the text of the name box while <see cref="IsEditing"/>.</summary>
    [ObservableProperty]
    public partial string EditText { get; set; } = "";

    /// <summary>Gets the reason the edited name was refused, or null while it was not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditError))]
    public partial string? EditError { get; private set; }

    /// <summary>Gets a value indicating whether the edited name was refused.</summary>
    public bool HasEditError => EditError is not null;

    /// <summary>Gets the automation id of the name box in edit mode, <c>Tree.rename.&lt;kind&gt;.&lt;name&gt;</c>.</summary>
    public string RenameBoxAutomationId => AutomationIds.TreeRenamePrefix + KindText + "." + Name;

    /// <summary>Gets the automation id of the refusal message in edit mode, <c>Tree.rename.error.&lt;kind&gt;.&lt;name&gt;</c>.</summary>
    public string RenameErrorAutomationId => AutomationIds.TreeRenamePrefix + "error." + KindText + "." + Name;

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

    /// <summary>Puts the row into edit mode with the current name; nothing happens for a row that cannot be renamed.</summary>
    public void BeginEdit()
    {
        if (!CanRename)
        {
            return;
        }

        EditText = Name;
        EditError = null;
        IsEditing = true;
    }

    /// <summary>
    /// Renames the model to <see cref="EditText"/> and leaves edit mode. A refused name keeps the row in edit mode and sets
    /// <see cref="EditError"/>; an unchanged name just leaves edit mode.
    /// </summary>
    [RelayCommand]
    private void CommitEdit()
    {
        if (!IsEditing || rename is null)
        {
            return;
        }

        if (EditText.Trim() == Name)
        {
            IsEditing = false;
            return;
        }

        EditError = rename(EditText);
        if (EditError is null)
        {
            IsEditing = false;
            Refresh();
        }
    }

    /// <summary>Leaves edit mode without renaming; nothing happens when the row is not being edited.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        EditError = null;
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
