using Avalonia.Controls;
using NetPrints.Editor.ClassEditor;

namespace NetPrints.Editor.Inspectors;

/// <summary>The class inspector pane (name, namespace, visibility, modifiers).</summary>
public partial class ClassInspectorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public ClassInspectorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // CodeViewVM has no EditorContext (FR-038); give CodeView this pane's own logger factory instead.
    private void OnDataContextChanged(object? sender, EventArgs e) =>
        CodeView.LoggerFactory = (DataContext as ClassEditorVM)?.Context.LoggerFactory;
}
