using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Editor.Shell;

/// <summary>
/// A panel that shows one item per class of the open project and picks the one of the active document's class. Every class
/// has its item from the moment the project opens, so what the item follows (analysis snapshots) is not missed while its class is not active.
/// </summary>
/// <typeparam name="TItem">The per-class view model; disposed when its class or the project goes, if it is <see cref="IDisposable"/>.</typeparam>
public abstract partial class ActiveClassPanelViewModel<TItem> : ObservableObject, IShellPanelContent
    where TItem : class
{
    private readonly Dictionary<ClassGraph, TItem> items = [];
    private ProjectSessionViewModel? followedSession;

    /// <summary>Gets the item of the active document's class, or null when there is none.</summary>
    [ObservableProperty]
    public partial TItem? Current { get; private set; }

    /// <summary>Gets the shell services, or null while the panel is not attached.</summary>
    protected PanelContext? Context { get; private set; }

    /// <inheritdoc/>
    public void Attach(PanelContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        context.Shell.PropertyChanged += OnShellChanged;
        OnAttached(context);
        Refresh();
    }

    /// <inheritdoc/>
    public void Detach()
    {
        if (Context is { } attached)
        {
            attached.Shell.PropertyChanged -= OnShellChanged;
        }

        Follow(null);
        Current = null;
        OnDetached();
        Context = null;
    }

    /// <summary>Creates the item of a class; it is disposed when the class or the project goes.</summary>
    /// <param name="cls">A class of the open project.</param>
    /// <param name="context">The shell services.</param>
    /// <returns>The item.</returns>
    protected abstract TItem CreateItem(ClassGraph cls, PanelContext context);

    /// <summary>Called once from <see cref="Attach"/>, before the first item is created.</summary>
    /// <param name="context">The shell services.</param>
    protected virtual void OnAttached(PanelContext context)
    {
    }

    /// <summary>Called once from <see cref="Detach"/>, after the items were disposed.</summary>
    protected virtual void OnDetached()
    {
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.Session) or nameof(ShellViewModel.ActiveDocument))
        {
            Refresh();
        }
    }

    private void OnClassesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (Context is not { } attached)
        {
            return;
        }

        Follow(attached.Shell.Session);
        if (followedSession is { } session)
        {
            foreach (ClassGraph removed in items.Keys.Where(cls => !session.Project.Classes.Contains(cls)).ToList())
            {
                Release(removed);
            }

            foreach (ClassGraph cls in session.Project.Classes.Where(cls => !items.ContainsKey(cls)))
            {
                items[cls] = CreateItem(cls, attached);
            }
        }

        ClassGraph? active = attached.Shell.ActiveDocument is { Id.ClassPath: { } classPath } ? followedSession?.FindClass(classPath) : null;
        Current = active is null ? null : items.GetValueOrDefault(active);
    }

    private void Follow(ProjectSessionViewModel? session)
    {
        if (ReferenceEquals(session, followedSession))
        {
            return;
        }

        if (followedSession is not null)
        {
            followedSession.Project.Classes.CollectionChanged -= OnClassesChanged;
        }

        foreach (ClassGraph cls in items.Keys.ToList())
        {
            Release(cls);
        }

        followedSession = session;
        if (session is not null)
        {
            session.Project.Classes.CollectionChanged += OnClassesChanged;
        }
    }

    private void Release(ClassGraph cls)
    {
        if (items.Remove(cls, out TItem? item))
        {
            if (ReferenceEquals(item, Current))
            {
                Current = null;
            }

            (item as IDisposable)?.Dispose();
        }
    }
}
