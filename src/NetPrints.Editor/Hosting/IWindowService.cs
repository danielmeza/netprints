namespace NetPrints.Editor.Hosting;

/// <summary>
/// The application's window operations that view models need.
/// </summary>
public interface IWindowService
{
    /// <summary>Closes the main window, which ends the application.</summary>
    void CloseMainWindow();
}
