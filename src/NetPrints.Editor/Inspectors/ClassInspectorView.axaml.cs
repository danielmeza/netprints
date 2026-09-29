using Avalonia.Controls;

namespace NetPrints.Editor.Inspectors;

/// <summary>The class inspector pane (name, namespace, visibility, modifiers).</summary>
public partial class ClassInspectorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public ClassInspectorView()
    {
        InitializeComponent();
    }
}
