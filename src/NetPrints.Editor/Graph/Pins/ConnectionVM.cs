using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// A cable between an output-side pin (<see cref="Source"/>) and an input-side pin (<see cref="Target"/>) (PAR-48).
/// </summary>
public sealed partial class ConnectionVM(NodePinVM source, NodePinVM target) : ObservableObject
{
    /// <summary>The output-side pin (exec pin: the outgoing pin; data/type pin: the source of the value).</summary>
    public NodePinVM Source { get; } = source;

    /// <summary>The input-side pin (exec pin: the incoming pin; data/type pin: the consumer of the value).</summary>
    public NodePinVM Target { get; } = target;

    /// <summary>The kind of pin this connects (exec, data or type), taken from <see cref="Source"/>.</summary>
    public PinKind Kind => Source.Kind;

    /// <summary>Stable identity of the cable for UI automation: "&lt;node&gt;.&lt;pin&gt;-&gt;&lt;node&gt;.&lt;pin&gt;".</summary>
    public string AutomationName => $"{Source.Pin.Node.Name}.{Source.Pin.Name}->{Target.Pin.Node.Name}.{Target.Pin.Name}";

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

    /// <summary>Mouse back button: toggles <see cref="IsFaint"/>.</summary>
    [RelayCommand]
    public void ToggleFaint() => IsFaint = !IsFaint;
}
