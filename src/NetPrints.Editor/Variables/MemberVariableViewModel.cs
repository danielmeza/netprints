using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Events;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Variables;

/// <summary>
/// A variable (property) of the edited class (PAR-29, PAR-36).
/// </summary>
public sealed partial class MemberVariableViewModel : ObservableObject, IDisposable
{
    private readonly ClassEditorServices services;
    private readonly Action<Variable, string> rename;

    /// <summary>
    /// Wraps <paramref name="variable"/> and subscribes to its property-changed event.
    /// </summary>
    /// <param name="variable">Variable to wrap.</param>
    /// <param name="services">Narrow services shared with the owning class editor (FR-038).</param>
    /// <param name="rename">Renames the variable together with its getter and setter nodes.</param>
    public MemberVariableViewModel(Variable variable, ClassEditorServices services, Action<Variable, string> rename)
    {
        Variable = variable;
        this.services = services;
        this.rename = rename;
        ((INotifyPropertyChanged)variable).PropertyChanged += OnVariablePropertyChanged;
    }

    /// <summary>The wrapped model variable.</summary>
    public Variable Variable { get; }

    /// <summary>The variable's type.</summary>
    public TypeSpecifier Type => Variable.Type;

    /// <summary>A snapshot of the variable's specifier.</summary>
    public VariableSpecifier Specifier => Variable.Specifier;

    /// <summary>Gets the reason the last name was refused, or null when it was accepted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Gets a value indicating whether the last name was refused.</summary>
    public bool HasError => Error is not null;

    /// <summary>The variable's name. A refused name leaves the variable as it was and sets <see cref="Error"/>.</summary>
    public string Name
    {
        get => Variable.Name;
        set
        {
            if (Variable.Name != value)
            {
                try
                {
                    rename(Variable, value);
                    Error = null;
                }
                catch (ArgumentException refused)
                {
                    Error = RefusalMessage.Of(refused);
                }
            }

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The variable's visibility. Setting it also updates any getter/setter method that still shared
    /// the old visibility, so they follow along instead of silently diverging.
    /// </summary>
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

    /// <summary>The visibility values offered by the visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => VisibilityChoices.All;

    /// <summary>The variable's modifiers.</summary>
    public VariableModifiers Modifiers
    {
        get => Variable.Modifiers;
        set => Variable.Modifiers = value;
    }

    /// <summary>Whether <see cref="VariableModifiers.ReadOnly"/> is set.</summary>
    public bool IsReadOnly { get => Has(VariableModifiers.ReadOnly); set => Set(VariableModifiers.ReadOnly, value); }

    /// <summary>Whether <see cref="VariableModifiers.Const"/> is set.</summary>
    public bool IsConst { get => Has(VariableModifiers.Const); set => Set(VariableModifiers.Const, value); }

    /// <summary>Whether <see cref="VariableModifiers.Static"/> is set.</summary>
    public bool IsStatic { get => Has(VariableModifiers.Static); set => Set(VariableModifiers.Static, value); }

    /// <summary>Whether <see cref="VariableModifiers.New"/> is set.</summary>
    public bool IsNew { get => Has(VariableModifiers.New); set => Set(VariableModifiers.New, value); }

    private bool Has(VariableModifiers flag) => Modifiers.HasFlag(flag);

    private void Set(VariableModifiers flag, bool value) => Modifiers = value ? Modifiers | flag : Modifiers & ~flag;

    /// <summary>The variable's getter method graph, or <see langword="null"/> if it has none.</summary>
    public MethodGraph? Getter => Variable.GetterMethod;

    /// <summary>The variable's setter method graph, or <see langword="null"/> if it has none.</summary>
    public MethodGraph? Setter => Variable.SetterMethod;

    /// <summary>Whether the variable has a getter method.</summary>
    public bool HasGetter => Getter is not null;

    /// <summary>Whether the variable has a setter method.</summary>
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
    private void Remove() => services.UndoRedo.Do(EditorCommands.RemoveVariable(Variable.Class, Variable));

    /// <summary>Shows the variable inspector.</summary>
    [RelayCommand]
    private void Select() => services.Messenger.Send(new SelectInspectorMessage(this));

    [RelayCommand]
    private void AddGetter() => services.UndoRedo.Do(EditorCommands.AddGetter(Variable));

    [RelayCommand]
    private void RemoveGetter()
    {
        services.UndoRedo.Do(EditorCommands.RemoveGetter(Variable));
    }

    [RelayCommand]
    private void AddSetter() => services.UndoRedo.Do(EditorCommands.AddSetter(Variable));

    [RelayCommand]
    private void RemoveSetter()
    {
        services.UndoRedo.Do(EditorCommands.RemoveSetter(Variable));
    }

    [RelayCommand]
    private void OpenGetter()
    {
        if (Getter is not null)
        {
            services.Messenger.Send(new OpenGraphMessage(Getter));
        }
    }

    [RelayCommand]
    private void OpenSetter()
    {
        if (Setter is not null)
        {
            services.Messenger.Send(new OpenGraphMessage(Setter));
        }
    }

    [RelayCommand]
    private void OpenTypeGraph() => services.Messenger.Send(new OpenGraphMessage(Variable.TypeGraph));

    /// <summary>Unsubscribes from the wrapped variable's property-changed event.</summary>
    public void Dispose() => ((INotifyPropertyChanged)Variable).PropertyChanged -= OnVariablePropertyChanged;
}
