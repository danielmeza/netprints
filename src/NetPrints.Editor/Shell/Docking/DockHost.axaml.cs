using Avalonia.Controls;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>The dock control of the shell window, bound to a <see cref="DockShellAdapter"/>.</summary>
public partial class DockHost : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public DockHost()
    {
        InitializeComponent();
    }
}
