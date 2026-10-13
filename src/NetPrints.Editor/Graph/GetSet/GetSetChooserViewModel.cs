using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph.GetSet;

/// <summary>
/// Popup offering Get and Set for a variable (PAR-55, PAR-57).
/// </summary>
public sealed partial class GetSetChooserViewModel(NodeGraphViewModel graph) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>Where the node is created (graph coordinates).</summary>
    [ObservableProperty]
    public partial GraphPoint Position { get; set; }

    [ObservableProperty]
    public partial VariableSpecifier? Variable { get; set; }

    /// <summary>The pin the search was opened for; the new node is connected to it.</summary>
    public NodePin? Pin { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetCommand))]
    public partial bool CanGet { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetCommand))]
    public partial bool CanSet { get; set; }

    /// <summary>
    /// Opens the chooser. Get and Set are enabled according to the accessor visibility, except for a
    /// method-local variable (<see cref="VariableScope.Local"/>, US5, no declaring type), which is
    /// always readable and writable from its own method. With a <paramref name="pin"/> (a pin search), the node is
    /// connected to it, and the chooser is skipped when the choice is clear: an input data pin takes a Get, and an
    /// output pin takes the only accessor that is enabled. While the reflection provider is still loading
    /// the visibility cannot be checked, so both are enabled.
    /// </summary>
    public void Open(VariableSpecifier variable, GraphPoint position, NodePin? pin = null)
    {
        Variable = variable;
        Position = position;
        Pin = pin;

        if (variable.DeclaringType is null || !graph.Context.Reflection.IsLoaded)
        {
            CanGet = true;
            CanSet = true;
        }
        else
        {
            var provider = graph.Context.Reflection.Provider;
            var fromType = graph.Graph.Class?.Type ?? variable.DeclaringType;
            CanGet = NetPrintsUtil.IsVisible(fromType, variable.DeclaringType, variable.GetterVisibility, provider.TypeSpecifierIsSubclassOf);
            CanSet = NetPrintsUtil.IsVisible(fromType, variable.DeclaringType, variable.SetterVisibility, provider.TypeSpecifierIsSubclassOf);
        }

        if (pin is NodeInputDataPin && CanGet)
        {
            Get();
        }
        else if (pin is not null && CanGet != CanSet)
        {
            if (CanGet)
            {
                Get();
            }
            else
            {
                Set();
            }
        }
        else
        {
            IsOpen = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGet))]
    private void Get()
    {
        if (Variable is not null)
        {
            graph.AddNode<VariableGetterNode>(Position, Pin, Variable);
        }

        Close();
    }

    [RelayCommand(CanExecute = nameof(CanSet))]
    private void Set()
    {
        if (Variable is not null)
        {
            graph.AddNode<VariableSetterNode>(Position, Pin, Variable);
        }

        Close();
    }

    /// <summary>Closes the chooser (also when the pointer leaves it).</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        Variable = null;
        Pin = null;
    }
}
