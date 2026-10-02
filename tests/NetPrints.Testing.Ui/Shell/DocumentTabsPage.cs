using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Shell;

/// <summary>Component object of the document area: tabs and contents by document id (the text form of <see cref="DocumentId"/>).</summary>
public sealed class DocumentTabsPage(IUiDriver driver, AutomationQuery window) : UiElement(driver, window)
{
    /// <summary>The tab of a document; its automation id is the document id.</summary>
    public UiElement Tab(string documentId) => new(Driver, new AutomationQuery(documentId) { Within = Query, IncludeHidden = true });

    /// <summary>The tab of a document.</summary>
    public UiElement Tab(DocumentId documentId) => Tab(documentId.ToString());

    /// <summary>The content of a document, shown while its tab is selected.</summary>
    public UiElement Content(string documentId) => Find(AutomationIds.ShellDocumentPrefix + documentId);

    /// <summary>The content of a document.</summary>
    public UiElement Content(DocumentId documentId) => Content(documentId.ToString());

    /// <summary>Waits until the tab of a document is shown.</summary>
    public Task WaitOpenAsync(DocumentId documentId, CancellationToken cancellationToken) => Tab(documentId).WaitVisibleAsync(cancellationToken);

    /// <summary>Waits until the tab of a document is gone.</summary>
    public Task WaitClosedAsync(DocumentId documentId, CancellationToken cancellationToken) =>
        UiWait.UntilAsync(Driver, async () => !await Tab(documentId).ExistsAsync(cancellationToken), $"{Tab(documentId)} closed", cancellationToken);

    /// <summary>Selects a document by clicking its tab and waits for its content.</summary>
    public async Task SelectAsync(DocumentId documentId, CancellationToken cancellationToken)
    {
        await Tab(documentId).ClickAsync(cancellationToken);
        await Content(documentId).WaitVisibleAsync(cancellationToken);
    }

    /// <summary>Whether the tab of a document is the selected one.</summary>
    public async Task<bool> IsSelectedAsync(DocumentId documentId, CancellationToken cancellationToken) =>
        (await Tab(documentId).GetAsync(cancellationToken)).Has(":selected");
}
