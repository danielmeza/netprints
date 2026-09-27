using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Events;

/// <summary>Component object of the class editor's event graph list (US4, ED-T07).</summary>
public sealed class EventGraphsPage(IUiDriver driver, AutomationQuery window)
    : UiElement(driver, new AutomationQuery(AutomationIds.EventGraphList) { Within = window })
{
    public UiElement CreateButton => new(Driver, new AutomationQuery(AutomationIds.CreateEventGraphButton) { Within = window });

    /// <summary>An event graph row (name text) in the list.</summary>
    public UiElement EventGraph(string name) => Find(AutomationIds.EventGraphName, text: name);

    public async Task<IReadOnlyList<string>> EventGraphNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.EventGraphName) { Within = Query }, cancellationToken))
            .Select(e => e.Text ?? "").ToList();

    /// <summary>Clicks Create and returns the new row's default name (EventGraph, EventGraph1, ...).</summary>
    public async Task<string> CreateAsync(CancellationToken cancellationToken)
    {
        var before = await EventGraphNamesAsync(cancellationToken);
        await CreateButton.ClickAsync(cancellationToken);
        var after = await UiWait.ForAsync(Driver, () => EventGraphNamesAsync(cancellationToken),
            names => names.Count == before.Count + 1, "a new event graph row", cancellationToken);
        return after.Except(before).Single();
    }

    /// <summary>Double-clicks an event graph row, opening it.</summary>
    public Task DoubleClickAsync(string name, CancellationToken cancellationToken) => EventGraph(name).DoubleClickAsync(cancellationToken);
}
