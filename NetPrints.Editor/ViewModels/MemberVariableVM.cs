using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Messages;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// A variable (property) of the edited class (PAR-29, PAR-36).
/// </summary>
public sealed partial class MemberVariableVM : ObservableObject, IDisposable
{
    private readonly ClassEditorVM owner;

    public MemberVariableVM(Variable variable, ClassEditorVM owner)
    {
        Variable = variable;
        this.owner = owner;
        ((INotifyPropertyChanged)variable).PropertyChanged += OnVariablePropertyChanged;
    }

    public Variable Variable { get; }

    public TypeSpecifier Type => Variable.Type;

    public VariableSpecifier Specifier => Variable.Specifier;

    public string Name
    {
        get => Variable.Name;
        set => Variable.Name = value;
    }

    public MemberVisibility Visibility
    {
        get => Variable.Visibility;
        set
        {
            if (Variable.Visibility == value)
            {
                return;
            }

            // Accessors that had the variable's visibility follow the new visibility.
            if (Variable.GetterMethod != null && Variable.GetterMethod.Visibility == Variable.Visibility)
            {
                Variable.GetterMethod.Visibility = value;
            }

            if (Variable.SetterMethod != null && Variable.SetterMethod.Visibility == Variable.Visibility)
            {
                Variable.SetterMethod.Visibility = value;
            }

            Variable.Visibility = value;
        }
    }

    public IReadOnlyList<MemberVisibility> PossibleVisibilities => ClassEditorVM.Visibilities;

    public VariableModifiers Modifiers
    {
        get => Variable.Modifiers;
        set => Variable.Modifiers = value;
    }

    public bool IsReadOnly { get => Has(VariableModifiers.ReadOnly); set => Set(VariableModifiers.ReadOnly, value); }

    public bool IsConst { get => Has(VariableModifiers.Const); set => Set(VariableModifiers.Const, value); }

    public bool IsStatic { get => Has(VariableModifiers.Static); set => Set(VariableModifiers.Static, value); }

    public bool IsNew { get => Has(VariableModifiers.New); set => Set(VariableModifiers.New, value); }

    private bool Has(VariableModifiers flag) => Modifiers.HasFlag(flag);

    private void Set(VariableModifiers flag, bool value) => Modifiers = value ? Modifiers | flag : Modifiers & ~flag;

    public MethodGraph? Getter => Variable.GetterMethod;

    public MethodGraph? Setter => Variable.SetterMethod;

    public bool HasGetter => Getter is not null;

    public bool HasSetter => Setter is not null;

    private void OnVariablePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Variable.Name):
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Specifier));
                break;
            case nameof(Variable.Visibility):
                OnPropertyChanged(nameof(Visibility));
                OnPropertyChanged(nameof(Specifier));
                break;
            case nameof(Variable.Modifiers):
                OnPropertyChanged(nameof(Modifiers));
                OnPropertyChanged(nameof(IsReadOnly));
                OnPropertyChanged(nameof(IsConst));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsNew));
                break;
            case nameof(Variable.GetterMethod):
                OnPropertyChanged(nameof(Getter));
                OnPropertyChanged(nameof(HasGetter));
                break;
            case nameof(Variable.SetterMethod):
                OnPropertyChanged(nameof(Setter));
                OnPropertyChanged(nameof(HasSetter));
                break;
        }
    }

    /// <summary>Removes the variable (undoable).</summary>
    [RelayCommand]
    private void Remove() => owner.RemoveVariable(this);

    /// <summary>Shows the variable inspector.</summary>
    [RelayCommand]
    private void Select() => owner.SelectVariable(this);

    [RelayCommand]
    private void AddGetter() => owner.UndoRedo.Do(EditorCommands.AddGetter(Variable));

    [RelayCommand]
    private void RemoveGetter()
    {
        owner.CloseGraphIfOpen(Getter);
        owner.UndoRedo.Do(EditorCommands.RemoveGetter(Variable));
    }

    [RelayCommand]
    private void AddSetter() => owner.UndoRedo.Do(EditorCommands.AddSetter(Variable));

    [RelayCommand]
    private void RemoveSetter()
    {
        owner.CloseGraphIfOpen(Setter);
        owner.UndoRedo.Do(EditorCommands.RemoveSetter(Variable));
    }

    [RelayCommand]
    private void OpenGetter()
    {
        if (Getter is not null)
        {
            owner.Messenger.Send(new OpenGraphMessage(Getter));
        }
    }

    [RelayCommand]
    private void OpenSetter()
    {
        if (Setter is not null)
        {
            owner.Messenger.Send(new OpenGraphMessage(Setter));
        }
    }

    [RelayCommand]
    private void OpenTypeGraph() => owner.Messenger.Send(new OpenGraphMessage(Variable.TypeGraph));

    public void Dispose() => ((INotifyPropertyChanged)Variable).PropertyChanged -= OnVariablePropertyChanged;
}
