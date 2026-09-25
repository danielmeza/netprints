using Avalonia.Controls;
using Avalonia.Input;
using NetPrints.Editor.ViewModels;
using NetPrints.Editor.Views.Graph;

namespace NetPrints.Editor.Views;

public partial class MemberVariableView : UserControl
{
    public MemberVariableView()
    {
        InitializeComponent();
    }

    private MemberVariableVM? ViewModel => DataContext as MemberVariableVM;

    private void OnNameTapped(object? sender, TappedEventArgs e) => ViewModel?.SelectCommand.Execute(null);

    private void OnGetterDoubleTapped(object? sender, TappedEventArgs e) => ViewModel?.OpenGetterCommand.Execute(null);

    private void OnSetterDoubleTapped(object? sender, TappedEventArgs e) => ViewModel?.OpenSetterCommand.Execute(null);

    // Variables can be dragged onto the graph to open the Get/Set chooser (PAR-57).
    private readonly DragSourceHelper dragSource = new();

    private void OnNamePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } variable)
        {
            dragSource.Pressed(e, this, variable);
        }
    }

    private void OnNamePointerMoved(object? sender, PointerEventArgs e) => dragSource.Moved(e, this);

    private void OnNamePointerReleased(object? sender, PointerReleasedEventArgs e) => dragSource.Released();
}
