using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;
using NetPrints.Editor.CodeView;

namespace NetPrints.Editor.Inspectors;

/// <summary>The class inspector: the class's name, namespace, visibility and modifiers, and its generated code.</summary>
public sealed class ClassInspectorViewModel : ObservableObject
{
    private readonly Action markDirty;

    /// <summary>Creates the inspector of a class.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="codeView">The read-only C# code view of the class.</param>
    /// <param name="markDirty">Called after an edit, to mark the class dirty.</param>
    public ClassInspectorViewModel(ClassGraph cls, CodeViewViewModel codeView, Action markDirty)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(codeView);
        ArgumentNullException.ThrowIfNull(markDirty);
        Class = cls;
        CodeView = codeView;
        this.markDirty = markDirty;
    }

    /// <summary>Gets the class.</summary>
    public ClassGraph Class { get; }

    /// <summary>Gets the read-only C# code view of the class.</summary>
    public CodeViewViewModel CodeView { get; }

    /// <summary>Gets the class name with its namespace.</summary>
    public string FullName => Class.FullName ?? "";

    /// <summary>Gets the title of the class: its name.</summary>
    public string Title => Class.Name ?? "";

    /// <summary>Gets the visibility values offered by the visibility chooser.</summary>
    public IReadOnlyList<MemberVisibility> PossibleVisibilities => VisibilityChoices.All;

    /// <summary>Gets or sets the class's name, without namespace; also refreshes <see cref="Title"/> and <see cref="FullName"/>.</summary>
    public string Name
    {
        get => Class.Name;
        set
        {
            if (Class.Name != value)
            {
                Class.Name = value;
                markDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>Gets or sets the class's namespace; also refreshes <see cref="FullName"/>.</summary>
    public string Namespace
    {
        get => Class.Namespace;
        set
        {
            if (Class.Namespace != value)
            {
                Class.Namespace = value;
                markDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    /// <summary>Gets or sets the class's visibility.</summary>
    public MemberVisibility Visibility
    {
        get => Class.Visibility;
        set
        {
            if (Class.Visibility != value)
            {
                Class.Visibility = value;
                markDirty();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Gets or sets the class's modifiers.</summary>
    public ClassModifiers Modifiers
    {
        get => Class.Modifiers;
        set
        {
            if (Class.Modifiers != value)
            {
                Class.Modifiers = value;
                markDirty();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSealed));
                OnPropertyChanged(nameof(IsAbstract));
                OnPropertyChanged(nameof(IsStatic));
                OnPropertyChanged(nameof(IsPartial));
            }
        }
    }

    /// <summary>Gets or sets whether <see cref="ClassModifiers.Sealed"/> is set.</summary>
    public bool IsSealed { get => Modifiers.HasFlag(ClassModifiers.Sealed); set => SetModifier(ClassModifiers.Sealed, value); }

    /// <summary>Gets or sets whether <see cref="ClassModifiers.Abstract"/> is set.</summary>
    public bool IsAbstract { get => Modifiers.HasFlag(ClassModifiers.Abstract); set => SetModifier(ClassModifiers.Abstract, value); }

    /// <summary>Gets or sets whether <see cref="ClassModifiers.Static"/> is set.</summary>
    public bool IsStatic { get => Modifiers.HasFlag(ClassModifiers.Static); set => SetModifier(ClassModifiers.Static, value); }

    /// <summary>Gets or sets whether <see cref="ClassModifiers.Partial"/> is set.</summary>
    public bool IsPartial { get => Modifiers.HasFlag(ClassModifiers.Partial); set => SetModifier(ClassModifiers.Partial, value); }

    private void SetModifier(ClassModifiers flag, bool value) => Modifiers = value ? Modifiers | flag : Modifiers & ~flag;
}
