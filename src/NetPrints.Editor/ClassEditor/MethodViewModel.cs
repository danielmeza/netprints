using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.Inspectors;

namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// A method or constructor in the class editor lists and the method inspector (PAR-24, 27, 35).
/// </summary>
/// <remarks>
/// The WPF editor used a full graph view model per list entry; a light wrapper avoids building
/// node view models for graphs that are not open.
/// </remarks>
public sealed partial class MethodViewModel : ObservableObject, IDisposable
{
    private readonly Action<MethodGraph, string>? rename;

    /// <summary>
    /// Wraps <paramref name="graph"/> and subscribes to its property-changed event.
    /// </summary>
    /// <param name="graph">Method or constructor graph to wrap.</param>
    /// <param name="rename">Renames a method together with the calls to it; without it the name is set on the graph alone.</param>
    public MethodViewModel(ExecutionGraph graph, Action<MethodGraph, string>? rename = null)
    {
        Graph = graph;
        this.rename = rename;
        ((INotifyPropertyChanged)graph).PropertyChanged += OnGraphPropertyChanged;
    }

    /// <summary>The wrapped model graph.</summary>
    public ExecutionGraph Graph { get; }

    /// <summary>Whether the wrapped graph is a <see cref="ConstructorGraph"/>.</summary>
    public bool IsConstructor => Graph is ConstructorGraph;

    /// <summary>Name; read-only for constructors.</summary>
    public string Name
    {
        get => Graph is MethodGraph method ? method.Name : Graph.ToString() ?? "";
        set
        {
            if (Graph is MethodGraph method && method.Name != value)
            {
                if (rename is null)
                {
                    method.Name = value;
                }
                else
                {
                    rename(method, value);
                }
            }
        }
    }

    /// <summary>The graph's visibility.</summary>
    public MemberVisibility Visibility
    {
        get => Graph.Visibility;
        set => Graph.Visibility = value;
    }

    /// <summary>The visibility values offered by the method's visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => VisibilityChoices.All;

    /// <summary>The graph's modifiers, or <see cref="MethodModifiers.None"/> for a constructor (which has none).</summary>
    public MethodModifiers Modifiers
    {
        get => Graph is MethodGraph method ? method.Modifiers : MethodModifiers.None;
        set
        {
            if (Graph is MethodGraph method)
            {
                method.Modifiers = value;
            }
        }
    }

    /// <summary>Whether <see cref="MethodModifiers.Sealed"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsSealed { get => Has(MethodModifiers.Sealed); set => Set(MethodModifiers.Sealed, value); }

    /// <summary>Whether <see cref="MethodModifiers.Abstract"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsAbstract { get => Has(MethodModifiers.Abstract); set => Set(MethodModifiers.Abstract, value); }

    /// <summary>Whether <see cref="MethodModifiers.Static"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsStatic { get => Has(MethodModifiers.Static); set => Set(MethodModifiers.Static, value); }

    /// <summary>Whether <see cref="MethodModifiers.Virtual"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsVirtual { get => Has(MethodModifiers.Virtual); set => Set(MethodModifiers.Virtual, value); }

    /// <summary>Whether <see cref="MethodModifiers.Override"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsOverride { get => Has(MethodModifiers.Override); set => Set(MethodModifiers.Override, value); }

    /// <summary>Whether <see cref="MethodModifiers.Async"/> is set. Always <see langword="false"/> for a constructor.</summary>
    public bool IsAsync { get => Has(MethodModifiers.Async); set => Set(MethodModifiers.Async, value); }

    private bool Has(MethodModifiers flag) => Modifiers.HasFlag(flag);

    private void Set(MethodModifiers flag, bool value) => Modifiers = value ? Modifiers | flag : Modifiers & ~flag;

    /// <summary>Re-raises this VM's properties when the wrapped graph's model properties change (editor-services.md §3).</summary>
    private void OnGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MethodGraph.Name):
                OnPropertyChanged(nameof(Name));
                break;
            case nameof(ExecutionGraph.Visibility):
                OnPropertyChanged(nameof(Visibility));
                break;
            case nameof(MethodGraph.Modifiers):
                OnPropertyChanged(nameof(Modifiers));
                OnPropertyChanged(nameof(IsSealed));
                OnPropertyChanged(nameof(IsAbstract));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsVirtual));
                OnPropertyChanged(nameof(IsOverride));
                OnPropertyChanged(nameof(IsAsync));
                break;
        }
    }

    /// <summary>Specifier used to call this method from a graph (drag &amp; drop, PAR-56).</summary>
    public MethodSpecifier ToMethodSpecifier(TypeSpecifier declaringType)
    {
        var method = (MethodGraph)Graph;
        return new MethodSpecifier(method.Name,
            method.NamedArgumentTypes.Select(nt => new MethodParameter(nt.Name, nt.Value, MethodParameterPassType.Default, false, null)),
            method.ReturnTypes.Cast<TypeSpecifier>(),
            method.Modifiers, method.Visibility,
            declaringType, Array.Empty<BaseType>());
    }

    /// <summary>Specifier used to construct the class with this constructor (drag &amp; drop, PAR-56).</summary>
    public ConstructorSpecifier ToConstructorSpecifier(TypeSpecifier declaringType) =>
        new(Graph.NamedArgumentTypes.Select(nt => new MethodParameter(nt.Name, nt.Value, MethodParameterPassType.Default, false, null)),
            declaringType);

    /// <summary>Unsubscribes from the wrapped graph's property-changed event.</summary>
    public void Dispose() => ((INotifyPropertyChanged)Graph).PropertyChanged -= OnGraphPropertyChanged;
}
