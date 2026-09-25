using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Main;

public class MainWindowTests
{
    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task StartsAndOpensStartupProject()
    {
        using var sample = new SampleCopy();
        var sw = Stopwatch.StartNew();
        using var main = MainWindowPage.Start();
        var frame = main.Window.CaptureRenderedFrame();
        sw.Stop();

        Assert.NotNull(frame);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"SC-006: main window in {sw.ElapsedMilliseconds} ms");
        Assert.Equal(800, main.Window.Width);
        Assert.Equal(600, main.Window.Height);
        Assert.Equal("NetPrints", main.Window.Title);

        await main.OpenStartupProjectAsync(sample.ProjectPath);

        Assert.Equal("HelloWorld", main.Window.Title); // PAR-01
        Assert.Equal(["HelloWorld.Program"], main.ClassNames); // PAR-05, PAR-11
        Assert.Empty(main.Dialogs.Errors);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ProjectAndSettingsPanesAndEnablement()
    {
        using var sample = new SampleCopy();
        using var main = MainWindowPage.Start();

        Assert.False(main.SettingsButton.IsEffectivelyEnabled); // PAR-07
        Assert.False(main.ReferencesButton.IsEffectivelyEnabled); // PAR-08
        Assert.False(main.CompileButton.IsEffectivelyEnabled);
        Assert.True(ToolTip.GetShowOnDisabled(main.CompileButton)); // PAR-09

        main.ClickProject();
        Assert.True(main.IsProjectPaneVisible);
        Assert.False(main.SaveProjectButton.IsEffectivelyEnabled); // PAR-02

        await main.OpenStartupProjectAsync(sample.ProjectPath);
        Assert.True(main.SaveProjectButton.IsEffectivelyEnabled);
        Assert.True(main.CompileButton.IsEffectivelyEnabled);
        Assert.True(main.RunButton.IsEffectivelyEnabled);

        // With the Project pane still open, one click on Settings switches panes (PAR-02).
        Assert.True(main.IsProjectPaneVisible);
        main.ClickSettings();
        Assert.True(main.IsSettingsPaneVisible);
        Assert.False(main.IsProjectPaneVisible);

        main.ClickProject();
        Assert.True(main.IsProjectPaneVisible);
        Assert.False(main.IsSettingsPaneVisible);
    }
}
