using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.ErrorList;

/// <summary>
/// The Errors panel: the diagnostics of every class of the open project, each row labelled with its class.
/// Activating a row opens, or activates, its graph's tab and selects the node, whether or not its class has a tab open.
/// </summary>
public sealed partial class ErrorsPanelViewModel : ObservableObject, IShellPanelContent, IRecipient<NavigateToNodeMessage>
{
    private PanelContext? context;
    private IMessenger? messenger;

    /// <summary>Gets the project's error list, or null while no project is open.</summary>
    [ObservableProperty]
    public partial ErrorListViewModel? Current { get; private set; }

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
        messenger = context.Context.CreateMessenger();
        messenger.Register<NavigateToNodeMessage>(this);
        context.Shell.PropertyChanged += OnShellChanged;
        Refresh();
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (context is { } attached)
        {
            attached.Shell.PropertyChanged -= OnShellChanged;
        }

        messenger?.UnregisterAll(this);
        messenger = null;
        Release();
        context = null;
    }

    /// <inheritdoc/>
    void IRecipient<NavigateToNodeMessage>.Receive(NavigateToNodeMessage message)
    {
        if (context is not { Shell: { Session: { } session } shell } attached)
        {
            return;
        }

        ClassGraph? cls = message.ClassFullName is { } name
            ? session.Project.Classes.FirstOrDefault(candidate => string.Equals(candidate.FullName, name, StringComparison.Ordinal))
            : session.FindClass(shell.ActiveDocument?.Id.ClassPath ?? string.Empty);
        if (cls is null
            || GraphKeys.Resolve(cls, message.GraphKey) is not { } graph
            || CommandTargets.GraphDocumentOf(session, graph) is not { } id)
        {
            return;
        }

        attached.Api.OpenDocument(id);
        if (message.NodeId is { } nodeId && shell.FindDocument(id) is GraphDocumentViewModel document)
        {
            document.Graph.RevealNode(nodeId);
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Session))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        Current?.Dispose();
        Current = context is { Shell.Session: { } session } attached && messenger is { } sender
            ? new ErrorListViewModel(session.Project, attached.Context.CodeAnalysis, sender)
            : null;
    }

    private void Release()
    {
        Current?.Dispose();
        Current = null;
    }
}
