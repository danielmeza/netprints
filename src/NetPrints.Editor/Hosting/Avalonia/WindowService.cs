using Avalonia.Controls;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;

namespace NetPrints.Editor.Hosting.Avalonia;

/// <summary>
/// Registry of class editor windows keyed by class (PAR-11, PAR-14, PAR-22).
/// </summary>
public sealed class WindowService : IWindowService
{
    private readonly Dictionary<ClassGraph, ClassEditorWindow> windows = new(ReferenceEqualityComparer.Instance);

    /// <summary>The main window (owner of dialogs when no class window is active).</summary>
    public Window? MainWindow { get; set; }

    /// <summary>The active window, used as dialog owner and for pickers.</summary>
    public Window? ActiveWindow =>
        windows.Values.FirstOrDefault(w => w.IsActive) as Window ?? (MainWindow?.IsActive == true ? MainWindow : null)
        ?? MainWindow ?? windows.Values.FirstOrDefault();

    /// <summary>Every currently open class editor window.</summary>
    public IReadOnlyCollection<ClassEditorWindow> ClassEditorWindows => windows.Values;

    /// <inheritdoc/>
    public bool TryActivateClassEditor(ClassGraph cls)
    {
        if (!windows.TryGetValue(cls, out var window))
        {
            return false;
        }

        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        return true;
    }

    /// <inheritdoc/>
    public void OpenClassEditor(ClassEditorVM editor)
    {
        var window = new ClassEditorWindow
        {
            DataContext = editor,
            WindowState = WindowState.Maximized,
        };

        window.Closed += (_, _) =>
        {
            windows.Remove(editor.Class);
            editor.Dispose();
        };

        windows[editor.Class] = window;
        editor.StartGeneratedCodeLoop();
        window.Show();
    }

    /// <inheritdoc/>
    public void CloseClassEditor(ClassGraph cls)
    {
        if (windows.TryGetValue(cls, out var window))
        {
            window.Close();
        }
    }

    /// <inheritdoc/>
    public void CloseAllClassEditors()
    {
        foreach (var window in windows.Values.ToList())
        {
            window.Close();
        }
    }
}
