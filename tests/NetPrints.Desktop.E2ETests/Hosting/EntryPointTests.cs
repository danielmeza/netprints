using System.Reflection;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// Pins <c>Program.Main</c> as the real assembly entry point carrying <see cref="STAThreadAttribute"/>
/// (R2-01): an <c>async Task&lt;int&gt; Main</c> compiles to a synthesized wrapper entry point that
/// does not carry the attribute, so the Windows UI thread silently becomes MTA (OLE drag-drop and
/// clipboard break) even though the attribute still shows up on <c>Main</c> itself. This only inspects
/// <see cref="EditorProcess.DesktopAssembly"/>'s metadata (no display needed), so unlike the X11
/// scenarios in this project it runs unconditionally, not just under <c>NETPRINTS_E2E=1</c>.
/// </summary>
public class EntryPointTests
{
    [Fact]
    public void MainIsTheRealSTAThreadEntryPoint()
    {
        Assembly desktop = Assembly.LoadFrom(EditorProcess.DesktopAssembly);
        MethodInfo? entryPoint = desktop.EntryPoint;

        Assert.NotNull(entryPoint);
        Assert.Equal("NetPrints.Desktop.Program", entryPoint.DeclaringType?.FullName);
        Assert.Equal("Main", entryPoint.Name);
        Assert.NotNull(entryPoint.GetCustomAttribute<STAThreadAttribute>());
    }
}
