using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Screen object of the start page: the recent list (search, open, pin, unpin, remove), the new and open buttons.</summary>
public sealed class StartPagePage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.StartPageRoot))
{
    public UiElement NewButton => Find(AutomationIds.StartPageNewButton);

    public UiElement OpenButton => Find(AutomationIds.StartPageOpenButton);

    public UiElement Search => Find(AutomationIds.StartPageRecentSearch);

    public UiElement ReopenLast => Find(AutomationIds.StartPageReopenLast);

    public UiElement LearnGuide => Find(AutomationIds.StartPageLearnGuide);

    public UiElement LearnShortcuts => Find(AutomationIds.StartPageLearnShortcuts);

    public UiElement LearnDocs => Find(AutomationIds.StartPageLearnDocs);

    public UiElement LearnReleaseNotes => Find(AutomationIds.StartPageReleasesLink);

    public UiElement Empty => Find(AutomationIds.StartPageRecentEmpty);

    /// <summary>A recent row by project name.</summary>
    public RecentRow Recent(string name) => new(Driver, Query, name);

    /// <summary>The names of the rows the recent list shows now, in order.</summary>
    public async Task<IReadOnlyList<string>> RecentNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.StartPageRecentRow) { Within = Query }, cancellationToken)).Select(e => e.Name ?? "").ToList();

    /// <summary>Replaces the search text.</summary>
    public async Task SearchForAsync(string text, CancellationToken cancellationToken)
    {
        await Search.ClickAsync(cancellationToken);
        if (text.Length == 0)
        {
            int length = (await Search.TextAsync(cancellationToken))?.Length ?? 0;
            for (int i = 0; i < length; i++)
            {
                await Driver.PressAsync("BackSpace", cancellationToken);
            }
        }
        else
        {
            await Driver.PressAsync("Ctrl+A", cancellationToken);
            await Driver.TypeAsync(text, cancellationToken);
        }
    }

    /// <summary>Waits until the recent list shows exactly these names, in order.</summary>
    public Task WaitForRecentAsync(IReadOnlyList<string> names, CancellationToken cancellationToken) =>
        UiWait.UntilAsync(Driver, async () => (await RecentNamesAsync(cancellationToken)).SequenceEqual(names), $"recent list [{string.Join(", ", names)}]", cancellationToken);
}

/// <summary>One row of the recent list.</summary>
public sealed class RecentRow(IUiDriver driver, AutomationQuery page, string name)
    : UiElement(driver, new AutomationQuery(AutomationIds.StartPageRecentRow) { Within = page, Name = name })
{
    public UiElement Open => Find(AutomationIds.StartPageRecentOpen);

    public UiElement Pin => Find(AutomationIds.StartPageRecentPin);

    public UiElement Unpin => Find(AutomationIds.StartPageRecentUnpin);

    public UiElement Remove => Find(AutomationIds.StartPageRecentRemove);
}
