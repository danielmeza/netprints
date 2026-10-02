using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ErrorList;

/// <summary>
/// The Errors panel: the diagnostics of the active document's class (<see cref="ActiveClassPanelViewModel{TItem}.Current"/>).
/// Activating a row opens, or activates, its graph's tab and selects the node.
/// </summary>
public sealed class ErrorsPanelViewModel : ActiveClassPanelViewModel<ErrorListViewModel>, IRecipient<NavigateToNodeMessage>
{
    private IMessenger? messenger;

    /// <inheritdoc/>
    void IRecipient<NavigateToNodeMessage>.Receive(NavigateToNodeMessage message)
    {
        if (Context is not { Shell: { Session: { } session } shell } context || Current is null)
        {
            return;
        }

        if (session.FindClass(shell.ActiveDocument?.Id.ClassPath ?? string.Empty) is not { } cls
            || GraphKeys.Resolve(cls, message.GraphKey) is not { } graph
            || CommandTargets.GraphDocumentOf(session, graph) is not { } id)
        {
            return;
        }

        context.Api.OpenDocument(id);
        if (message.NodeId is { } nodeId && shell.FindDocument(id) is GraphDocumentViewModel document)
        {
            document.Graph.RevealNode(nodeId);
        }
    }

    /// <inheritdoc/>
    protected override ErrorListViewModel CreateItem(ClassGraph cls, PanelContext context) =>
        new(cls, context.Context.CodeAnalysis, messenger ?? throw new InvalidOperationException("The errors panel is not attached to a shell."));

    /// <inheritdoc/>
    protected override void OnAttached(PanelContext context)
    {
        messenger = context.Context.CreateMessenger();
        messenger.Register<NavigateToNodeMessage>(this);
    }

    /// <inheritdoc/>
    protected override void OnDetached()
    {
        messenger?.UnregisterAll(this);
        messenger = null;
    }
}
