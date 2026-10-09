using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Icons;
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

    /// <summary>The methods a class deriving from <see cref="Exception"/> can override, nearest base type first (T092j).</summary>
    internal static MethodSpecifier[] ExceptionOverrides()
    {
        MethodSpecifier Method(Type declaring, string name, Type? returns, params (string Name, Type Type)[] parameters) =>
            new(name, [.. parameters.Select(p => new MethodParameter(p.Name, TypeSpecifier.FromType(p.Type), MethodParameterPassType.Default, false, null))],
                returns is null ? [] : [TypeSpecifier.FromType(returns)], MethodModifiers.Virtual, MemberVisibility.Public, TypeSpecifier.FromType(declaring), []);

        return
        [
            Method(typeof(Exception), "GetBaseException", typeof(Exception)),
            Method(typeof(Exception), "GetObjectData", null, ("info", typeof(System.Runtime.Serialization.SerializationInfo)), ("context", typeof(System.Runtime.Serialization.StreamingContext))),
            Method(typeof(Exception), "ToString", typeof(string)),
            Method(typeof(object), "Equals", typeof(bool), ("obj", typeof(object))),
            Method(typeof(object), "Finalize", null),
            Method(typeof(object), "GetHashCode", typeof(int)),
        ];
    }

    private static T Named<T>(Window window, string automationId) where T : Avalonia.Controls.Control =>
        window.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == automationId);

    private static double ResourceNumber(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? value) && value is double number ? number : double.NaN;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheOverrideDialogIsAShellDialogOfAboutSixHundredFortyByFourHundredEighty()
    {
        using var ui = HeadlessUi.Create();

        SelectMethodDialog dialog = ui.Show(new SelectMethodDialog(ExceptionOverrides()));

        DialogShell shell = Assert.Single(dialog.GetVisualDescendants().OfType<DialogShell>());
        Assert.Equal("Override method", dialog.Title);
        Assert.Equal("Override method", shell.Title);
        Assert.Equal(IconIds.CategoryMethod, shell.IconId);
        Assert.Equal(ResourceNumber("Dialog.MaxWidth"), dialog.ClientSize.Width);
        Assert.InRange(dialog.ClientSize.Height, 440, 520);
        Assert.Equal(["Exception", "Object"], Assert.IsType<SelectMethodDialogViewModel>(dialog.DataContext).List.Rows.Where(row => row.IsHeader).Select(row => row.Text));
        Assert.Equal(ResourceNumber("Dialog.ListMaxHeight"),Named<ListBox>(dialog, AutomationIds.MethodPickerRows).Bounds.Height);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TypingAFilterAndPressingEnterClosesWithThatMethodAndKeepsTheSize()
    {
        using var ui = HeadlessUi.Create();
        SelectMethodDialog dialog = ui.Show(new SelectMethodDialog(ExceptionOverrides()));
        Size size = dialog.ClientSize;
        double listHeight = Named<ListBox>(dialog, AutomationIds.MethodPickerRows).Bounds.Height;
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        await ui.Driver.TypeAsync("tostr", Token);
        Assert.Equal(size, dialog.ClientSize);
        Assert.Equal(listHeight, Named<ListBox>(dialog, AutomationIds.MethodPickerRows).Bounds.Height);
        await ui.Driver.PressAsync("Enter", Token);

        Assert.True(closed);
        Assert.Equal("Exception", dialog.Result?.DeclaringType.ShortName);
        Assert.Equal("ToString", dialog.Result?.Name);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnOverriddenMethodIsDimmedAndEnterDoesNotPickIt()
    {
        using var ui = HeadlessUi.Create();
        SelectMethodDialog dialog = ui.Show(new SelectMethodDialog(ExceptionOverrides(), new HashSet<string> { "Finalize" }));
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Grid dimmed = Assert.Single(dialog.GetVisualDescendants().OfType<Grid>(), grid => grid.Classes.Contains("dimmed"));
        Assert.True(dimmed.Opacity < 1);
        await ui.Driver.TypeAsync("finalize", Token);
        await ui.Driver.PressAsync("Enter", Token);

        Assert.False(closed);
        Assert.False(await new SelectMethodDialogPage(ui.Driver).SelectButton.IsEnabledAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EscapeClosesTheOverrideDialogWithNoMethod()
    {
        using var ui = HeadlessUi.Create();
        SelectMethodDialog dialog = ui.Show(new SelectMethodDialog(ExceptionOverrides()));
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        await ui.Driver.PressAsync("Esc", Token);

        Assert.True(closed);
        Assert.Null(dialog.Result);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOverrideButtonPicksTheSelectedMethod()
    {
        using var ui = HeadlessUi.Create();
        SelectMethodDialog dialog = ui.Show(new SelectMethodDialog(ExceptionOverrides()));
        var page = new SelectMethodDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.True(await page.SelectButton.IsEnabledAsync(Token));
        await page.SelectButton.ClickAsync(Token);

        Assert.True(closed);
        Assert.Equal("GetBaseException", dialog.Result?.Name);
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
