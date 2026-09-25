using Avalonia.Controls;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Driving;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>The automation tree and headless driver without an editor (for windows shown on their own, such as dialogs).</summary>
public sealed class HeadlessUi : IDisposable
{
    private HeadlessUi()
    {
        Tree = new AutomationTree();
        Driver = new HeadlessDriver(Tree, () => "");
    }

    public AutomationTree Tree { get; }

    public HeadlessDriver Driver { get; }

    public static HeadlessUi Create() => new();

    /// <summary>Shows a window and tracks it.</summary>
    public T Show<T>(T window) where T : Window
    {
        window.Show();
        Tree.Track(window);
        HeadlessDriver.Pump();
        return window;
    }

    public void Dispose()
    {
        foreach (var window in Tree.Windows.Reverse().ToList())
        {
            window.Close();
        }

        Tree.Dispose();
    }
}
