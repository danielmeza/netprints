using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Graph;
using NetPrints.Translator;

namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// A pin of a node (PAR-43..48). Uses <see cref="PinKind"/> and <see cref="GraphPoint"/> instead of
/// brushes and UI points; the view pushes the connector position into <see cref="Anchor"/>.
/// </summary>
public sealed partial class NodePinVM : ObservableObject, IDisposable
{
    private readonly INotifyPropertyChanged pinNotifier;

    /// <summary>
    /// Wraps <paramref name="pin"/>: subscribes to its property-changed event, its node's input type
    /// change event, and its own connection-changed event (whichever applies to its concrete pin type).
    /// </summary>
    /// <param name="pin">Pin to wrap.</param>
    /// <param name="node">View model of the node the pin belongs to.</param>
    public NodePinVM(NodePin pin, NodeVM node)
    {
        Pin = pin;
        Node = node;

        pinNotifier = (INotifyPropertyChanged)pin;
        pinNotifier.PropertyChanged += OnPinPropertyChanged;
        pin.Node.InputTypeChanged += OnInputTypeChanged;

        switch (pin)
        {
            case NodeInputDataPin idp:
                idp.IncomingPinChanged += OnInputDataPinIncomingPinChanged;
                break;
            case NodeOutputExecPin oxp:
                oxp.OutgoingPinChanged += OnOutputExecPinOutgoingPinChanged;
                break;
            case NodeInputTypePin itp:
                itp.IncomingPinChanged += OnInputTypePinIncomingPinChanged;
                break;
        }
    }

    /// <summary>Raised when the model connection of this pin changed.</summary>
    public event EventHandler? ConnectionChanged;

    /// <summary>The wrapped model pin.</summary>
    public NodePin Pin { get; }

    /// <summary>The node view model this pin belongs to.</summary>
    public NodeVM Node { get; }

    /// <summary>Exec, data or type, derived from the wrapped pin's concrete type.</summary>
    public PinKind Kind => Pin switch
    {
        NodeExecPin => PinKind.Exec,
        NodeTypePin => PinKind.Type,
        _ => PinKind.Data,
    };

    /// <summary>Whether the wrapped pin is an input pin.</summary>
    public bool IsInput => Pin is NodeInputDataPin or NodeInputExecPin or NodeInputTypePin;

    /// <summary>Whether the wrapped pin is an output pin.</summary>
    public bool IsOutput => !IsInput;

    /// <summary>Stable identity of the pin within its node for UI automation: "in:&lt;name&gt;" or "out:&lt;name&gt;".</summary>
    public string AutomationName => $"{(IsInput ? "in" : "out")}:{Pin.Name}";

    /// <summary>Exec pins are squares (PAR-43).</summary>
    public bool ShowRectangle => Kind == PinKind.Exec;

    /// <summary>Data pins are circles (PAR-43).</summary>
    public bool ShowCircle => Kind == PinKind.Data;

    /// <summary>Type pins are triangles (PAR-43).</summary>
    public bool ShowTriangle => Kind == PinKind.Type;

    /// <summary>Connector position in graph coordinates, pushed by the view.</summary>
    [ObservableProperty]
    public partial GraphPoint Anchor { get; set; }

    /// <summary>Whether the model pin has at least one connection.</summary>
    public bool IsConnected => Pin switch
    {
        NodeInputDataPin idp => idp.IncomingPin != null,
        NodeOutputDataPin odp => odp.OutgoingPins.Count > 0,
        NodeInputExecPin iep => iep.IncomingPins.Count > 0,
        NodeOutputExecPin oep => oep.OutgoingPin != null,
        NodeInputTypePin itp => itp.IncomingPin != null,
        NodeOutputTypePin otp => otp.OutgoingPins.Count > 0,
        _ => false,
    };

    /// <summary>Unconnected pins are drawn dimmed (60 %, PAR-43).</summary>
    public bool IsDimmed => !IsConnected;

    /// <summary>Whether the wrapped pin belongs to a <see cref="RerouteNode"/> (drawn without a label).</summary>
    public bool IsRerouteNodePin => Pin.Node is RerouteNode;

    /// <summary>The wrapped pin's display string (<c>Pin.ToString()</c>).</summary>
    public string DisplayName => Pin.ToString();

    /// <summary>Editable for method entry outputs and return node inputs (PAR-45).</summary>
    public bool IsNameEditable =>
        (Pin.Node is MethodEntryNode && Pin.Node.OutputDataPins.Contains(Pin))
        || (Pin.Node is ReturnNode && Pin.Node.InputDataPins.Contains(Pin));

    /// <summary>Whether to show the pin's name as a plain label (not a reroute node pin, not editable).</summary>
    public bool ShowLabel => !IsRerouteNodePin && !IsNameEditable;

    /// <summary>Whether to show the pin's name as an editable text box (see <see cref="IsNameEditable"/>).</summary>
    public bool ShowEditableName => !IsRerouteNodePin && IsNameEditable;

    /// <summary>The wrapped pin's name. Setting it also refreshes <see cref="DisplayName"/>.</summary>
    public string Name
    {
        get => Pin.Name;
        set
        {
            if (Pin.Name != value)
            {
                Pin.Name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    /// <summary>
    /// Tooltip text: for a data pin, its type and name, its explicit default value if it has one, and
    /// its reflected parameter/return documentation if loaded; a fixed description for an exec pin
    /// (the "Catch" pin also explains what it does); otherwise the pin's name.
    /// </summary>
    public string ToolTip
    {
        get
        {
            string toolTip = "";

            if (Pin is NodeDataPin dataPin)
            {
                toolTip = $"{dataPin.PinType.Value}: {dataPin.Name}";
                string? documentation = null;
                var reflection = Node.Graph.Context.Reflection;

                if (dataPin.Node is CallMethodNode callMethodNode)
                {
                    if (dataPin is NodeInputDataPin inputDataPin)
                    {
                        int paramIndex = callMethodNode.ArgumentPins.IndexOf(inputDataPin);
                        if (paramIndex >= 0)
                        {
                            var parameter = callMethodNode.MethodSpecifier.Parameters[paramIndex];

                            if (parameter.HasExplicitDefaultValue)
                            {
                                toolTip += $"{Environment.NewLine}Default: {TranslatorUtil.ObjectToLiteral(parameter.ExplicitDefaultValue, TypeSpecifier.FromType(parameter.ExplicitDefaultValue?.GetType() ?? typeof(object)))}";
                            }

                            if (reflection.IsLoaded)
                            {
                                documentation = reflection.Provider.GetMethodParameterDocumentation(callMethodNode.MethodSpecifier, paramIndex);
                            }
                        }
                    }
                    else if (dataPin is NodeOutputDataPin outputDataPin && reflection.IsLoaded)
                    {
                        int returnIndex = callMethodNode.OutputDataPins.IndexOf(outputDataPin);
                        if (returnIndex >= 0)
                        {
                            documentation = reflection.Provider.GetMethodReturnDocumentation(callMethodNode.MethodSpecifier, returnIndex);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(documentation))
                {
                    toolTip += Environment.NewLine + Environment.NewLine + documentation;
                }
            }
            else if (Pin is NodeInputExecPin)
            {
                toolTip = "Can be connected to output execution pins to receive execution.";
            }
            else if (Pin is NodeOutputExecPin)
            {
                toolTip = "Can be connected to input execution pins to pass on execution.";

                if (Pin.Name == "Catch")
                {
                    toolTip += Environment.NewLine + Environment.NewLine + "Executed when an exception is thrown on this node. The Exception output data pin will be set to the caught exception.";
                }
            }
            else
            {
                toolTip += Pin.Name;
            }

            return toolTip;
        }
    }

    // Unconnected values (PAR-44)

    private TypeSpecifier? PinTypeSpecifier => (Pin as NodeDataPin)?.PinType.Value as TypeSpecifier;

    private bool UsesUnconnectedValue => Pin is NodeInputDataPin { UsesUnconnectedValue: true } && !IsConnected;

    /// <summary>Text editor for unconnected inputs that are neither enums nor booleans.</summary>
    public bool ShowUnconnectedValue => UsesUnconnectedValue
        && !(PinTypeSpecifier is { } t && (t.IsEnum || t == TypeSpecifier.FromType<bool>()));

    /// <summary>Enum chooser for an unconnected input whose type is an enum.</summary>
    public bool ShowEnumValue => UsesUnconnectedValue && PinTypeSpecifier is { IsEnum: true };

    /// <summary>Checkbox for an unconnected input typed <see cref="bool"/>.</summary>
    public bool ShowBooleanValue => UsesUnconnectedValue && PinTypeSpecifier == TypeSpecifier.FromType<bool>();

    /// <summary>
    /// The enum member names for an unconnected input data pin whose type is an enum and whose host's
    /// reflection is loaded; otherwise <see langword="null"/>.
    /// </summary>
    public IEnumerable<string>? PossibleEnumNames =>
        Pin is NodeInputDataPin && PinTypeSpecifier is { IsEnum: true } typeSpecifier && Node.Graph.Context.Reflection.IsLoaded
            ? Node.Graph.Context.Reflection.Provider.GetEnumNames(typeSpecifier)
            : null;

    /// <summary>Refreshes the values that come from reflection after the host (re)loaded.</summary>
    internal void OnReflectionReloaded()
    {
        OnPropertyChanged(nameof(PossibleEnumNames));
        OnPropertyChanged(nameof(ToolTip));
    }

    /// <summary>
    /// The pin's unconnected value (only meaningful for an input data pin). Setting it converts the
    /// value to the pin's runtime type when known and not an enum (silently ignoring a conversion that
    /// throws <see cref="InvalidCastException"/>, <see cref="FormatException"/> or
    /// <see cref="OverflowException"/>), then raises the dependent properties. <see langword="null"/>
    /// for anything but an input data pin.
    /// </summary>
    public object? UnconnectedValue
    {
        get => (Pin as NodeInputDataPin)?.UnconnectedValue;
        set
        {
            if (Pin is not NodeInputDataPin p || Equals(p.UnconnectedValue, value) || PinTypeSpecifier is not { } typeSpecifier)
            {
                return;
            }

            // Convert to the pin's type when it is a known runtime type; enums use their name.
            Type? type = Type.GetType(typeSpecifier.Name);
            if (value is not null && type is not null && !typeSpecifier.IsEnum)
            {
                try
                {
                    p.UnconnectedValue = Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    return;
                }
            }
            else
            {
                p.UnconnectedValue = value;
            }

            RaiseUnconnectedValueChanged();
        }
    }

    /// <summary>String view of <see cref="UnconnectedValue"/> for the text editor.</summary>
    public string? UnconnectedText
    {
        get => UnconnectedValue is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : UnconnectedValue?.ToString();
        set => UnconnectedValue = value;
    }

    /// <summary><see cref="UnconnectedValue"/> as a <see cref="bool"/>, for the checkbox editor.</summary>
    public bool UnconnectedBool
    {
        get => UnconnectedValue is true;
        set => UnconnectedValue = value;
    }

    /// <summary><see cref="UnconnectedValue"/> as an enum member name, for the enum chooser.</summary>
    public string? UnconnectedEnumName
    {
        get => UnconnectedValue as string;
        set => UnconnectedValue = value;
    }

    /// <summary>Watermark showing the explicit default value of a parameter (PAR-44).</summary>
    public string? UnconnectedTextWatermark =>
        ShowUnconnectedValue && Pin is NodeInputDataPin { UsesExplicitDefaultValue: true } idp && idp.UnconnectedValue is null
            ? idp.ExplicitDefaultValue?.ToString() ?? "null"
            : null;

    /// <summary>Whether to draw the default-value indicator (PAR-43).</summary>
    public bool ShowDefaultValueIndicator => Pin is NodeInputDataPin { UsesExplicitDefaultValue: true };

    /// <summary>The default value is in effect (drawn at full alpha instead of half).</summary>
    public bool IsDefaultValueActive =>
        Pin is NodeInputDataPin idp && idp.IncomingPin is null && (!idp.UsesUnconnectedValue || idp.UnconnectedValue is null);

    /// <summary>Middle click on an unconnected editor clears the value (PAR-44).</summary>
    [RelayCommand]
    public void ClearUnconnectedValue()
    {
        if (Pin is NodeInputDataPin { UsesUnconnectedValue: true } idp)
        {
            idp.UnconnectedValue = null;
            RaiseUnconnectedValueChanged();
        }
    }

    private void RaiseUnconnectedValueChanged()
    {
        OnPropertyChanged(nameof(UnconnectedValue));
        OnPropertyChanged(nameof(UnconnectedText));
        OnPropertyChanged(nameof(UnconnectedBool));
        OnPropertyChanged(nameof(UnconnectedEnumName));
        OnPropertyChanged(nameof(UnconnectedTextWatermark));
        OnPropertyChanged(nameof(IsDefaultValueActive));
    }

    // Connections (PAR-46..48)

    /// <summary>Whether this pin can be connected to another one (type checks with subclass and implicit-cast rules).</summary>
    public bool CanConnectTo(NodePinVM other)
    {
        if (other == this || other.Pin.Node == Pin.Node)
        {
            return false;
        }

        var reflection = Node.Graph.Context.Reflection;
        if (!reflection.IsLoaded)
        {
            // Type compatibility needs the provider; nothing connects until the types are loaded.
            return false;
        }

        var provider = reflection.Provider;
        return GraphUtil.CanConnectNodePins(Pin, other.Pin, provider.TypeSpecifierIsSubclassOf, provider.HasImplicitCast);
    }

    /// <summary>Connects this pin to another pin if they are compatible. Returns whether it connected.</summary>
    public bool ConnectTo(NodePinVM other)
    {
        if (!CanConnectTo(other))
        {
            return false;
        }

        GraphUtil.ConnectNodePins(Pin, other.Pin);
        return true;
    }

    /// <summary>Disconnects all connections of this pin (middle click, PAR-48).</summary>
    [RelayCommand]
    public void DisconnectAll() => GraphUtil.DisconnectPin(Pin);

    /// <summary>Toggles the faint state of the cables of this pin (mouse back button, PAR-48).</summary>
    [RelayCommand]
    public void ToggleFaint() => Node.Graph.ToggleFaint(this);

    /// <summary>
    /// Inserts a reroute node midway on the connection of an input data, output exec or input type pin (PAR-48).
    /// </summary>
    public void AddRerouteNode()
    {
        switch (Pin)
        {
            case NodeInputDataPin { IncomingPin: not null } dataPin:
                {
                    // AddRerouteNode reconnects the pin, so remember the source first.
                    var source = dataPin.IncomingPin;
                    var reroute = GraphUtil.AddRerouteNode(dataPin);
                    reroute.PositionX = (Pin.Node.PositionX + source.Node.PositionX) / 2;
                    reroute.PositionY = (Pin.Node.PositionY + source.Node.PositionY) / 2;
                    break;
                }
            case NodeOutputExecPin { OutgoingPin: not null } execPin:
                {
                    // AddRerouteNode reconnects the pin, so remember the target first.
                    var target = execPin.OutgoingPin;
                    var reroute = GraphUtil.AddRerouteNode(execPin);
                    reroute.PositionX = (Pin.Node.PositionX + target.Node.PositionX) / 2;
                    reroute.PositionY = (Pin.Node.PositionY + target.Node.PositionY) / 2;
                    break;
                }
            case NodeInputTypePin { IncomingPin: not null } typePin:
                {
                    var source = typePin.IncomingPin;
                    var reroute = GraphUtil.AddRerouteNode(typePin);
                    reroute.PositionX = (Pin.Node.PositionX + source.Node.PositionX) / 2;
                    reroute.PositionY = (Pin.Node.PositionY + source.Node.PositionY) / 2;
                    break;
                }
            default:
                throw new InvalidOperationException("Can't add a reroute node for this pin.");
        }
    }

    /// <summary>Raises the connection-dependent properties after the model connections changed.</summary>
    internal void RefreshConnectionState()
    {
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsDimmed));
        OnPropertyChanged(nameof(ShowUnconnectedValue));
        OnPropertyChanged(nameof(ShowEnumValue));
        OnPropertyChanged(nameof(ShowBooleanValue));
        OnPropertyChanged(nameof(UnconnectedTextWatermark));
        OnPropertyChanged(nameof(IsDefaultValueActive));
    }

    private void OnPinPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NodeInputDataPin.UnconnectedValue))
        {
            RaiseUnconnectedValueChanged();
        }
        else if (e.PropertyName == nameof(NodePin.Name))
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    private void OnInputTypeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(PossibleEnumNames));
        OnPropertyChanged(nameof(ShowUnconnectedValue));
        OnPropertyChanged(nameof(ShowBooleanValue));
        OnPropertyChanged(nameof(ShowEnumValue));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ToolTip));
    }

    private void OnInputDataPinIncomingPinChanged(NodeInputDataPin pin, NodeOutputDataPin? oldPin, NodeOutputDataPin? newPin) =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnOutputExecPinOutgoingPinChanged(NodeOutputExecPin pin, NodeInputExecPin? oldPin, NodeInputExecPin? newPin) =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnInputTypePinIncomingPinChanged(NodeInputTypePin pin, NodeOutputTypePin? oldPin, NodeOutputTypePin? newPin) =>
        ConnectionChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Unsubscribes from the wrapped pin's and node's events.</summary>
    public void Dispose()
    {
        pinNotifier.PropertyChanged -= OnPinPropertyChanged;
        Pin.Node.InputTypeChanged -= OnInputTypeChanged;

        switch (Pin)
        {
            case NodeInputDataPin idp:
                idp.IncomingPinChanged -= OnInputDataPinIncomingPinChanged;
                break;
            case NodeOutputExecPin oxp:
                oxp.OutgoingPinChanged -= OnOutputExecPinOutgoingPinChanged;
                break;
            case NodeInputTypePin itp:
                itp.IncomingPinChanged -= OnInputTypePinIncomingPinChanged;
                break;
        }
    }
}
