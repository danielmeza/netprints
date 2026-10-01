using Avalonia.Controls;

namespace NetPrints.Editor.Shell.Docking.Spike;

/// <summary>The spike window the desktop host opens when <c>NETPRINTS_DOCK_SPIKE=1</c>.</summary>
internal partial class DockSpikeWindow : Window
{
    /// <summary>The environment variable that makes the host open the spike window.</summary>
    public const string EnableVariable = "NETPRINTS_DOCK_SPIKE";

    /// <summary>Loads the window's XAML and gives it a fresh spike view model.</summary>
    public DockSpikeWindow()
        : this(new DockSpikeViewModel(new SpikeDockFactory()))
    {
    }

    /// <summary>Loads the window's XAML around <paramref name="viewModel"/>.</summary>
    /// <param name="viewModel">The spike view model.</param>
    public DockSpikeWindow(DockSpikeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Whether the host should open the spike window.</summary>
    /// <returns>True when <see cref="EnableVariable"/> is <c>1</c>.</returns>
    public static bool IsSpikeEnabled() => Environment.GetEnvironmentVariable(EnableVariable) == "1";
}
