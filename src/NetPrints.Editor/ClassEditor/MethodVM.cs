using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// A method or constructor in the class editor lists and the method inspector (PAR-24, 27, 35).
/// </summary>
/// <remarks>
/// The WPF editor used a full graph view model per list entry; a light wrapper avoids building
/// node view models for graphs that are not open.
/// </remarks>
public sealed class MethodVM(ExecutionGraph graph) : ObservableObject
{
    /// <summary>The wrapped model graph.</summary>
    public ExecutionGraph Graph { get; } = graph;

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
                method.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The graph's visibility.</summary>
    public MemberVisibility Visibility
    {
        get => Graph.Visibility;
        set
        {
            if (Graph.Visibility != value)
            {
                Graph.Visibility = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The visibility values offered by the method's visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => ClassEditorVM.Visibilities;

    /// <summary>The graph's modifiers, or <see cref="MethodModifiers.None"/> for a constructor (which has none).</summary>
    public MethodModifiers Modifiers
    {
        get => Graph is MethodGraph method ? method.Modifiers : MethodModifiers.None;
        set
        {
            if (Graph is MethodGraph method && method.Modifiers != value)
            {
                method.Modifiers = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSealed));
                OnPropertyChanged(nameof(IsAbstract));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsVirtual));
                OnPropertyChanged(nameof(IsOverride));
                OnPropertyChanged(nameof(IsAsync));
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
}
