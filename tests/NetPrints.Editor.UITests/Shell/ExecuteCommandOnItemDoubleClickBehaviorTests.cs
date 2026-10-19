using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The item double-click behavior runs its command for a row of its own list and for nothing outside it.</summary>
public class ExecuteCommandOnItemDoubleClickBehaviorTests
{
    private sealed record OuterItem;

    private static (Window Window, List<string> Executed) Show()
    {
        var executed = new List<string>();
        var list = new ListBox
        {
            ItemsSource = new[] { "row" },
            Height = 150,
            Width = 200,
            Background = Brushes.Transparent,
        };
        Interaction.GetBehaviors(list).Add(new ExecuteCommandOnItemDoubleClickBehavior
        {
            Command = new RelayCommand<string>(text => executed.Add(text ?? "")),
        });
        var outer = new ItemsControl
        {
            ItemsSource = new object[] { new OuterItem() },
            ItemTemplate = new FuncDataTemplate<OuterItem>((_, _) => list),
        };
        var window = new Window { Width = 300, Height = 300, Content = outer };
        window.Show();
        window.UpdateLayout();
        HeadlessDriver.Pump();
        return (window, executed);
    }

    private static void DoubleClick(Window window, double x, double y)
    {
        var point = new Point(x, y);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADoubleClickBelowTheRowsRunsNothingAndThrowsNothing()
    {
        (Window window, List<string> executed) = Show();

        DoubleClick(window, 100, 140);

        Assert.Empty(executed);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADoubleClickOnARowRunsTheCommandWithItsItem()
    {
        (Window window, List<string> executed) = Show();

        DoubleClick(window, 100, 15);

        Assert.Equal(["row"], executed);
    }
}
