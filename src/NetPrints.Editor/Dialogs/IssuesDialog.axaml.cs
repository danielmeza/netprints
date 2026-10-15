using Avalonia.Controls;
using NetPrints.Compilation;

namespace NetPrints.Editor.Dialogs;

/// <summary>Lists diagnostics, one row each (extension load failures at startup, editor-services.md §5).</summary>
public partial class IssuesDialog : Window
{
    /// <summary>Loads the dialog's XAML, with no title or issues set.</summary>
    public IssuesDialog() : this("", [])
    {
    }

    /// <summary>Loads the dialog's XAML with a title and the issues to list.</summary>
    /// <param name="title">Dialog window title.</param>
    /// <param name="issues">The diagnostics, listed in order as <c>Id: Message</c>.</param>
    public IssuesDialog(string title, IReadOnlyList<CodeDiagnostic> issues)
    {
        Title = title;
        DataContext = new IssuesDialogViewModel(issues);
        InitializeComponent();
    }
}
