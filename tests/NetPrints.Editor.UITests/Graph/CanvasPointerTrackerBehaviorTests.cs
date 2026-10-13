using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Xaml.Interactivity;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.Controls;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>The behavior starts the top level's pointer tracker when its control joins the visual tree, before the first popup asks for it.</summary>
public class CanvasPointerTrackerBehaviorTests
{
    private static Window ShowWindow(bool withBehavior)
    {
        var canvas = new Border { Width = 100, Height = 100, Background = Avalonia.Media.Brushes.Transparent };
        if (withBehavior)
        {
            Interaction.GetBehaviors(canvas).Add(new CanvasPointerTrackerBehavior());
        }

        var window = new Window { Width = 200, Height = 200, Content = canvas };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static Point? PositionAfterAMove(bool withBehavior)
    {
        Window window = ShowWindow(withBehavior);
        try
        {
            window.MouseMove(new Point(40, 30));
            HeadlessDriver.Pump();
            return CanvasPointerTracker.For(window).LastPosition;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePointerMovedBeforeAnyPopupAskedIsKnown()
    {
        Assert.Equal(new Point(40, 30), PositionAfterAMove(withBehavior: true));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void WithoutTheBehaviorTheTrackerStartsOnlyWhenAskedFor()
    {
        Assert.Null(PositionAfterAMove(withBehavior: false));
    }
}
