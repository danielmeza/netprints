using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// A cable between an output-side pin (<see cref="Source"/>) and an input-side pin (<see cref="Target"/>) (PAR-48).
/// </summary>
public sealed partial class ConnectionVM(NodePinVM source, NodePinVM target) : ObservableObject
{
    public NodePinVM Source { get; } = source;

    public NodePinVM Target { get; } = target;

    public PinKind Kind => Source.Kind;

    /// <summary>Faint cables are thinner and more transparent (toggled with the mouse back button).</summary>
    [ObservableProperty]
    public partial bool IsFaint { get; set; }

    /// <summary>
    /// The pin that owns the connection in the model (input data, output exec or input type pin).
    /// </summary>
    public NodePinVM OwningPin => Source.Pin is NodeOutputExecPin ? Source : Target;

    /// <summary>Middle click: removes the connection.</summary>
    [RelayCommand]
    public void Disconnect() => OwningPin.DisconnectAll();

    /// <summary>Double click: inserts a reroute node midway.</summary>
    [RelayCommand]
    public void InsertReroute() => OwningPin.AddRerouteNode();

    [RelayCommand]
    public void ToggleFaint() => IsFaint = !IsFaint;
}
