using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>
/// The <see cref="IShell"/> over Dock.Avalonia (ADR-0018). It keeps <see cref="ShellViewModel"/>'s documents, active
/// document and panel visibility in step with the Dock layout, whether a change comes from a call or from the user.
/// </summary>
public sealed partial class DockShellAdapter : ObservableObject, IShell, IShellLayoutHost, IDisposable
{
    private readonly ShellViewModel shell;
    private readonly ShellDockFactory factory;
    private readonly Func<DocumentId, DocumentViewModel?> openDocument;
    private readonly Dictionary<DocumentViewModel, PropertyChangedEventHandler> titleWatchers = [];

    /// <summary>Creates the adapter with the default layout.</summary>
    /// <param name="shell">The shell state the layout keeps in step.</param>
    /// <param name="projectActions">The project flows.</param>
    /// <param name="openDocument">Creates the view model of a document, or null when the id names nothing that exists.</param>
    public DockShellAdapter(ShellViewModel shell, IProjectActions projectActions, Func<DocumentId, DocumentViewModel?> openDocument)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(projectActions);
        ArgumentNullException.ThrowIfNull(openDocument);
        this.shell = shell;
        this.openDocument = openDocument;
        ProjectActions = projectActions;
        factory = new ShellDockFactory(shell.Panels);
        Layout = NewLayout();
        factory.ActiveDockableChanged += OnActiveDockableChanged;
        factory.DockableClosed += OnDockableClosed;
        factory.DockableHidden += OnPanelsMayHaveChanged;
        factory.DockableRestored += OnPanelsMayHaveChanged;
        factory.DockableAdded += OnPanelsMayHaveChanged;
        factory.DockableRemoved += OnPanelsMayHaveChanged;
        factory.WindowClosed += OnPanelsMayHaveChanged;
    }

    /// <inheritdoc/>
    public IProjectActions ProjectActions { get; }

    /// <summary>Gets the layout the dock control shows; replaced by <see cref="ResetLayout"/>.</summary>
    [ObservableProperty]
    internal partial IRootDock Layout { get; private set; }

    /// <inheritdoc/>
    public DocumentId? ActiveDocument => shell.ActiveDocument?.Id;

    /// <inheritdoc/>
    public IReadOnlyList<DocumentId> OpenDocuments =>
        [.. ShellDockFactory.Walk(Layout).OfType<ShellDocument>().Select(document => DocumentId.TryParse(document.Id, out DocumentId id) ? id : null).OfType<DocumentId>()];

    /// <inheritdoc/>
    public bool IsPanelVisible(string panelId) => ShellDockFactory.Walk(Layout).OfType<ShellTool>().Any(tool => tool.Id == panelId);

    /// <inheritdoc/>
    public bool IsFloating(DocumentId id) => FindDocument(id) is { } document && ShellDockFactory.IsFloating(Layout, document);

    /// <inheritdoc/>
    public void OpenDocument(DocumentId id)
    {
        if (FindDocument(id) is not null)
        {
            ActivateDocument(id);
            return;
        }

        if (openDocument(id) is not { } created)
        {
            return;
        }

        DocumentViewModel model = shell.AddDocument(created);
        if (!ReferenceEquals(model, created))
        {
            created.Dispose();
        }

        if (factory.FindDocumentDock() is not { } dock)
        {
            RebuildLayout();
            return;
        }

        var document = new ShellDocument { Id = id.ToString(), Title = model.Title, Context = model };
        Watch(model, document);
        factory.AddDockable(dock, document);
        Activate(document, model);
    }

    /// <inheritdoc/>
    public void ActivateDocument(DocumentId id)
    {
        if (FindDocument(id) is { Context: DocumentViewModel model } document)
        {
            Activate(document, model);
        }
    }

    /// <inheritdoc/>
    public void CloseDocument(DocumentId id)
    {
        if (FindDocument(id) is { } document)
        {
            factory.CloseDockable(document);
        }
    }

    /// <inheritdoc/>
    public void ShowPanel(string panelId)
    {
        if (shell.FindPanel(panelId) is not { } panel)
        {
            return;
        }

        if (ShellDockFactory.Walk(Layout).OfType<ShellTool>().FirstOrDefault(tool => tool.Id == panelId) is { } shown)
        {
            factory.SetActiveDockable(shown);
        }
        else if (Layout.HiddenDockables?.FirstOrDefault(hidden => hidden.Id == panelId) is { } hidden)
        {
            factory.RestoreDockable(hidden);
            factory.SetActiveDockable(hidden);
        }
        else if (factory.DockHome(panel) is null)
        {
            RebuildLayout();
        }

        SyncPanels();
    }

    /// <inheritdoc/>
    public void HidePanel(string panelId)
    {
        if (ShellDockFactory.Walk(Layout).OfType<ShellTool>().FirstOrDefault(tool => tool.Id == panelId) is not { } tool)
        {
            return;
        }

        if (ShellDockFactory.IsFloating(Layout, tool) && shell.FindPanel(panelId) is { } panel)
        {
            factory.CloseDockable(tool);
            Layout.HiddenDockables?.Remove(tool);
            factory.DockHome(panel);
            tool = ShellDockFactory.Walk(Layout).OfType<ShellTool>().First(candidate => candidate.Id == panelId);
        }

        factory.HideDockable(tool);
        SyncPanels();
    }

    /// <inheritdoc/>
    public void FloatDocument(DocumentId id)
    {
        if (FindDocument(id) is { } document && !ShellDockFactory.IsFloating(Layout, document))
        {
            factory.FloatDockable(document);
            ActivateDocument(id);
            shell.NotifyLayoutChanged();
        }
    }

    /// <inheritdoc/>
    public void DockDocument(DocumentId id)
    {
        if (FindDocument(id) is { Owner: IDock source } document
            && ShellDockFactory.IsFloating(Layout, document)
            && factory.FindDocumentDock() is { } target
            && !ReferenceEquals(source, target))
        {
            factory.MoveDockable(source, target, document, null);
            ActivateDocument(id);
            shell.NotifyLayoutChanged();
        }
    }

    /// <inheritdoc/>
    public void ResetLayout() => RebuildLayout();

    /// <summary>Stops listening to the layout and to the documents' titles.</summary>
    public void Dispose()
    {
        factory.ActiveDockableChanged -= OnActiveDockableChanged;
        factory.DockableClosed -= OnDockableClosed;
        factory.DockableHidden -= OnPanelsMayHaveChanged;
        factory.DockableRestored -= OnPanelsMayHaveChanged;
        factory.DockableAdded -= OnPanelsMayHaveChanged;
        factory.DockableRemoved -= OnPanelsMayHaveChanged;
        factory.WindowClosed -= OnPanelsMayHaveChanged;
        foreach ((DocumentViewModel model, PropertyChangedEventHandler handler) in titleWatchers)
        {
            model.PropertyChanged -= handler;
        }

        titleWatchers.Clear();
    }

    /// <summary>Moves a panel into a window of its own.</summary>
    /// <param name="panelId">The panel id.</param>
    internal void FloatPanel(string panelId)
    {
        if (ShellDockFactory.Walk(Layout).OfType<ShellTool>().FirstOrDefault(tool => tool.Id == panelId) is { } tool && !ShellDockFactory.IsFloating(Layout, tool))
        {
            factory.FloatDockable(tool);
        }
    }

    private IRootDock NewLayout()
    {
        IRootDock created = factory.CreateLayout();
        factory.MainLayout = created;
        factory.InitLayout(created);
        return created;
    }

    private ShellDocument? FindDocument(DocumentId id) =>
        ShellDockFactory.Walk(Layout).OfType<ShellDocument>().FirstOrDefault(document => document.Id == id.ToString());

    private void Activate(ShellDocument document, DocumentViewModel model)
    {
        factory.SetActiveDockable(document);
        if (document.Owner is IDock owner)
        {
            factory.SetFocusedDockable(owner, document);
        }

        ShellDockFactory.FloatingWindowOf(Layout, document)?.Host?.SetActive();

        shell.ActiveDocument = model;
    }

    private void Watch(DocumentViewModel model, ShellDocument document)
    {
        PropertyChangedEventHandler handler = (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentViewModel.Title))
            {
                document.Title = model.Title;
            }
        };
        model.PropertyChanged += handler;
        titleWatchers[model] = handler;
    }

    private void RebuildLayout()
    {
        factory.SuppressHoming = true;
        try
        {
            DocumentViewModel? active = shell.ActiveDocument;
            foreach (IDockWindow window in (Layout.Windows ?? []).ToList())
            {
                window.Host?.Exit();
            }

            IRootDock created = NewLayout();
            IDocumentDock? dock = factory.FindDocumentDock();
            ShellDocument? activeDocument = null;
            foreach (DocumentViewModel model in shell.Documents)
            {
                var document = new ShellDocument { Id = model.Id.ToString(), Title = model.Title, Context = model };
                titleWatchers.Remove(model, out PropertyChangedEventHandler? old);
                if (old is not null)
                {
                    model.PropertyChanged -= old;
                }

                Watch(model, document);
                if (dock is not null)
                {
                    factory.AddDockable(dock, document);
                }

                activeDocument = ReferenceEquals(model, active) ? document : activeDocument;
            }

            Layout = created;
            if (activeDocument is not null && active is not null)
            {
                Activate(activeDocument, active);
            }

            SyncPanels();
        }
        finally
        {
            factory.SuppressHoming = false;
        }
    }

    private void SyncPanels()
    {
        foreach (PanelViewModel panel in shell.Panels)
        {
            panel.IsVisible = IsPanelVisible(panel.Id);
        }

        shell.NotifyLayoutChanged();
    }

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        if (e.Dockable is ShellDocument { Context: DocumentViewModel model } && shell.FindDocument(model.Id) is not null)
        {
            shell.ActiveDocument = model;
        }
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        if (e.Dockable is ShellDocument document && DocumentId.TryParse(document.Id, out DocumentId id) && !factory.SuppressHoming)
        {
            if (shell.FindDocument(id) is { } model && titleWatchers.Remove(model, out PropertyChangedEventHandler? handler))
            {
                model.PropertyChanged -= handler;
            }

            shell.RemoveDocument(id);
        }
    }

    private void OnPanelsMayHaveChanged(object? sender, EventArgs e) => SyncPanels();
}
