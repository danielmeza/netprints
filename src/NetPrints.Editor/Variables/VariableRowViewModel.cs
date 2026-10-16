using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Variables;

/// <summary>
/// A row of the Variables panel, member or local: its name and type, the colour family of its type and the inline rename that
/// F2 or a double click starts (Enter commits, Escape or leaving the box cancels, like the project tree).
/// </summary>
public abstract partial class VariableRowViewModel : ObservableObject
{
    private readonly ClassEditorServices rowServices;

    /// <summary>Initializes the row.</summary>
    /// <param name="services">Narrow services shared with the owning class editor; the reflection host classifies the type's colour.</param>
    protected VariableRowViewModel(ClassEditorServices services)
    {
        rowServices = services;
    }

    /// <summary>The variable's name. A refused name leaves the variable as it was and sets <see cref="Error"/>.</summary>
    public abstract string Name { get; set; }

    /// <summary>The variable's type.</summary>
    public abstract TypeSpecifier Type { get; }

    /// <summary>The short name of <see cref="Type"/>, shown muted beside the name.</summary>
    public string TypeName => Type.ShortName;

    /// <summary>The style class of the type's colour family, which colours the row's chip.</summary>
    public string FamilyClass => PinTypeFamily.Of(PinKind.Data, Type, rowServices.Context.Reflection.IsLoaded ? rowServices.Context.Reflection.Provider : null).StyleClass;

    /// <summary>Gets the reason the last name set through <see cref="Name"/> was refused, or null when it was accepted; the inspectors show it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; protected set; }

    /// <summary>Gets a value indicating whether the last name was refused.</summary>
    public bool HasError => Error is not null;

    /// <summary>Gets the reason the name typed into the row was refused, or null; separate from <see cref="Error"/>, which the inspectors show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRenameError))]
    public partial string? RenameError { get; private set; }

    /// <summary>Gets a value indicating whether the name typed into the row was refused.</summary>
    public bool HasRenameError => RenameError is not null;

    /// <summary>Gets a value indicating whether the row shows the name in a text box.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEditing))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(BeginRenameCommand))]
    public partial bool IsEditing { get; private set; }

    /// <summary>Gets a value indicating whether the row shows the name as text.</summary>
    public bool IsNotEditing => !IsEditing;

    /// <summary>Gets or sets the text of the name box while <see cref="IsEditing"/>.</summary>
    [ObservableProperty]
    public partial string EditText { get; set; } = "";

    /// <summary>Raises the changes of <see cref="Type"/> for the bindings of the row.</summary>
    protected void NotifyTypeChanged()
    {
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(FamilyClass));
    }

    /// <summary>Removes the variable (undoable).</summary>
    protected abstract void RemoveVariable();

    /// <summary>Removes the variable (undoable); not while its name is being edited, so a Delete typed into the box keeps the variable.</summary>
    [RelayCommand(CanExecute = nameof(IsNotEditing))]
    private void Remove() => RemoveVariable();

    /// <summary>Puts the row into edit mode with the current name.</summary>
    [RelayCommand(CanExecute = nameof(IsNotEditing))]
    private void BeginRename()
    {
        EditText = Name;
        RenameError = null;
        IsEditing = true;
    }

    /// <summary>
    /// Renames the variable to <see cref="EditText"/> (one undo step) and leaves edit mode. A refused name keeps the row in edit
    /// mode and sets <see cref="RenameError"/>, leaving the inspectors' <see cref="Error"/> as it was; an unchanged name just leaves edit mode.
    /// </summary>
    [RelayCommand]
    private void CommitEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        string text = EditText.Trim();
        RenameError = null;
        if (text != Name)
        {
            string? inspectorError = Error;
            Name = text;
            if (Error is not null)
            {
                RenameError = Error;
                Error = inspectorError;
            }
        }

        if (RenameError is null)
        {
            IsEditing = false;
        }
    }

    /// <summary>Leaves edit mode without renaming.</summary>
    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        RenameError = null;
    }
}
