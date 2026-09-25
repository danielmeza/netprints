using System.Globalization;
using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Graph;

/// <summary>Component object of a node on the canvas.</summary>
public sealed class NodeObject(IUiDriver driver, AutomationQuery query) : UiElement(driver, query)
{
    public UiElement Label => Find(AutomationIds.NodeLabel);
    public UiElement Overloads => Find(AutomationIds.NodeOverloads);
    public UiElement Pure => Find(AutomationIds.NodePure);
    public UiElement LeftPlus => Find(AutomationIds.NodeLeftPlus);
    public UiElement LeftMinus => Find(AutomationIds.NodeLeftMinus);

    /// <summary>An input pin by name.</summary>
    public PinObject Input(string pin) => new(Driver, new AutomationQuery(AutomationIds.Pin) { Within = Query, Name = "in:" + pin });

    /// <summary>An output pin by name.</summary>
    public PinObject Output(string pin) => new(Driver, new AutomationQuery(AutomationIds.Pin) { Within = Query, Name = "out:" + pin });

    public async Task<IReadOnlyList<string>> PinNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.Pin) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    /// <summary>Clicks the node's title (selects it).</summary>
    public Task SelectAsync(CancellationToken cancellationToken) => Label.ClickAsync(cancellationToken);

    /// <summary>Drags the node by its title.</summary>
    public Task MoveByAsync(double dx, double dy, CancellationToken cancellationToken) => Label.DragByAsync(dx, dy, cancellationToken);

    public async Task<bool> IsSelectedAsync(CancellationToken cancellationToken) => await PropertyAsync("IsSelected", cancellationToken) == "True";

    public async Task<(double X, double Y)> LocationAsync(CancellationToken cancellationToken)
    {
        var e = await GetAsync(cancellationToken);
        return (double.Parse(e["LocationX"]!, CultureInfo.InvariantCulture), double.Parse(e["LocationY"]!, CultureInfo.InvariantCulture));
    }
}

/// <summary>Component object of a pin: its row, its connector dot and its inline value editor.</summary>
public sealed class PinObject(IUiDriver driver, AutomationQuery query) : UiElement(driver, query)
{
    /// <summary>The connector dot, where cables start and end.</summary>
    public UiElement Connector => Find(AutomationIds.PinConnector);

    /// <summary>The inline value editor of an unconnected input (PAR-44).</summary>
    public UiElement ValueBox => Find(AutomationIds.PinValueText);

    /// <summary>The inline check box of an unconnected boolean input.</summary>
    public UiElement ValueCheck => Find(AutomationIds.PinValueCheck);

    /// <summary>The inline chooser of an unconnected enum input.</summary>
    public UiElement ValueEnum => Find(AutomationIds.PinValueEnum);

    /// <summary>Drags a cable from this pin to another pin (PAR-46).</summary>
    public Task ConnectToAsync(PinObject other, CancellationToken cancellationToken) => Connector.DragToAsync(other.Connector, cancellationToken);

    /// <summary>Drags a cable from this pin and releases it at a point.</summary>
    public Task DragCableToAsync(UiTarget target, CancellationToken cancellationToken) => Connector.DragToAsync(target, cancellationToken);

    /// <summary>Middle-clicks the connector: disconnects the pin (PAR-48).</summary>
    public Task DisconnectAsync(CancellationToken cancellationToken) => Connector.ClickAsync(UiButton.Middle, cancellationToken);
}
