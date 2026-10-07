using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Core.Events;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.State;

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
    private readonly ILogger logger;
    private readonly Dictionary<DocumentViewModel, PropertyChangedEventHandler> titleWatchers = [];
    private List<string> suspendedPanels = [];
    private Dictionary<string, string> suspendedActive = [];
    private Dictionary<string, double> suspendedProportions = [];

    /// <summary>Creates the adapter with the default layout.</summary>
    /// <param name="shell">The shell state the layout keeps in step.</param>
    /// <param name="projectActions">The project flows.</param>
    /// <param name="openDocument">Creates the view model of a document, or null when the id names nothing that exists.</param>
    /// <param name="logger">Logs a saved layout that cannot be restored.</param>
    public DockShellAdapter(ShellViewModel shell, IProjectActions projectActions, Func<DocumentId, DocumentViewModel?> openDocument, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(projectActions);
        ArgumentNullException.ThrowIfNull(openDocument);
        ArgumentNullException.ThrowIfNull(logger);
        this.shell = shell;
        this.openDocument = openDocument;
        this.logger = logger;
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
        factory.DockableMoved += OnLayoutEvent;
        factory.DockableDocked += OnLayoutEvent;
        factory.DockableUndocked += OnLayoutEvent;
        factory.WindowAdded += OnLayoutEvent;
        factory.WindowMoveDragEnd += OnLayoutEvent;
    }

    /// <summary>Raised when the layout changed in a way worth saving: a tab or panel moved, opened, closed, hidden or activated, a window came or went, or the layout was replaced.</summary>
    internal event EventHandler? LayoutChanged;

    /// <summary>Raised just before the tool panels are hidden for lack of a project, while the layout still has them.</summary>
    internal event EventHandler? PanelsSuspending;

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

        if ((factory.FindDocumentDock() ?? factory.AddDocumentDock()) is not { } dock)
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
            if (factory.DockHome(panel) is not { } homed)
            {
                SyncPanels();
                return;
            }

            tool = homed;
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
        factory.DockableMoved -= OnLayoutEvent;
        factory.DockableDocked -= OnLayoutEvent;
        factory.DockableUndocked -= OnLayoutEvent;
        factory.WindowAdded -= OnLayoutEvent;
        factory.WindowMoveDragEnd -= OnLayoutEvent;
        foreach ((DocumentViewModel model, PropertyChangedEventHandler handler) in titleWatchers)
        {
            model.PropertyChanged -= handler;
        }

        titleWatchers.Clear();
    }

    /// <summary>Gets a value indicating whether the tool panels are hidden because no project is open.</summary>
    internal bool PanelsSuspended { get; private set; }

    /// <summary>Hides every tool panel while no project is open, or brings the ones it hid back to their docks.</summary>
    /// <param name="suspended">Whether the panels are hidden.</param>
    internal void SetPanelsSuspended(bool suspended)
    {
        if (suspended == PanelsSuspended)
        {
            return;
        }

        if (suspended)
        {
            PanelsSuspending?.Invoke(this, EventArgs.Empty);
            PanelsSuspended = true;
            HideVisiblePanels();
        }
        else
        {
            PanelsSuspended = false;
            RestoreSuspendedPanels();
        }
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

    /// <summary>Describes the layout as it is now.</summary>
    /// <returns>The persisted form.</returns>
    internal DockLayoutDto CaptureLayout() => DockLayoutMapper.Capture(Layout, shell.ActiveDocument?.Id.ToString());

    /// <summary>Replaces the layout by a saved one; a layout that cannot be used is logged and leaves the current one.</summary>
    /// <param name="state">The saved state, or null for none.</param>
    internal void RestoreLayout(LayoutState? state)
    {
        if (LayoutSerializer.FromState(state, logger) is not { } saved)
        {
            return;
        }

        try
        {
            ApplyLayout(saved);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NullReferenceException or ArgumentException)
        {
            Log.LayoutUnusable(logger, exception, "it does not fit this editor");
            RebuildLayout();
        }
    }

    private void ApplyLayout(DockLayoutDto saved)
    {
        factory.SuppressHoming = true;
        try
        {
            HashSet<DocumentId> placed = [];
            BuiltLayout built = DockLayoutMapper.Build(factory, shell.Panels, saved, id => PlaceDocument(id, placed));
            foreach (IDockWindow window in (Layout.Windows ?? []).ToList())
            {
                window.Host?.Exit();
            }

            IRootDock created = built.Root;
            factory.MainLayout = created;
            created.HiddenDockables = factory.CreateList<IDockable>([.. built.Hidden]);
            factory.InitLayout(created);
            foreach (ShellTool hidden in built.Hidden)
            {
                hidden.OriginalOwner = factory.DefaultDockOf(hidden.DefaultDock);
            }

            Layout = created;
            foreach ((IDockable dock, FloatingWindowDto bounds) in built.Windows)
            {
                factory.AddFloatingWindow(created, dock, bounds);
            }

            DockLeftoverDocuments(placed);
            factory.DockOrphanedPanels();
            ActivateRestored(saved.ActiveDocument);
            HideVisiblePanelsWhileSuspended();
            SyncPanels();
        }
        finally
        {
            factory.SuppressHoming = false;
        }
    }

    private ShellDocument? PlaceDocument(string text, HashSet<DocumentId> placed)
    {
        if (!DocumentId.TryParse(text, out DocumentId id) || !placed.Add(id))
        {
            return null;
        }

        DocumentViewModel? model = shell.FindDocument(id);
        if (model is null)
        {
            if (openDocument(id) is not { } created)
            {
                return null;
            }

            model = shell.AddDocument(created);
            if (!ReferenceEquals(model, created))
            {
                created.Dispose();
            }
        }

        var document = new ShellDocument { Id = id.ToString(), Title = model.Title, Context = model };
        Unwatch(model);
        Watch(model, document);
        return document;
    }

    private void DockLeftoverDocuments(HashSet<DocumentId> placed)
    {
        foreach (DocumentViewModel model in shell.Documents.Where(model => !placed.Contains(model.Id)).ToList())
        {
            IDocumentDock dock = factory.FindDocumentDock() ?? factory.AddDocumentDock() ?? throw new InvalidOperationException("The layout has nowhere to put a document.");
            var document = new ShellDocument { Id = model.Id.ToString(), Title = model.Title, Context = model };
            Unwatch(model);
            Watch(model, document);
            factory.AddDockable(dock, document);
        }
    }

    private void ActivateRestored(string? savedActive)
    {
        DocumentViewModel? target = DocumentId.TryParse(savedActive, out DocumentId id) ? shell.FindDocument(id) : null;
        target ??= shell.ActiveDocument is { } current && shell.FindDocument(current.Id) is not null ? current : null;
        target ??= shell.Documents.FirstOrDefault();
        if (target is not null && FindDocument(target.Id) is { } document)
        {
            Activate(document, target);
        }
        else
        {
            shell.ActiveDocument = null;
        }
    }

    private void Unwatch(DocumentViewModel model)
    {
        if (titleWatchers.Remove(model, out PropertyChangedEventHandler? old))
        {
            model.PropertyChanged -= old;
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
                Unwatch(model);
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

            HideVisiblePanelsWhileSuspended();
            SyncPanels();
        }
        finally
        {
            factory.SuppressHoming = false;
        }
    }

    private void HideVisiblePanelsWhileSuspended()
    {
        if (PanelsSuspended)
        {
            HideVisiblePanels();
        }
    }

    private void HideVisiblePanels()
    {
        List<ShellTool> visible = [.. ShellDockFactory.Walk(Layout).OfType<ShellTool>()];
        suspendedPanels = [.. visible.Select(tool => tool.Id)];
        suspendedActive = [];
        foreach (ShellTool tool in visible)
        {
            if (tool.Owner is IDock owner && ReferenceEquals(owner.ActiveDockable, tool))
            {
                suspendedActive[owner.Id] = tool.Id;
            }
        }

        List<IProportionalDock> columns = [.. ShellDockFactory.Walk(Layout).OfType<IProportionalDock>().Where(IsToolColumn)];
        suspendedProportions = ShellDockFactory.Walk(Layout).Where(dockable => dockable is IDock && !double.IsNaN(dockable.Proportion)).ToDictionary(dockable => dockable.Id, dockable => dockable.Proportion);
        foreach (string id in suspendedPanels)
        {
            HidePanel(id);
        }

        foreach (IProportionalDock column in columns)
        {
            column.Proportion = 0;
        }
    }

    private static bool IsToolColumn(IProportionalDock dock) =>
        dock.VisibleDockables?.Where(child => child is not IProportionalDockSplitter).All(child => child is IToolDock) == true;

    private void RestoreSuspendedPanels()
    {
        List<IDockable> docks = [.. ShellDockFactory.Walk(Layout)];
        foreach (IDockable column in docks.Where(dock => dock is IProportionalDock && suspendedProportions.ContainsKey(dock.Id)))
        {
            column.Proportion = suspendedProportions[column.Id];
        }

        foreach (string id in suspendedPanels.OrderBy(id => shell.FindPanel(id)?.Order ?? int.MaxValue))
        {
            if (Layout.HiddenDockables?.FirstOrDefault(hidden => hidden.Id == id) is { } hidden)
            {
                factory.RestoreDockable(hidden);
            }
        }

        List<IDockable> shown = [.. ShellDockFactory.Walk(Layout)];
        foreach ((string dockId, string toolId) in suspendedActive)
        {
            if (shown.FirstOrDefault(dockable => dockable.Id == toolId) is { } tool && shown.FirstOrDefault(dockable => dockable.Id == dockId) is IDock)
            {
                factory.SetActiveDockable(tool);
            }
        }

        foreach (IDockable dock in shown.Where(dockable => dockable is IDock && suspendedProportions.ContainsKey(dockable.Id)))
        {
            dock.Proportion = suspendedProportions[dock.Id];
        }

        suspendedPanels = [];
        suspendedActive = [];
        suspendedProportions = [];
        SyncPanels();
    }

    private void SyncPanels()
    {
        foreach (PanelViewModel panel in shell.Panels)
        {
            panel.IsVisible = IsPanelVisible(panel.Id);
        }

        shell.NotifyLayoutChanged();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLayoutEvent(object? sender, EventArgs e) => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private void OnActiveDockableChanged(object? sender, ActiveDockableChangedEventArgs e)
    {
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        if (e.Dockable is ShellDocument { Context: DocumentViewModel model } && shell.FindDocument(model.Id) is not null)
        {
            shell.ActiveDocument = model;
        }
    }

    private void OnDockableClosed(object? sender, DockableClosedEventArgs e)
    {
        if (e.Dockable is ShellDocument document && DocumentId.TryParse(document.Id, out DocumentId id) && !factory.SuppressHoming)
        {
            if (shell.FindDocument(id) is { } model)
            {
                Unwatch(model);
            }

            shell.RemoveDocument(id);
        }
    }

    private void OnPanelsMayHaveChanged(object? sender, EventArgs e) => SyncPanels();
}
