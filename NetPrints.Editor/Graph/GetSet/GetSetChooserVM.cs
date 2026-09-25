using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph;

namespace NetPrints.Editor.Graph.GetSet;

/// <summary>
/// Popup offering Get and Set for a variable (PAR-55, PAR-57).
/// </summary>
public sealed partial class GetSetChooserVM(NodeGraphVM graph) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>Where the node is created (graph coordinates).</summary>
    [ObservableProperty]
    public partial GraphPoint Position { get; set; }

    [ObservableProperty]
    public partial VariableSpecifier? Variable { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetCommand))]
    public partial bool CanGet { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SetCommand))]
    public partial bool CanSet { get; set; }

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
