using Avalonia.Controls;

namespace NetPrints.Editor.Controls;

/// <summary>The placeholder view of a contribution whose extension threw while building it.</summary>
public partial class ContributionErrorView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public ContributionErrorView()
    {
        InitializeComponent();
    }
}
