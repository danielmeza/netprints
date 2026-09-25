using System.Globalization;
using NetPrints.Editor.Hosting.Automation;

namespace NetPrints.Testing.Ui.Driving;

/// <summary>
/// A handle to one UI element, located by an automation id query each time it is used (never a
/// cached reference, so it survives re-rendering). The base of every component object.
/// </summary>
public class UiElement(IUiDriver driver, AutomationQuery query)
{
    public IUiDriver Driver { get; } = driver;

    public AutomationQuery Query { get; } = query;

    /// <summary>A descendant of this element.</summary>
    public UiElement Find(string automationId, string? name = null, string? text = null, int? index = null) =>
        new(Driver, new AutomationQuery(automationId) { Within = Query, Name = name, Text = text, Index = index });

    /// <summary>The element now, or null when it is not there (a snapshot; no waiting).</summary>
    public async Task<AutomationElement?> TryGetAsync(CancellationToken cancellationToken)
    {
        var matches = await Driver.FindAllAsync(Query, cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }

    /// <summary>Waits until exactly one element matches and returns it.</summary>
    public async Task<AutomationElement> GetAsync(CancellationToken cancellationToken) =>
        (await UiWait.ForAsync(Driver, () => TryGetAsync(cancellationToken), e => e is not null, $"{this} to be shown", cancellationToken))!;

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken) => await TryGetAsync(cancellationToken) is not null;

    /// <summary>Whether the element exists and is effectively visible (no waiting).</summary>
    public async Task<bool> IsVisibleAsync(CancellationToken cancellationToken)
    {
        var matches = await Driver.FindAllAsync(Query with { IncludeHidden = true }, cancellationToken);
        return matches.Count == 1 && matches[0].IsVisible;
    }

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => (await GetAsync(cancellationToken)).IsEnabled;

    public async Task<string?> TextAsync(CancellationToken cancellationToken) => (await GetAsync(cancellationToken)).Text;

    /// <summary>A property of the element (see <see cref="AutomationTree"/> for the names).</summary>
    public async Task<string?> PropertyAsync(string property, CancellationToken cancellationToken) =>
        (await GetAsync(cancellationToken))[property];

    /// <summary>A property of the element, parsed with the invariant culture.</summary>
    public async Task<T> GetAsync<T>(string property, CancellationToken cancellationToken) where T : IParsable<T>
    {
        string? value = await PropertyAsync(property, cancellationToken);
        return T.Parse(value ?? throw new InvalidOperationException($"{this} has no {property}."), CultureInfo.InvariantCulture);
    }

    /// <summary>Waits until the element satisfies a condition.</summary>
    public Task WaitUntilAsync(Func<AutomationElement, bool> condition, string what, CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        UiWait.UntilAsync(Driver, async () => await TryGetAsync(cancellationToken) is { } e && condition(e), $"{this}: {what}", cancellationToken, timeout);

    public Task WaitVisibleAsync(CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        UiWait.UntilAsync(Driver, () => IsVisibleAsync(cancellationToken), $"{this} visible", cancellationToken, timeout);

    public Task WaitHiddenAsync(CancellationToken cancellationToken, TimeSpan? timeout = null) =>
        UiWait.UntilAsync(Driver, async () => !await IsVisibleAsync(cancellationToken), $"{this} hidden", cancellationToken, timeout);

    /// <summary>A point inside the element, as a fraction of its size.</summary>
    public async Task<UiTarget> PointAsync(double fractionX, double fractionY, CancellationToken cancellationToken) =>
        Driver.At(await GetAsync(cancellationToken), fractionX, fractionY);

    /// <summary>A point at an offset from the element's top-left corner, in DIPs.</summary>
    public async Task<UiTarget> OffsetAsync(double x, double y, CancellationToken cancellationToken)
    {
        var element = await GetAsync(cancellationToken);
        return Driver.At(element, x / element.Bounds.Width, y / element.Bounds.Height);
    }

    public Task<UiTarget> CenterAsync(CancellationToken cancellationToken) => PointAsync(0.5, 0.5, cancellationToken);

    public Task ClickAsync(CancellationToken cancellationToken) => ClickAsync(UiButton.Left, cancellationToken);

    public async Task ClickAsync(UiButton button, CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await CenterAsync(cancellationToken), button, 1, cancellationToken);

    public async Task DoubleClickAsync(CancellationToken cancellationToken) =>
        await Driver.ClickAsync(await CenterAsync(cancellationToken), UiButton.Left, 2, cancellationToken);

    public async Task HoverAsync(CancellationToken cancellationToken) =>
        await Driver.MoveAsync(await CenterAsync(cancellationToken), cancellationToken);

    public async Task DragToAsync(UiElement target, CancellationToken cancellationToken) =>
        await Driver.DragAsync(await CenterAsync(cancellationToken), await target.CenterAsync(cancellationToken), UiButton.Left, cancellationToken);

    public async Task DragToAsync(UiTarget target, CancellationToken cancellationToken) =>
        await Driver.DragAsync(await CenterAsync(cancellationToken), target, UiButton.Left, cancellationToken);

    public async Task DragByAsync(double dx, double dy, CancellationToken cancellationToken)
    {
        var from = await CenterAsync(cancellationToken);
        await Driver.DragAsync(from, from.Offset(dx, dy), UiButton.Left, cancellationToken);
    }

    /// <summary>The element's pixels, cut from a screenshot of its window.</summary>
    public async Task<Snapshots.UiImage> ScreenshotAsync(CancellationToken cancellationToken)
    {
        var element = await GetAsync(cancellationToken);
        var window = await Driver.ScreenshotAsync(element.Window, cancellationToken);
        return window.Crop((int)Math.Round(element.Bounds.X), (int)Math.Round(element.Bounds.Y),
            (int)Math.Round(element.Bounds.Width), (int)Math.Round(element.Bounds.Height));
    }

    public override string ToString() => Query.ToString();
}
