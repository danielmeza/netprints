using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Events;

/// <summary>
/// The inspector of an event entry (US8, contracts/shell.md §7): its name, kind and arguments. A custom entry's name and arguments
/// are edited, each change one undo step; an override's name and arguments come from the base method and are read-only.
/// </summary>
public sealed partial class EventEntryInspectorViewModel : ObservableObject, IDisposable
{
    private readonly EventEntryNode entry;
    private readonly ClassEditorServices services;
    private readonly Action<EventEntryNode, string> rename;
    private readonly Func<IReadOnlyList<ClassGraph>> projectClasses;

    /// <summary>Wraps <paramref name="entry"/>.</summary>
    /// <param name="entry">The event entry.</param>
    /// <param name="services">Narrow services shared with the owning class editor (FR-038).</param>
    /// <param name="rename">Renames the entry and the calls to it as one undo step.</param>
    /// <param name="projectClasses">The classes whose graphs may call the event; their calls follow argument edits.</param>
    public EventEntryInspectorViewModel(EventEntryNode entry, ClassEditorServices services, Action<EventEntryNode, string> rename, Func<IReadOnlyList<ClassGraph>> projectClasses)
    {
        this.entry = entry;
        this.services = services;
        this.rename = rename;
        this.projectClasses = projectClasses;
        Arguments = new ObservableCollection<EventArgumentViewModel>();
        services.UndoRedo.Applied += OnUndoApplied;
        Refresh();
    }

    /// <summary>Gets the wrapped entry.</summary>
    public EventEntryNode Entry => entry;

    /// <summary>Gets the arguments, in order.</summary>
    public ObservableCollection<EventArgumentViewModel> Arguments { get; }

    /// <summary>Gets a value indicating whether the entry overrides a base method.</summary>
    public bool IsOverride => entry.OverriddenMethod is not null;

    /// <summary>Gets a value indicating whether the name and arguments can be edited.</summary>
    public bool IsEditable => !IsOverride;

    /// <summary>Gets a value indicating whether the name is read-only.</summary>
    public bool IsNameReadOnly => IsOverride;

    /// <summary>Gets the kind of entry: a custom event or an override.</summary>
    public string Kind => IsOverride ? "Override" : "Custom event";

    /// <summary>Gets the note on where an override's signature comes from, or null for a custom event.</summary>
    public string? BaseSignatureNote => entry.OverriddenMethod is { } method ? $"Signature comes from {method.DeclaringType.ShortName}.{method.Name}" : null;

    /// <summary>Gets a value indicating whether the entry has no arguments.</summary>
    public bool HasNoArguments => Arguments.Count == 0;

    /// <summary>Gets the reason the last edit was refused, or null when it was accepted.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; private set; }

    /// <summary>Gets a value indicating whether the last edit was refused.</summary>
    public bool HasError => Error is not null;

    /// <summary>Gets or sets the event's name; a refused name is shown in <see cref="Error"/> and the name stays.</summary>
    public string Name
    {
        get => entry.EventName;
        set
        {
            if (IsEditable && value != entry.EventName)
            {
                Error = null;
                if (!SyntaxFacts.IsValidIdentifier(value) || SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None)
                {
                    Error = $"'{value}' is not a valid C# identifier";
                }
                else
                {
                    try
                    {
                        rename(entry, value);
                    }
                    catch (ArgumentException refused)
                    {
                        Error = RefusalMessage.Of(refused);
                    }
                }

                Refresh();
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Re-reads the entry after an edit, an undo or a redo.</summary>
    public void Refresh()
    {
        IReadOnlyList<EventArgument> arguments = entry.Arguments;
        while (Arguments.Count > arguments.Count)
        {
            Arguments.RemoveAt(Arguments.Count - 1);
        }

        for (int i = 0; i < arguments.Count; i++)
        {
            bool isLast = i == arguments.Count - 1;
            if (i < Arguments.Count)
            {
                Arguments[i].Update(arguments[i], isLast);
            }
            else
            {
                Arguments.Add(new EventArgumentViewModel(this, i, arguments[i], IsEditable, isLast));
            }
        }

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HasNoArguments));
        AddArgumentCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Stops following the class's undo stack.</summary>
    public void Dispose() => services.UndoRedo.Applied -= OnUndoApplied;

    internal void RenameArgument(int index, string name)
    {
        List<EventArgument> arguments = [.. entry.DeclaredArguments];
        if (arguments[index].Name != name)
        {
            arguments[index] = arguments[index] with { Name = name };
            Apply("Rename argument", arguments, Identity(arguments.Count));
        }
    }

    internal void RemoveArgument(int index)
    {
        List<EventArgument> arguments = [.. entry.DeclaredArguments];
        arguments.RemoveAt(index);
        Apply("Remove argument", arguments, [.. Identity(arguments.Count + 1).Where(source => source != index)]);
    }

    internal void MoveArgument(int index, int offset)
    {
        List<EventArgument> arguments = [.. entry.DeclaredArguments];
        int target = index + offset;
        List<int> sources = Identity(arguments.Count);
        (arguments[index], arguments[target]) = (arguments[target], arguments[index]);
        (sources[index], sources[target]) = (sources[target], sources[index]);
        Apply(offset < 0 ? "Move argument up" : "Move argument down", arguments, sources);
    }

    internal async Task ChangeArgumentTypeAsync(int index)
    {
        EventArgument current = entry.DeclaredArguments[index];
        TypeSpecifier? chosen = await services.Context.Dialogs.SelectTypeAsync(services.Context.Reflection.NonStaticTypes, current.Type);
        if (chosen is not null && chosen != current.Type)
        {
            List<EventArgument> arguments = [.. entry.DeclaredArguments];
            arguments[index] = current with { Type = chosen };
            Apply("Change argument type", arguments, Identity(arguments.Count));
        }
    }

    [RelayCommand(CanExecute = nameof(IsEditable))]
    private void AddArgument()
    {
        List<EventArgument> arguments = [.. entry.DeclaredArguments];
        string name = NetPrintsUtil.GetUniqueName("arg", [.. arguments.Select(argument => argument.Name)]);
        List<int> sources = Identity(arguments.Count);
        arguments.Add(new EventArgument(name, TypeSpecifier.FromType<object>()));
        sources.Add(-1);
        Apply("Add argument", arguments, sources);
    }

    private static List<int> Identity(int count) => [.. Enumerable.Range(0, count)];

    private void Apply(string label, IReadOnlyList<EventArgument> arguments, IReadOnlyList<int> sources)
    {
        Error = null;
        try
        {
            services.UndoRedo.Do(EditorCommands.SetEventArguments(projectClasses(), entry, label, arguments, sources));
        }
        catch (ArgumentException refused)
        {
            Error = RefusalMessage.Of(refused);
        }

        Refresh();
    }

    private void OnUndoApplied(object? sender, EventArgs e) => Refresh();
}
