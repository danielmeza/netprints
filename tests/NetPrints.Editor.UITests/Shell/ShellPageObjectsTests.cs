using Avalonia.Headless.XUnit;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Testing.Ui.Screenplay;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The shell page objects find panels by panel id, tabs by document id and commands by command id.</summary>
public class ShellPageObjectsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheTreeOpensAMethodAndItsTabAndContentAreFoundByDocumentId()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        var shell = app.Actor.Using<UseNetPrints>().Shell;
        var cls = app.Session.Project.Classes.Single();
        var document = DocumentId.Graph(app.Session.ClassPathOf(cls), DocumentId.MethodKeyPrefix + cls.Methods.First().Id);

        await shell.Tree.OpenMethodAsync("Main", Token);

        await shell.Tabs.WaitOpenAsync(document, Token);
        Assert.True(await shell.Tabs.IsSelectedAsync(document, Token));
        await shell.Tabs.Content(document).WaitVisibleAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheInspectorShowsTheClassInspectorOfTheSelectedTreeRow()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        var shell = app.Actor.Using<UseNetPrints>().Shell;
        Assert.True(await shell.Inspector.Empty.IsVisibleAsync(Token));

        await shell.Tree.SelectAsync(shell.Tree.Class("Program"), Token);

        await shell.Inspector.ClassName.WaitVisibleAsync(Token);
        await shell.Inspector.Empty.WaitHiddenAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheBottomPanelsAreShownByPanelId()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        var bottom = app.Actor.Using<UseNetPrints>().Shell.Bottom;

        await bottom.ShowAsync(PanelContributions.OutputId, Token);
        await bottom.OutputList.WaitVisibleAsync(Token);
        await bottom.ShowAsync(PanelContributions.ErrorsId, Token);
        await bottom.ErrorsList.WaitVisibleAsync(Token);
        await bottom.OutputList.WaitHiddenAsync(Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheMenuBarAndTheCommandBarFindCommandsByCommandId()
    {
        await using ShellApp app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        var shell = app.Actor.Using<UseNetPrints>().Shell;

        Assert.True(await shell.Commands.Button(ShellCommands.Compile).IsEnabledAsync(Token));
        var item = await shell.Menu.OpenAsync("Build", ShellCommands.Compile, Token);
        Assert.True(await item.IsEnabledAsync(Token));
    }
}
