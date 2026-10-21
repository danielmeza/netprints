using System.Text.Json;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>Rebinds a built-in and an extension command with the keyboard only, resolves a conflict, restarts and runs both through their new gestures (SC-007).</summary>
public sealed class RebindShortcutsTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string PaletteLabel = "Command palette…";
    private const string ExtensionLabel = "Test hello";
    private const string KeyboardPage = "Keyboard shortcuts";
    private const string ExtensionGesture = "Ctrl+Shift+K";
    private const string PaletteGesture = "Ctrl+Shift+L";
    private const string ExtensionPathVariable = "NETPRINTS_EXTENSION_PATH";

    private string? lastFiltered;

    private string ConfigDirectory => Path.Combine(Work, "config");

    private string MarkerFile => Path.Combine(Work, "extension-ran.txt");

    protected override EditorStart EditorStartFor(string sampleProject) => new(sampleProject, IsolatedEnvironment(), WaitForProject: true);

    private Dictionary<string, string> IsolatedEnvironment()
    {
        Dictionary<string, string> variables = Environment();
        variables["XDG_CONFIG_HOME"] = ConfigDirectory;
        variables[ExtensionPathVariable] = Path.GetDirectoryName(FixtureExtensions.TestExtensionFolder()) ?? "";
        variables["NETPRINTS_TEST_COMMAND_MARKER"] = MarkerFile;
        return variables;
    }

    private UiElement Row(string label) => new(Driver, new AutomationQuery(AutomationIds.ShortcutsEditRow) { Name = label });

    private UiElement AddButton(string label) => new(Driver, new AutomationQuery(AutomationIds.ShortcutsAdd) { Within = Row(label).Query });

    private UiElement Filter => new(Driver, new AutomationQuery(AutomationIds.ShortcutsFilter));

    private Task WaitFocusedAsync(UiElement element, string what, CancellationToken token) =>
        element.WaitUntilAsync(e => e[AutomationPropertyNames.IsFocused] == "True", what, token);

    private async Task RecordAsync(string label, string gesture, CancellationToken token)
    {
        await WaitFocusedAsync(Filter, "the filter has the keyboard", token);
        await Driver.PressAsync("Ctrl+A", token);
        await Driver.TypeAsync(label, token);
        if (lastFiltered is { } previous && previous != label)
        {
            await Row(previous).WaitHiddenAsync(token);
        }

        lastFiltered = label;
        await Row(label).WaitVisibleAsync(token);
        await Driver.PressAsync("Enter", token);
        await AddButton(label).WaitUntilAsync(e => e.Text == "Press keys…", "recording", token);
        await Driver.PressAsync(gesture, token);
    }

    private string[] StoredGestures(string command, string member)
    {
        string? file = Directory.EnumerateFiles(Work, "settings.json", SearchOption.AllDirectories).FirstOrDefault();
        if (file is null)
        {
            return [];
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        if (!document.RootElement.TryGetProperty("keyboard", out JsonElement keyboard))
        {
            return [];
        }

        foreach (JsonElement entry in keyboard.GetProperty("bindings").EnumerateArray())
        {
            if (entry.GetProperty("command").GetString() == command)
            {
                return [.. entry.GetProperty(member).EnumerateArray().Select(item => item.GetString() ?? "")];
            }
        }

        return [];
    }

    private async Task WaitStoredAsync(string command, string member, string gesture, CancellationToken token)
    {
        try
        {
            await WaitForAsync(() => StoredGestures(command, member).Contains(gesture), $"{gesture} stored for {command}", token);
        }
        catch (TimeoutException)
        {
            string? file = Directory.EnumerateFiles(Work, "settings.json", SearchOption.AllDirectories).FirstOrDefault();
            throw new TimeoutException($"{gesture} was not stored for {command}; the settings file holds: {(file is null ? "nothing" : File.ReadAllText(file))}");
        }
    }

    [Fact]
    public Task ABuiltInAndAnExtensionCommandRunThroughTheirNewGesturesAfterARestart() => RunScenarioAsync(async token =>
    {
        await StartAsync(token);
        await WaitForProjectAsync(token);

        using (Step("open the Keyboard shortcuts page"))
        {
            await Driver.PressAsync("Ctrl+,", token);
            await WaitFocusedAsync(new UiElement(Driver, new AutomationQuery(AutomationIds.SettingsSearch)), "the settings search has the keyboard", token);
            await Driver.TypeAsync(KeyboardPage, token);
            await Driver.PressAsync("Down", token);
            await Driver.PressAsync("Tab", token);
            await Row(PaletteLabel).WaitVisibleAsync(token);
            await Row(ExtensionLabel).WaitVisibleAsync(token);
        }

        using (Step("give the palette a gesture, then take it for the extension command"))
        {
            await RecordAsync(PaletteLabel, ExtensionGesture, token);
            await WaitStoredAsync("netprints.command.commandPalette", "add", ExtensionGesture, token);

            await RecordAsync(ExtensionLabel, ExtensionGesture, token);
            var conflict = new UiElement(Driver, new AutomationQuery(AutomationIds.ShortcutsConflict));
            await conflict.WaitVisibleAsync(token);
            Assert.Contains(PaletteLabel, (await conflict.GetAsync(token)).Name ?? "", StringComparison.Ordinal);
            await WaitFocusedAsync(new UiElement(Driver, new AutomationQuery(AutomationIds.ShortcutsConflictAccept)), "Move has the keyboard", token);
            await Driver.PressAsync("Enter", token);
            await WaitStoredAsync("netprints.test.hello", "add", ExtensionGesture, token);
        }

        using (Step("give the palette its new gesture"))
        {
            await RecordAsync(PaletteLabel, PaletteGesture, token);
            await WaitStoredAsync("netprints.command.commandPalette", "add", PaletteGesture, token);
            Assert.DoesNotContain(ExtensionGesture, StoredGestures("netprints.command.commandPalette", "add"));
        }

        using (Step("restart the editor"))
        {
            await CloseWindowAndWaitForExitAsync(token);
            await RestartEditorAsync(EditorStartFor(Path.Combine(SampleDirectory, "HelloWorld.csproj")), token);
            await WaitForProjectAsync(token);
        }

        using (Step("the palette opens with its new gesture"))
        {
            var palette = new CommandPalettePage(Driver);
            await Driver.PressAsync(PaletteGesture, token);
            await palette.WaitVisibleAsync(token);
            await Driver.PressAsync("Escape", token);
            await palette.WaitHiddenAsync(token);
        }

        using (Step("the extension command runs with its new gesture"))
        {
            Assert.False(File.Exists(MarkerFile));
            await Driver.PressAsync(ExtensionGesture, token);
            await WaitForAsync(() => File.Exists(MarkerFile), "the extension command ran", token);
        }
    });
}
