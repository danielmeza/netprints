using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Hosting;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>
/// The ability to use the NetPrints editor through a driver. Wraps the root page object, so the
/// same tasks run headless and on a real desktop.
/// </summary>
public sealed class UseNetPrints(IUiDriver driver, IFileDialogs fileDialogs) : IAbility
{
    public IUiDriver Driver { get; } = driver;

    public IFileDialogs FileDialogs { get; } = fileDialogs;

    public ShellPage Shell => new(Driver);

    public static UseNetPrints With(IUiDriver driver, IFileDialogs fileDialogs) => new(driver, fileDialogs);
}
