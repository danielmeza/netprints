using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-023, NPXE0005: the editor facet buffers with the extension, replays to the host's facet on success and is a Null Object without one.</summary>
public class EditorFacetTests
{
    private static CommandDescriptor Command(string id) => new(id, "Label", new NoopHandler());

    [Fact]
    public async Task AHostWithoutAnEditorIgnoresEveryCall()
    {
        var extension = InProcess("test.ext", builder => builder.Editor.AddCommand(Command("test.ext.a")).AddViewResources("avares://Test/Views.axaml"));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Empty(registry.Issues);
        Assert.DoesNotContain(registry.Results, result => result is ExtensionLoadResult.Failed);
    }

    [Fact]
    public async Task TheHostFacetReceivesTheCallsInOrderAfterRegisterReturns()
    {
        var recorder = new RecordingFacet();
        var extension = InProcess("test.ext", builder => builder.Editor.AddCommand(Command("test.ext.a")).AddViewResources("avares://Test/Views.axaml").AddCommand(Command("test.ext.b")));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]) with { EditorContributions = manifest => recorder.For(manifest.Id) });

        Assert.Equal(["test.ext:command:test.ext.a", "test.ext:view:avares://Test/Views.axaml", "test.ext:command:test.ext.b"], recorder.Calls);
    }

    [Fact]
    public async Task StatusBarItemsAndDialogsReachTheHostFacetInOrder()
    {
        var recorder = new RecordingFacet();
        var extension = InProcess("test.ext", builder => builder.Editor
            .AddStatusBarItem(new StatusBarItemDescriptor("test.ext.status.a", StatusBarAlignment.Right, 1, _ => new object()))
            .AddProjectTemplate(new ProjectTemplateDescriptor("test.ext.template.a", "A", "A", "p", ProjectOutputType.Library))
            .AddDialog(new DialogDescriptor("test.ext.dialog.a", "A", _ => new object())));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]) with { EditorContributions = manifest => recorder.For(manifest.Id) });

        Assert.Equal(["test.ext:status:test.ext.status.a", "test.ext:template:test.ext.template.a", "test.ext:dialog:test.ext.dialog.a"], recorder.Calls);
    }

    [Fact]
    public async Task AnExtensionWhoseRegisterThrowsContributesNothingToTheEditor()
    {
        var recorder = new RecordingFacet();
        var extension = InProcess("test.ext", builder =>
        {
            builder.Editor.AddCommand(Command("test.ext.a"));
            throw new InvalidOperationException("boom");
        });

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]) with { EditorContributions = manifest => recorder.For(manifest.Id) });

        Assert.Empty(recorder.Calls);
        Assert.Contains(registry.Results, result => result is ExtensionLoadResult.Failed);
    }

    [Fact]
    public async Task ACallAfterRegisterReturnedThrows()
    {
        IEditorContributions? kept = null;
        var extension = InProcess("test.ext", builder => kept = builder.Editor);

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.NotNull(kept);
        Assert.Throws<InvalidOperationException>(() => kept.AddCommand(Command("test.ext.late")));
    }

    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingFacet
    {
        public List<string> Calls { get; } = [];

        public IEditorContributions For(string extensionId) => new Facet(extensionId, Calls);

        private sealed class Facet(string extensionId, List<string> calls) : IEditorContributions
        {
            public IEditorContributions AddCommand(CommandDescriptor command) => Record("command:" + command.Id);

            public IEditorContributions AddPanel(PanelDescriptor panel) => Record("panel:" + panel.Id);

            public IEditorContributions AddDashboardTile(DashboardTileDescriptor tile) => Record("tile:" + tile.Id);

            public IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item) => Record("menu:" + item.Id);

            public IEditorContributions AddTooltipProvider(ITooltipProvider provider) => Record("tooltip");

            public IEditorContributions AddGoToProvider(IGoToProvider provider) => Record("goto");

            public IEditorContributions AddStatusBarItem(StatusBarItemDescriptor item) => Record("status:" + item.Id);

            public IEditorContributions AddProjectTemplate(ProjectTemplateDescriptor template) => Record("template:" + template.Id);

            public IEditorContributions AddDialog(DialogDescriptor dialog) => Record("dialog:" + dialog.Id);

            public IEditorContributions AddViewResources(string resourceUri) => Record("view:" + resourceUri);

            private Facet Record(string call)
            {
                calls.Add(extensionId + ":" + call);
                return this;
            }
        }
    }
}
