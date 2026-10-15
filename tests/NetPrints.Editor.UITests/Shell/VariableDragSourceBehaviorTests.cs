using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.Variables;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The variable name behavior starts the drag onto the canvas for a variable row, and for nothing else.</summary>
public class VariableDragSourceBehaviorTests
{
    private const double SourceX = 100;
    private const double SourceY = 15;
    private const double DropY = 75;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Window ShowSource(object? dataContext, Action<DragEventArgs> onDrop)
    {
        var source = new TextBox { Text = "name", Width = 120, Height = 30, DataContext = dataContext };
        Interaction.GetBehaviors(source).Add(new VariableDragSourceBehavior());
        var target = new Border { Width = 120, Height = 60, Background = Brushes.Transparent };
        DragDrop.SetAllowDrop(target, true);
        target.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DragDropEffects.Copy);
        target.AddHandler(DragDrop.DropEvent, (_, e) => onDrop(e));
        var window = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { source, target } } };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static void DragToTheTarget(Window window)
    {
        var from = new Point(SourceX, SourceY);
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        for (int step = 1; step <= 10; step++)
        {
            window.MouseMove(new Point(SourceX, SourceY + 6 * step), RawInputModifiers.LeftMouseButton);
            HeadlessDriver.Pump();
        }

        window.MouseUp(new Point(SourceX, DropY), MouseButton.Left);
        HeadlessDriver.Pump();
    }

    private static void AddIfPresent(List<object> list, object? value)
    {
        if (value is not null)
        {
            list.Add(value);
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DraggingAVariableNameStartsAVariableDragAndOtherRowsStartNone()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.AddVariableAsync(Token);
        MemberVariableViewModel member = session.ClassContext.Variables[^1];
        session.App.Api.ShowPanel(PanelContributions.VariablesId);
        await session.Page.Variables.AddLocalAsync(Token);
        var panel = Assert.IsType<ShellVariablesPanelViewModel>(session.App.Shell.FindPanel(PanelContributions.VariablesId)?.Content);
        LocalVariableViewModel local = (panel.Current?.MethodVariables ?? throw new InvalidOperationException("No method graph is open.")).Single();

        var dropped = new List<object>();
        Window memberWindow = ShowSource(member, e => AddIfPresent(dropped, e.DataTransfer.TryGetValue(GraphDragDrop.VariableFormat)));
        DragToTheTarget(memberWindow);
        memberWindow.Close();

        Window localWindow = ShowSource(local, e => AddIfPresent(dropped, e.DataTransfer.TryGetValue(GraphDragDrop.LocalVariableFormat)));
        DragToTheTarget(localWindow);
        localWindow.Close();

        Window otherWindow = ShowSource("not a variable", e => dropped.Add(e));
        DragToTheTarget(otherWindow);
        otherWindow.Close();

        Assert.Equal([member, (object)local], dropped);
    }
}
