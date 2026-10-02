using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Inspectors;

/// <summary>
/// The inspector panel: hosts the class, method or variable inspector for the project tree's selection
/// (<see cref="ShellViewModel.TreeSelection"/>), and an empty state when nothing is selected or no project is open.
/// The inspectors come from the class's <see cref="ClassContext"/>, which the session owns.
/// </summary>
public sealed partial class InspectorPanelViewModel : ObservableObject, IShellPanelContent, IRecipient<SelectInspectorMessage>
{
    private readonly HashSet<ClassContext> watched = [];
    private PanelContext? context;

    /// <summary>Gets the view model of the inspector shown, or null for the empty state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial object? Content { get; private set; }

    /// <summary>Gets a value indicating whether nothing is inspected.</summary>
    public bool IsEmpty => Content is null;

    /// <summary>Gets the text of the empty state.</summary>
    public string EmptyMessage => "Select a class, method or variable to inspect it.";

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        this.context = context;
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

        Content = null;
        Unwatch();
        context = null;
    }

    /// <inheritdoc/>
    void IRecipient<SelectInspectorMessage>.Receive(SelectInspectorMessage message)
    {
        if (context is { } attached)
        {
            attached.Shell.TreeSelection = message.Target.Variable;
        }
    }

    private void OnInspectorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassInspectorViewModel.Name))
        {
            context?.Shell.NotifyModelRenamed();
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.Session):
                Content = null;
                Unwatch();
                break;
            case nameof(ShellViewModel.TreeSelection):
                Refresh();
                break;
        }
    }

    private void Refresh()
    {
        if (context is not { Shell: { Session: { } session } shell })
        {
            Content = null;
            return;
        }

        watched.RemoveWhere(classContext => classContext.IsDisposed);
        Content = InspectorOf(session, shell.TreeSelection);
    }

    private object? InspectorOf(ProjectSessionViewModel session, object? selection) => selection switch
    {
        ClassGraph cls => Watch(session.ContextFor(cls)).ClassInspector,
        MethodGraph { Class: { } owner } method => Watch(session.ContextFor(owner)).Methods.FirstOrDefault(item => ReferenceEquals(item.Graph, method)),
        ConstructorGraph { Class: { } owner } constructor => Watch(session.ContextFor(owner)).Constructors.FirstOrDefault(item => ReferenceEquals(item.Graph, constructor)),
        Variable { Class: { } owner } variable => Watch(session.ContextFor(owner)).Variables.FirstOrDefault(item => ReferenceEquals(item.Variable, variable)),
        _ => null,
    };

    // Routes the context's inspector selections and class renames to the shell, once per context.
    private ClassContext Watch(ClassContext classContext)
    {
        if (watched.Add(classContext))
        {
            classContext.Messenger.Register<SelectInspectorMessage>(this);
            classContext.ClassInspector.PropertyChanged += OnInspectorChanged;
        }

        return classContext;
    }

    private void Unwatch()
    {
        foreach (ClassContext classContext in watched)
        {
            classContext.ClassInspector.PropertyChanged -= OnInspectorChanged;
            classContext.Messenger.UnregisterAll(this);
        }

        watched.Clear();
    }
}
