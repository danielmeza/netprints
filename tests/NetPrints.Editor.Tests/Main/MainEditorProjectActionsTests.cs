using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Main;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Main;

public sealed class MainEditorProjectActionsTests : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];
    private readonly List<MainEditorViewModel> models = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        models.ForEach(model => model.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private async Task<(MainEditorViewModel Model, IProjectActions Actions, ClassGraph Class)> OpenAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var model = new MainEditorViewModel(editor.Context);
        models.Add(model);
        await model.LoadProjectAsync(path);
        return (model, model, Assert.Single(Assert.IsType<Project>(model.Project).Classes));
    }

    [Fact]
    public async Task OpenProjectWithAPathLoadsIt()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var model = new MainEditorViewModel(editor.Context);
        models.Add(model);

        await ((IProjectActions)model).OpenProjectAsync(path, Token);

        Assert.Equal(path, model.Project?.Path);
        Assert.NotNull(model.Session);
    }

    [Fact]
    public async Task OpenProjectWithoutAPathAsksTheFilePicker()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        editor.FilePicker.OpenFileAnswers.Enqueue(path);
        var model = new MainEditorViewModel(editor.Context);
        models.Add(model);

        await ((IProjectActions)model).OpenProjectAsync(null, Token);

        Assert.Equal(path, model.Project?.Path);
    }

    [Fact]
    public async Task CloseProjectClosesTheEditorsAndTheSession()
    {
        (MainEditorViewModel model, IProjectActions actions, _) = await OpenAsync();
        int closedBefore = editor.Windows.CloseAllCount;

        await actions.CloseProjectAsync(Token);

        Assert.Null(model.Project);
        Assert.Null(model.Session);
        Assert.Equal(closedBefore + 1, editor.Windows.CloseAllCount);
    }

    [Fact]
    public async Task ExitClosesTheMainWindow()
    {
        (_, IProjectActions actions, _) = await OpenAsync();

        await actions.ExitAsync(Token);

        Assert.Equal(1, editor.Windows.CloseMainWindowCount);
    }

    [Fact]
    public async Task UnloadIsAlwaysConfirmedUntilTheDirtyPromptExists()
    {
        (_, IProjectActions actions, _) = await OpenAsync();

        Assert.True(await actions.ConfirmUnloadAsync(Token));
    }

    [Fact]
    public async Task ProjectSettingsOpensTheSettingsPane()
    {
        (MainEditorViewModel model, IProjectActions actions, _) = await OpenAsync();

        actions.ShowProjectSettings();

        Assert.True(model.IsSettingsPaneOpen);
    }

    [Fact]
    public async Task ReferencesShowsTheDialog()
    {
        (_, IProjectActions actions, _) = await OpenAsync();

        await actions.ShowReferencesAsync(Token);

        Assert.Single(editor.Dialogs.ReferenceDialogs);
    }

    [Fact]
    public async Task AddMethodAddsAndOpensTheClassEditor()
    {
        (_, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        int methods = cls.Methods.Count;

        actions.AddMethod(cls);

        Assert.Equal(methods + 1, cls.Methods.Count);
        Assert.Contains(cls, editor.Windows.Open.Keys);
    }

    [Fact]
    public async Task AddConstructorVariableAndEventGraphGrowTheClass()
    {
        (_, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        int constructors = cls.Constructors.Count;
        int variables = cls.Variables.Count;
        int eventGraphs = cls.EventGraphs.Count;

        actions.AddConstructor(cls);
        actions.AddVariable(cls);
        actions.AddEventGraph(cls);

        Assert.Equal(constructors + 1, cls.Constructors.Count);
        Assert.Equal(variables + 1, cls.Variables.Count);
        Assert.Equal(eventGraphs + 1, cls.EventGraphs.Count);
    }

    [Fact]
    public async Task ClassSettingsShowsTheClassInspector()
    {
        (_, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        actions.AddVariable(cls);
        ClassEditorViewModel classEditor = Assert.IsType<ClassEditorViewModel>(editor.Windows.FindClassEditor(cls));
        classEditor.Inspector = InspectorKind.Method;

        actions.ShowClassSettings(cls);

        Assert.Equal(InspectorKind.Class, classEditor.Inspector);
    }

    [Fact]
    public async Task DeleteItemRemovesAClassAndItsWindow()
    {
        (MainEditorViewModel model, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        actions.AddVariable(cls);

        actions.DeleteItem(cls);

        Assert.DoesNotContain(cls, model.Classes);
        Assert.Contains(cls, editor.Windows.Closed);
    }

    [Fact]
    public async Task DeleteItemRemovesAMethodThroughTheUndoStack()
    {
        (_, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        actions.AddMethod(cls);
        MethodGraph method = cls.Methods.Last();

        actions.DeleteItem(method);

        Assert.DoesNotContain(method, cls.Methods);
    }

    [Fact]
    public async Task RenameItemRevealsTheClassSettings()
    {
        (_, IProjectActions actions, ClassGraph cls) = await OpenAsync();
        actions.AddVariable(cls);
        ClassEditorViewModel classEditor = Assert.IsType<ClassEditorViewModel>(editor.Windows.FindClassEditor(cls));
        classEditor.Inspector = InspectorKind.Method;

        actions.RenameItem(cls);

        Assert.Equal(InspectorKind.Class, classEditor.Inspector);
    }
}
