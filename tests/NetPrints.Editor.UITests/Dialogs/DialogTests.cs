using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.References;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Projects;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.References;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Dialogs;

public class DialogTests
{
    private static readonly TypeSpecifier[] Types = [TypeSpecifier.FromType<object>(), TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<int>()];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Open", SampleTargetChoice.Open)]
    [InlineData("Change", SampleTargetChoice.Change)]
    [InlineData("Cancel", SampleTargetChoice.Cancel)]
    public async Task TheSampleTargetDialogNamesTheFolderAndClosesWithTheChoice(string button, SampleTargetChoice expected)
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new SampleTargetDialog("HelloWorld", "/projects/HelloWorld"));
        var page = new SampleTargetDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal("/projects/HelloWorld", await page.Folder.TextAsync(Token));
        await (button switch { "Open" => page.OpenButton, "Change" => page.ChangeButton, _ => page.CancelButton }).ClickAsync(Token);

        Assert.True(closed);
        Assert.Equal(expected, dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectTypeDefaultsToObjectAndResolvesText()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new SelectTypeDialog(Types, TypeSpecifier.FromType<object>()));
        var page = new SelectTypeDialogPage(ui.Driver);

        Assert.Equal(TypeSpecifier.FromType<object>(), dialog.ResolveSelection()); // PAR-58
        Assert.Equal("System.Object", await page.TypeBox.TextAsync(Token));

        await page.TypeBox.ClickAsync(Token);
        await ui.Driver.PressAsync("Ctrl+A", Token);
        await ui.Driver.TypeAsync("System.String", Token);
        Assert.Equal(TypeSpecifier.FromType<string>(), dialog.ResolveSelection()); // editable chooser

        bool closed = false;
        dialog.Closed += (_, _) => closed = true;
        await ui.Driver.PressAsync("Escape", Token); // dismisses the AutoCompleteBox's suggestion popup
        await page.SelectButton.ClickAsync(Token); // batch X2b: DialogViewModel + DialogCloseBehavior
        Assert.True(closed);
        Assert.Equal(TypeSpecifier.FromType<string>(), dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task NewProjectTypesANameAndAFolderThenCreatesAndClosesWithThePath()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        var service = new ProjectTemplateService(() => registry.ProjectTemplates, _ => DefaultProjectProfile.Instance, projects);
        string location = Path.Combine(Path.GetTempPath(), "netprints-ui-" + Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(location, "Typed");
        try
        {
            using var ui = HeadlessUi.Create();
            var viewModel = new NewProjectDialogViewModel(service, new QueuedFilePicker(), new ProjectLocations(null, location), TimeProvider.System);
            var dialog = ui.Show(new NewProjectDialog(viewModel));
            var page = new NewProjectDialogPage(ui.Driver);
            bool closed = false;
            dialog.Closed += (_, _) => closed = true;

            Assert.False(await page.CreateButton.IsEnabledAsync(Token));
            await page.CreateAsync("Typed", location, Token);
            await page.WaitHiddenAsync(Token);

            Assert.True(closed);
            Assert.Equal(Path.Combine(folder, "Typed.csproj"), dialog.Result);
            Assert.True(File.Exists(Path.Combine(folder, "Program.netpc.json")));
        }
        finally
        {
            TryDeleteParent(folder);
        }
    }

    private static void TryDeleteParent(string folder)
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(folder) ?? folder, recursive: true);
        }
        catch (IOException)
        {
            // A temporary folder left behind is harmless.
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectMethodPreselectsFirst()
    {
        var stringType = TypeSpecifier.FromType<string>();
        MethodSpecifier[] methods =
        [
            new("Trim", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, []),
            new("ToUpper", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, []),
        ];
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new SelectMethodDialog(methods));
        var page = new SelectMethodDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal(methods[0].ToString(), await page.MethodBox.PropertyAsync(AutomationPropertyNames.SelectedItem, Token)); // PAR-59
        Assert.True(await page.SelectButton.IsEnabledAsync(Token));

        await page.SelectButton.ClickAsync(Token); // batch X2b: DialogViewModel + DialogCloseBehavior
        Assert.True(closed);
        Assert.Equal(methods[0], dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ErrorDialogShowsCopyableMessage()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new ErrorDialog("Failed", "details\nline 2"));
        var page = new ErrorDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal("Failed", await page.TextAsync(Token));
        Assert.Equal("True", await page.Message.PropertyAsync(AutomationPropertyNames.IsReadOnly, Token));
        Assert.Equal("details\nline 2", await page.Message.TextAsync(Token));
        await page.OkButton.ClickAsync(Token);
        Assert.True(closed);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReferencesDialogListsAndCloses()
    {
        using var rig = new ReferencesRig();
        using var ui = HeadlessUi.Create();
        ui.Show(new ReferencesDialog { DataContext = rig.References });
        var page = new ReferencesDialogPage(ui.Driver);

        var rows = await page.RowNamesAsync(Token);
        Assert.Equal(4, rows.Count); // PAR-16
        int enabled = 0;
        foreach (string row in rows)
        {
            enabled += await page.IncludeSwitch(row).IsEnabledAsync(Token) ? 1 : 0;
        }

        Assert.Equal(1, enabled); // PAR-19
        Assert.Contains(rows, r => r.Contains("System.dll"));
        await page.CloseAsync(Token); // PAR-21
    }
}
