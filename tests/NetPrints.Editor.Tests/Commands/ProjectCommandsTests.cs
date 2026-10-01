using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Commands;

public sealed class ProjectCommandsTests : SessionCommandTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<(CommandContext Context, ClassGraph Class)> OpenContextAsync(bool treeSelection = false)
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        CommandContext context = treeSelection
            ? Shell.Context(session: session, selection: new CommandSelection([], cls))
            : Shell.Context(session: session);
        return (context, cls);
    }

    public static TheoryData<ICommandHandler, string> UnloadingHandlers() => new()
    {
        { new OpenProjectCommandHandler(), "OpenProject:" },
        { new NewProjectCommandHandler(), "NewProject" },
        { new CloseProjectCommandHandler(), "CloseProject" },
        { new ExitCommandHandler(), "Exit" },
    };

    public static TheoryData<ICommandHandler, string> NeedsAProject() => new()
    {
        { new CloseProjectCommandHandler(), "CloseProject" },
        { new ProjectSettingsCommandHandler(), "ShowProjectSettings" },
        { new ReferencesCommandHandler(), "ShowReferences" },
    };

    public static TheoryData<ICommandHandler, string> NeedsAClass() => new()
    {
        { new ClassSettingsCommandHandler(), "ShowClassSettings" },
        { new AddMethodCommandHandler(), "AddMethod" },
        { new AddConstructorCommandHandler(), "AddConstructor" },
        { new AddVariableCommandHandler(), "AddVariable" },
        { new AddEventGraphCommandHandler(), "AddEventGraph" },
    };

    [Theory]
    [MemberData(nameof(NeedsAProject))]
    [MemberData(nameof(NeedsAClass))]
    public void NothingRunsWithoutAProject(ICommandHandler handler, string call)
    {
        Assert.False(handler.CanExecute(Shell.Context()), call);
        Assert.False(handler.CanExecute(Shell.Context(selection: new CommandSelection([], new ClassGraph { Name = "C", Namespace = "N" }))));
    }

    public static TheoryData<ICommandHandler> StartPageHandlers() =>
        [new OpenProjectCommandHandler(), new NewProjectCommandHandler(), new ExitCommandHandler()];

    [Theory]
    [MemberData(nameof(StartPageHandlers))]
    public void StartPageCommandsRunWithoutAProject(ICommandHandler handler) =>
        Assert.True(handler.CanExecute(Shell.Context()));

    [Theory]
    [MemberData(nameof(UnloadingHandlers))]
    public async Task UnloadingCommandsAskBeforeUnloadingAnOpenProject(ICommandHandler handler, string call)
    {
        (CommandContext context, _) = await OpenContextAsync();

        await handler.ExecuteAsync(context, Token);

        Assert.Equal(["ConfirmUnload", call], Shell.Project.Calls);
    }

    [Theory]
    [MemberData(nameof(UnloadingHandlers))]
    public async Task DecliningTheUnloadPromptKeepsTheProject(ICommandHandler handler, string call)
    {
        (CommandContext context, _) = await OpenContextAsync();
        Shell.Project.AllowUnload = false;

        await handler.ExecuteAsync(context, Token);

        Assert.Equal(["ConfirmUnload"], Shell.Project.Calls);
        Assert.DoesNotContain(call, Shell.Project.Calls);
    }

    [Theory]
    [MemberData(nameof(UnloadingHandlers))]
    public async Task NoPromptWithoutAnOpenProject(ICommandHandler handler, string call)
    {
        if (handler is CloseProjectCommandHandler)
        {
            return;
        }

        await handler.ExecuteAsync(Shell.Context(), Token);

        Assert.Equal([call], Shell.Project.Calls);
    }

    [Fact]
    public async Task OpenProjectPassesTheParameterAsThePath()
    {
        await new OpenProjectCommandHandler().ExecuteAsync(Shell.Context(parameter: "/work/App.csproj"), Token);

        Assert.Equal(["OpenProject:/work/App.csproj"], Shell.Project.Calls);
    }

    [Theory]
    [MemberData(nameof(NeedsAProject))]
    public async Task ProjectCommandsRunWithAnOpenProject(ICommandHandler handler, string call)
    {
        (CommandContext context, _) = await OpenContextAsync();

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, Token);

        Assert.Contains(call, Shell.Project.Calls);
    }

    [Theory]
    [MemberData(nameof(NeedsAClass))]
    public async Task ClassCommandsActOnTheTreeSelectionsClass(ICommandHandler handler, string call)
    {
        (CommandContext context, ClassGraph cls) = await OpenContextAsync(treeSelection: true);

        Assert.True(handler.CanExecute(context));
        await handler.ExecuteAsync(context, Token);

        Assert.Equal([call], Shell.Project.Calls);
        Assert.Same(cls, Shell.Project.LastClass);
    }

    [Theory]
    [MemberData(nameof(NeedsAClass))]
    public async Task ClassCommandsFallBackToTheActiveDocumentsClass(ICommandHandler handler, string call)
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph cls = session.Project.Classes.Single();
        Shell.OpenDocument(DocumentId.Graph(session.ClassPathOf(cls), "class"));

        await handler.ExecuteAsync(Shell.Context(session: session), Token);

        Assert.Equal([call], Shell.Project.Calls);
        Assert.Same(cls, Shell.Project.LastClass);
    }

    [Fact]
    public async Task ClassCommandsPreferTheTreeSelectionOverTheActiveDocument()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ClassGraph active = session.Project.Classes.Single();
        ClassGraph selected = session.Project.CreateNewClass(DefaultProjectProfile.Instance);
        Shell.OpenDocument(DocumentId.Graph(session.ClassPathOf(active), "class"));

        await new AddMethodCommandHandler().ExecuteAsync(Shell.Context(session: session, selection: new CommandSelection([], selected)), Token);

        Assert.Same(selected, Shell.Project.LastClass);
    }
}
