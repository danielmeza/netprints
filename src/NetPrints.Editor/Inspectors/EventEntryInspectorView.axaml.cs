using Avalonia.Controls;

namespace NetPrints.Editor.Inspectors;

/// <summary>The event entry inspector pane (name, kind and arguments of the selected event entry).</summary>
public partial class EventEntryInspectorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public EventEntryInspectorView()
    {
        InitializeComponent();
    }
}
