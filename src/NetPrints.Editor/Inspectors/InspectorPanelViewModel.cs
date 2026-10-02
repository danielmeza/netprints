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
/// Each inspected class gets one <see cref="ClassEditorViewModel"/>, which owns the view models of its members.
/// </summary>
public sealed partial class InspectorPanelViewModel : ObservableObject, IShellPanelContent, IRecipient<SelectInspectorMessage>
{
    private readonly Dictionary<ClassGraph, ClassEditorViewModel> editors = [];
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
        ReleaseEditors();
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

    private void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ClassEditorViewModel.Name))
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
                ReleaseEditors();
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

        foreach (ClassGraph removed in editors.Keys.Where(cls => !session.Project.Classes.Contains(cls)).ToList())
        {
            ReleaseEditor(removed);
        }

        Content = InspectorOf(shell.TreeSelection);
    }

    private object? InspectorOf(object? selection) => selection switch
    {
        ClassGraph cls => EditorFor(cls),
        MethodGraph { Class: { } owner } method => EditorFor(owner).Methods.FirstOrDefault(item => ReferenceEquals(item.Graph, method)),
        ConstructorGraph { Class: { } owner } constructor => EditorFor(owner).Constructors.FirstOrDefault(item => ReferenceEquals(item.Graph, constructor)),
        Variable { Class: { } owner } variable => EditorFor(owner).Variables.FirstOrDefault(item => ReferenceEquals(item.Variable, variable)),
        _ => null,
    };

    /// <summary>Gets the editor of a class, creating it on first use; graph documents of the class are built on its services, so they share the undo stack the Undo command reads.</summary>
    /// <param name="cls">A class of the open project.</param>
    /// <returns>The class's one editor.</returns>
    /// <exception cref="InvalidOperationException">The panel is not attached to a shell.</exception>
    public ClassEditorViewModel EditorFor(ClassGraph cls)
    {
        if (!editors.ContainsKey(cls))
        {
            AddEditor(cls);
        }

        return editors[cls];
    }

    private void AddEditor(ClassGraph cls)
    {
        PanelContext attached = context ?? throw new InvalidOperationException("The inspector panel is not attached to a shell.");
        var editor = new ClassEditorViewModel(cls, attached.Context) { SessionSource = () => attached.Shell.Session };
        attached.Shell.Session?.UseUndoStack(cls, editor.UndoRedo);
        editor.Messenger.Register<SelectInspectorMessage>(this);
        editor.PropertyChanged += OnEditorChanged;
        editors[cls] = editor;
    }

    private void ReleaseEditors()
    {
        foreach (ClassGraph cls in editors.Keys.ToList())
        {
            ReleaseEditor(cls);
        }
    }

    private void ReleaseEditor(ClassGraph cls)
    {
        if (editors.Remove(cls, out ClassEditorViewModel? editor))
        {
            editor.PropertyChanged -= OnEditorChanged;
            editor.Messenger.UnregisterAll(this);
            editor.Dispose();
        }
    }
}
