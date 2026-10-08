using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// A cable between an output-side pin (<see cref="Source"/>) and an input-side pin (<see cref="Target"/>) (PAR-48).
/// </summary>
public sealed partial class ConnectionViewModel(NodePinViewModel source, NodePinViewModel target) : ObservableObject
{
    private IReadOnlyList<CommandEntryViewModel>? menuEntries;

    /// <summary>The output-side pin (exec pin: the outgoing pin; data/type pin: the source of the value).</summary>
    public NodePinViewModel Source { get; } = source;

    /// <summary>The input-side pin (exec pin: the incoming pin; data/type pin: the consumer of the value).</summary>
    public NodePinViewModel Target { get; } = target;

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
    public NodePinViewModel OwningPin => Source.Pin is NodeOutputExecPin ? Source : Target;

    /// <summary>Middle click: removes the connection.</summary>
    [RelayCommand]
    public void Disconnect() => OwningPin.DisconnectAll();

    /// <summary>Double click: inserts a reroute node midway.</summary>
    [RelayCommand]
    public void InsertReroute() => OwningPin.AddRerouteNode();

    /// <summary>Mouse back button: toggles <see cref="IsFaint"/>.</summary>
    [RelayCommand]
    public void ToggleFaint() => IsFaint = !IsFaint;

    /// <summary>The entries of the connection's context menu, each running its command with this connection as the parameter (FR-062); empty while no commands are attached.</summary>
    public IReadOnlyList<CommandEntryViewModel> MenuEntries => menuEntries ??= BuildMenu();

    /// <summary>The tooltip text of the cable: the heading, the type and the documentation, from the registered tooltip providers; null while none gives any.</summary>
    public string? ToolTip =>
        Source.Node.Graph.Commands?.TooltipFor(new TooltipTarget(TooltipTargetKind.Connection, this)) is { } content
            ? string.Join(Environment.NewLine, [content.Title, .. content.Lines, .. content.Documentation is { } documentation ? (string[])["", documentation] : []])
            : null;

    /// <summary>Gets the end that is farther from a click on the cable.</summary>
    /// <param name="click">The click, in graph units.</param>
    /// <returns>The farther end.</returns>
    public ConnectionEnd FartherEnd(GraphPoint click) => ConnectionEnds.Farther(click, Source.Anchor, Target.Anchor);

    /// <summary>Ctrl+click: moves the view to the end farther from the click.</summary>
    /// <param name="click">The click, in graph units.</param>
    public void GoToFartherEnd(GraphPoint click) =>
        Source.Node.Graph.Commands?.TryRun(FartherEnd(click) == ConnectionEnd.Source ? "goToSource" : "goToTarget", CommandScope.Graph, this);

    private List<CommandEntryViewModel> BuildMenu()
    {
        List<CommandEntryViewModel> entries = [];
        if (Source.Node.Graph.Commands is { } invoker)
        {
            foreach (CommandDescriptor command in invoker.ContextMenuCommands(ContextMenuTarget.Connection))
            {
                entries.Add(new CommandEntryViewModel(command, invoker, AutomationIds.ConnectionMenuPrefix, CommandScope.Graph, this));
            }
        }

        return entries;
    }
}
