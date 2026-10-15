using Avalonia.Controls;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The method list of the override dialog and the overload flyout (FR-094): a filter box above one virtualized list of
/// group headers and method rows. Its <c>DataContext</c> is a <see cref="MethodPickerListViewModel"/>, which owns the
/// filter, the selection and the pick and cancel commands; the keys are behaviors in the XAML.
/// </summary>
public partial class MethodPickerList : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public MethodPickerList()
    {
        InitializeComponent();
    }
}
