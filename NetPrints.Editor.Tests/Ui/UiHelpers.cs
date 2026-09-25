using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Tests.Ui;

/// <summary>Helpers for driving the real editor windows headless.</summary>
public static class UiHelpers
{
    /// <summary>Creates the editor with fake dialogs and process launcher (no modal windows or processes).</summary>
    public static (EditorComposition Composition, MainWindow Window, FakeDialogs Dialogs, FakeProcessLauncher Processes) StartEditor()
    {
        var dialogs = new FakeDialogs();
        var processes = new FakeProcessLauncher();
        var composition = new EditorComposition(c => c with { Dialogs = dialogs, Processes = processes });
        var window = composition.CreateMainWindow();
        window.Show();
        UiTest.Pump();
        return (composition, window, dialogs, processes);
    }

    public static IEnumerable<T> Descendants<T>(this Visual root) where T : Visual => root.GetVisualDescendants().OfType<T>();

    public static T Find<T>(this Control root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    /// <summary>Center of a control in window coordinates.</summary>
    public static Point CenterIn(this Visual visual, Visual window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)
        ?? throw new InvalidOperationException("Control is not in the window.");

    public static void Click(this Window window, Point point, MouseButton button = MouseButton.Left, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseMove(point, modifiers);
        window.MouseDown(point, button, modifiers);
        window.MouseUp(point, button, modifiers);
        UiTest.Pump();
    }

    public static void Drag(this Window window, Point from, Point to, MouseButton button = MouseButton.Left, int steps = 10)
    {
        window.MouseMove(from);
        window.MouseDown(from, button);
        for (int i = 1; i <= steps; i++)
        {
            window.MouseMove(new Point(from.X + (to.X - from.X) * i / steps, from.Y + (to.Y - from.Y) * i / steps));
            UiTest.Pump();
        }

        window.MouseUp(to, button);
        UiTest.Pump();
    }

    /// <summary>Opens the class window of the only class and returns it with its view model.</summary>
    public static async Task<(ClassEditorWindow Window, ClassEditorVM Editor)> OpenClassAsync(EditorComposition composition, MainEditorVM main)
    {
        var cls = main.Project!.Classes.First();
        main.OpenClassCommand.Execute(cls);
        await UiTest.WaitUntilAsync(() => composition.Windows.ClassEditorWindows.Count == 1);
        var window = composition.Windows.ClassEditorWindows.Single();
        return (window, (ClassEditorVM)window.DataContext!);
    }
}
