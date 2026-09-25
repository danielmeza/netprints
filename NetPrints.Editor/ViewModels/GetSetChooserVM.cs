using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.ViewModels;

/// <summary>
/// Popup offering Get and Set for a variable (PAR-55, PAR-57).
/// </summary>
public sealed partial class GetSetChooserVM(NodeGraphVM graph) : ObservableObject
{
    [ObservableProperty]
    private bool isOpen;

    /// <summary>Where the node is created (graph coordinates).</summary>
    [ObservableProperty]
    private GraphPoint position;

    [ObservableProperty]
    private VariableSpecifier? variable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetCommand))]
    private bool canGet;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetCommand))]
    private bool canSet;

    /// <summary>Opens the chooser; Get and Set are enabled according to the accessor visibility.</summary>
    public void Open(VariableSpecifier variable, GraphPoint position)
    {
        var provider = graph.Context.Reflection.Provider;
        var fromType = graph.Graph.Class?.Type ?? variable.DeclaringType;

        Variable = variable;
        Position = position;
        CanGet = NetPrintsUtil.IsVisible(fromType, variable.DeclaringType, variable.GetterVisibility, provider.TypeSpecifierIsSubclassOf);
        CanSet = NetPrintsUtil.IsVisible(fromType, variable.DeclaringType, variable.SetterVisibility, provider.TypeSpecifierIsSubclassOf);
        IsOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanGet))]
    private void Get()
    {
        if (Variable is not null)
        {
            graph.AddNode<VariableGetterNode>(Position, null, Variable);
        }

        Close();
    }

    [RelayCommand(CanExecute = nameof(CanSet))]
    private void Set()
    {
        if (Variable is not null)
        {
            graph.AddNode<VariableSetterNode>(Position, null, Variable);
        }

        Close();
    }

    /// <summary>Closes the chooser (also when the pointer leaves it).</summary>
    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        Variable = null;
    }
}
