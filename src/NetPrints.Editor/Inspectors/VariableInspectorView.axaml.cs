using Avalonia.Controls;

namespace NetPrints.Editor.Inspectors;

/// <summary>The variable inspector pane (name, type, visibility, modifiers, getter/setter of the selected variable).</summary>
public partial class VariableInspectorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public VariableInspectorView()
    {
        InitializeComponent();
    }
}
