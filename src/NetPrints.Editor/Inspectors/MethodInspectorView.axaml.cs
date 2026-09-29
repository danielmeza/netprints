using Avalonia.Controls;

namespace NetPrints.Editor.Inspectors;

/// <summary>The method inspector pane (name, visibility and modifiers of the selected method or constructor).</summary>
public partial class MethodInspectorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public MethodInspectorView()
    {
        InitializeComponent();
    }
}
