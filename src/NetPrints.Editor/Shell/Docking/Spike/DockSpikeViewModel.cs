using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace NetPrints.Editor.Shell.Docking.Spike;

/// <summary>The spike window's view model: the layout and the float and re-dock commands the E2E test drives.</summary>
internal sealed partial class DockSpikeViewModel : ObservableObject
{
    private readonly SpikeDockFactory factory;

    /// <summary>Creates the spike with the default layout.</summary>
    /// <param name="factory">The layout factory.</param>
    public DockSpikeViewModel(SpikeDockFactory factory)
        : this(factory, factory?.CreateLayout() ?? throw new ArgumentNullException(nameof(factory)))
    {
    }

    /// <summary>Creates the spike around an existing layout, such as a loaded one.</summary>
    /// <param name="factory">The layout factory.</param>
    /// <param name="layout">The layout to show.</param>
    public DockSpikeViewModel(SpikeDockFactory factory, IRootDock layout)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(layout);
        this.factory = factory;
        Layout = layout;
        factory.InitLayout(Layout);
    }

    /// <summary>Gets the factory.</summary>
    public SpikeDockFactory Factory => factory;

    /// <summary>Gets the layout the dock control shows.</summary>
    public IRootDock Layout { get; }

    /// <summary>Moves the second document into its own window.</summary>
    [RelayCommand]
    private void FloatDocument()
    {
        if (factory.Find(dockable => dockable.Id == SpikeDockFactory.SecondDocumentId).FirstOrDefault() is { } document)
        {
            factory.FloatDockable(document);
        }
    }

    /// <summary>Moves the second document back into the main window's document dock as a tab.</summary>
    [RelayCommand]
    private void DockDocument()
    {
        if (SpikeDockFactory.Walk(Layout).FirstOrDefault(dockable => dockable.Id == SpikeDockFactory.SecondDocumentId) is { Owner: IDock source } document
            && factory.Find(dockable => dockable.Id == SpikeDockFactory.DocumentsId).OfType<IDocumentDock>().FirstOrDefault(dock => !ReferenceEquals(dock, source)) is { } target)
        {
            factory.MoveDockable(source, target, document, null);
            factory.SetActiveDockable(document);
        }
    }
}
