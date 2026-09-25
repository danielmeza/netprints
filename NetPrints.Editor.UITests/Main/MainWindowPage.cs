using Avalonia.Controls;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Main;

/// <summary>Page object of the main window, started with recording dialogs and process launcher.</summary>
public sealed class MainWindowPage : IDisposable
{
    private MainWindowPage(EditorComposition composition, MainWindow window, RecordingDialogs dialogs, RecordingProcessLauncher processes)
    {
        Composition = composition;
        Window = window;
        Dialogs = dialogs;
        Processes = processes;
    }

    public EditorComposition Composition { get; }
    public MainWindow Window { get; }
    public RecordingDialogs Dialogs { get; }
    public RecordingProcessLauncher Processes { get; }
    public MainEditorVM ViewModel => Composition.MainEditor!;

    public static MainWindowPage Start()
    {
        var dialogs = new RecordingDialogs();
        var processes = new RecordingProcessLauncher();
        var composition = new EditorComposition(c => c with { Dialogs = dialogs, Processes = processes });
        var window = composition.CreateMainWindow();
        window.Show();
        HeadlessInput.Pump();
        return new MainWindowPage(composition, window, dialogs, processes);
    }

    public Button ProjectButton => Window.ById<Button>(AutomationIds.MainProjectButton);
    public Button ReferencesButton => Window.ById<Button>(AutomationIds.MainReferencesButton);
    public Button SettingsButton => Window.ById<Button>(AutomationIds.MainSettingsButton);
    public Button CompileButton => Window.ById<Button>(AutomationIds.MainCompileButton);
    public Button RunButton => Window.ById<Button>(AutomationIds.MainRunButton);
    public Button SaveProjectButton => Window.ById<Button>(AutomationIds.MainSaveProjectButton);
    public bool IsProjectPaneVisible => Window.ById<StackPanel>(AutomationIds.MainProjectPane).IsVisible;
    public bool IsSettingsPaneVisible => Window.ById<StackPanel>(AutomationIds.MainSettingsPane).IsVisible;

    public IReadOnlyList<string> ClassNames =>
        Window.AllById<Button>(AutomationIds.MainOpenClassButton).Select(b => b.Content as string ?? "").ToList();

    public void ClickProject() => Window.Click(ProjectButton.CenterIn(Window));

    public void ClickSettings() => Window.Click(SettingsButton.CenterIn(Window));

    /// <summary>Opens a project the way the command line does (PAR-05) and waits for its types.</summary>
    public async Task OpenStartupProjectAsync(string path)
    {
        await ViewModel.OpenStartupProjectAsync([path]);
        await HeadlessInput.WaitUntilAsync(() => ViewModel.Project is not null, "project loaded");
        await HeadlessInput.WaitUntilAsync(() => Composition.Context.Reflection.NonStaticTypes.Count > 0, "reflection loaded", 60_000);
    }

    /// <summary>Clicks a class in the class list and returns its editor window.</summary>
    public async Task<ClassEditorPage> OpenClassAsync(string fullName)
    {
        var button = Window.AllById<Button>(AutomationIds.MainOpenClassButton).Single(b => b.Content as string == fullName);
        Window.Click(button.CenterIn(Window));
        await HeadlessInput.WaitUntilAsync(() => Composition.Windows.ClassEditorWindows.Count == 1, "class window opened");
        return new ClassEditorPage(Composition.Windows.ClassEditorWindows.Single());
    }

    public void Dispose() => Window.Close();
}
