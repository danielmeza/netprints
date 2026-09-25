using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;

namespace NetPrints.Editor.Tests.Ui;

[TestClass]
public class AppStartupTests
{
    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task StartsAndOpensStartupProject() => UiTest.RunAsync(async () =>
    {
        string path = TestPaths.CopyHelloWorldSample();
        try
        {
            var sw = Stopwatch.StartNew();
            var (composition, window, dialogs, _) = UiHelpers.StartEditor();
            var frame = window.CaptureRenderedFrame();
            sw.Stop();

            Assert.IsNotNull(frame);
            Assert.IsLessThan(5000, sw.ElapsedMilliseconds, "SC-006: main window in under 5 s");
            Assert.AreEqual(800, window.Width);
            Assert.AreEqual(600, window.Height);
            Assert.AreEqual("NetPrints", window.Title);

            // A single argument opens the project (PAR-05).
            var main = composition.MainEditor!;
            await main.OpenStartupProjectAsync([path]);
            await UiTest.WaitUntilAsync(() => main.Project is not null);

            Assert.AreEqual("HelloWorld", window.Title, "the title shows the project name (PAR-01)");
            var classButtons = window.Descendants<Button>().Where(b => b.Content as string == "HelloWorld.Program").ToList();
            Assert.HasCount(1, classButtons, "the class list shows the class (PAR-11)");
            Assert.IsEmpty(dialogs.Errors);

            window.Close();
        }
        finally
        {
            TestPaths.TryDelete(path);
        }
    });

    [TestMethod]
    [Timeout(60000, CooperativeCancellation = true)]
    public Task ProjectAndSettingsPanesAndEnablement() => UiTest.RunAsync(async () =>
    {
        var (composition, window, _, _) = UiHelpers.StartEditor();
        var main = composition.MainEditor!;

        var settings = window.Find<Button>("SettingsButton");
        var references = window.Find<Button>("ReferencesButton");
        var compile = window.Find<Button>("CompileButton");
        var save = window.Descendants<Button>().First(b => b.Content as string == "Save Project");
        Assert.IsFalse(settings.IsEffectivelyEnabled, "disabled without a project (PAR-07)");
        Assert.IsFalse(references.IsEffectivelyEnabled, "disabled without a project (PAR-08)");
        Assert.IsFalse(compile.IsEffectivelyEnabled);
        Assert.IsTrue(ToolTip.GetShowOnDisabled(compile), "tooltips show on disabled buttons (PAR-09)");

        // Clicking Project opens the Project pane.
        window.Click(window.Find<Button>("ProjectButton").CenterIn(window));
        Assert.IsTrue(main.IsProjectPaneOpen);
        Assert.IsTrue(window.Find<StackPanel>("ProjectPane").IsVisible);
        Assert.IsFalse(save.IsEffectivelyEnabled, "Save needs an open project (PAR-02)");

        string path = TestPaths.CopyHelloWorldSample();
        try
        {
            await main.LoadProjectAsync(path);
            UiTest.Pump();
            Assert.IsTrue(save.IsEffectivelyEnabled);
            Assert.IsTrue(compile.IsEffectivelyEnabled);
            Assert.IsTrue(window.Find<Button>("RunButton").IsEffectivelyEnabled, "the sample is an executable");

            main.ToggleSettingsPaneCommand.Execute(null);
            UiTest.Pump();
            Assert.IsTrue(window.Find<StackPanel>("SettingsPane").IsVisible);
            Assert.IsFalse(window.Find<StackPanel>("ProjectPane").IsVisible, "panes are exclusive");
        }
        finally
        {
            window.Close();
            TestPaths.TryDelete(path);
        }
    });
}
