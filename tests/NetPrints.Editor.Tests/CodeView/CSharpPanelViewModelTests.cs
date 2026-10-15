using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Diagnostics;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Translator;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.CodeView;

public sealed class CSharpPanelViewModelTests : IAsyncDisposable
{
    private readonly PushableCodeAnalysisHost host = new();
    private readonly ShellPanelRig rig;

    public CSharpPanelViewModelTests() => rig = new ShellPanelRig(context => context with { CodeAnalysis = host });

    public async ValueTask DisposeAsync()
    {
        await rig.DisposeAsync();
        host.Dispose();
    }

    private static DocumentId ClassDocument(ProjectSessionViewModel session, ClassGraph cls) =>
        DocumentId.Graph(session.ClassPathOf(cls), DocumentId.ClassGraphKey);

    private static void Activate(ShellViewModel shell, DocumentId id) =>
        shell.ActiveDocument = shell.AddDocument(new TestDocumentViewModel(id, "doc"));

    private void PushCode(params ClassGraph[] classes) =>
        host.Push(new CodeAnalysisSnapshot(
            classes.ToDictionary(cls => cls.FullName, cls => new TranslatedClass(cls.FullName, "// code of " + cls.Name, SourceMap.Empty), StringComparer.Ordinal), []));

    [Fact]
    public async Task ThePanelFollowsTheActiveDocumentsClassAndHasTheCodeThatArrivedBeforeItWasActive()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph first = session.Project.Classes[0];
        var second = new ClassGraph { Name = "Other", Namespace = first.Namespace };
        session.Project.Classes.Add(second);
        PushCode(first, second);
        Assert.Null(rig.CSharp.Current);

        Activate(rig.Shell, ClassDocument(session, first));
        Assert.Equal("// code of " + first.Name, rig.CSharp.Current?.Code);

        Activate(rig.Shell, ClassDocument(session, second));
        Assert.Equal("// code of Other", rig.CSharp.Current?.Code);

        rig.Shell.ActiveDocument = null;
        Assert.Null(rig.CSharp.Current);
    }

    [Fact]
    public async Task ANewSnapshotUpdatesTheShownCodeAndClosingTheProjectEmptiesThePanel()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        Activate(rig.Shell, ClassDocument(session, cls));
        PushCode(cls);
        Assert.Equal("// code of " + cls.Name, rig.CSharp.Current?.Code);

        host.Push(new CodeAnalysisSnapshot(
            new Dictionary<string, TranslatedClass>(StringComparer.Ordinal) { [cls.FullName] = new TranslatedClass(cls.FullName, "// changed", SourceMap.Empty) }, []));
        Assert.Equal("// changed", rig.CSharp.Current?.Code);

        rig.Shell.Session = null;
        Assert.Null(rig.CSharp.Current);
    }

    [Fact]
    public async Task AClassAddedAfterTheProjectOpenedIsFollowedToo()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        var added = new ClassGraph { Name = "Added", Namespace = session.Project.Classes[0].Namespace };
        session.Project.Classes.Add(added);
        PushCode(added);

        Activate(rig.Shell, ClassDocument(session, added));

        Assert.Equal("// code of Added", rig.CSharp.Current?.Code);
    }
}
