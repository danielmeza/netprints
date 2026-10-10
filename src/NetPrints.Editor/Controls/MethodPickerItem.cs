using NetPrints.Core;
using NetPrints.Editor.Search;

namespace NetPrints.Editor.Controls;

/// <summary>
/// One method or constructor offered by <see cref="MethodPickerListViewModel"/>, with the text and marks its row shows.
/// </summary>
/// <param name="Member">The <see cref="MethodSpecifier"/> or <see cref="ConstructorSpecifier"/> the row stands for.</param>
/// <param name="DeclaringTypeName">The group the row sits under: the declaring type's short name.</param>
/// <param name="Name">The method name, or the type name for a constructor; used to sort and to filter.</param>
/// <param name="ParameterCount">The number of parameters; used to sort.</param>
/// <param name="Signature">The signature as <see cref="MethodSignatureFormatter"/> writes it; shown, sorted by and filtered on.</param>
/// <param name="IsCurrent">Whether this is the overload the node uses now; it is marked and listed first in its group.</param>
/// <param name="IsOverridden">Whether the class already overrides the method; the row is dimmed and cannot be picked.</param>
/// <param name="IsAbstract">Whether the method is abstract and must be overridden; the row is marked.</param>
public sealed record MethodPickerItem(
    object Member,
    string DeclaringTypeName,
    string Name,
    int ParameterCount,
    string Signature,
    bool IsCurrent = false,
    bool IsOverridden = false,
    bool IsAbstract = false)
{
    /// <summary>Gets the method this item stands for, or <see langword="null"/> for a constructor.</summary>
    public MethodSpecifier? Method => Member as MethodSpecifier;

    /// <summary>Gets the constructor this item stands for, or <see langword="null"/> for a method.</summary>
    public ConstructorSpecifier? Constructor => Member as ConstructorSpecifier;

    /// <summary>The group both size modes of a make-array node share.</summary>
    public const string ModeGroupName = "Size mode";

    /// <summary>Gets the size-mode text this item stands for, or <see langword="null"/> for a method or a constructor.</summary>
    public string? Mode => Member as string;

    /// <summary>Creates the item of a make-array node's size mode, which shares the overload list.</summary>
    /// <param name="mode">The mode text, <c>ModelOperations.UsePredefinedSize</c> or <c>ModelOperations.UseInitializerList</c>.</param>
    /// <param name="isCurrent">Whether it is the mode in use.</param>
    /// <returns>The item, named and signed by its text.</returns>
    public static MethodPickerItem ForMode(string mode, bool isCurrent = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(mode);
        return new MethodPickerItem(mode, ModeGroupName, mode, 0, mode, isCurrent);
    }

    /// <summary>Creates the item of a method.</summary>
    /// <param name="method">The method.</param>
    /// <param name="isCurrent">Whether it is the overload in use.</param>
    /// <param name="isOverridden">Whether the class already overrides it.</param>
    /// <returns>The item; <see cref="IsAbstract"/> comes from the method's modifiers.</returns>
    public static MethodPickerItem For(MethodSpecifier method, bool isCurrent = false, bool isOverridden = false)
    {
        ArgumentNullException.ThrowIfNull(method);
        return new MethodPickerItem(method, MethodSignatureFormatter.DeclaringTypeName(method), method.Name, method.Parameters.Count,
            MethodSignatureFormatter.Format(method), isCurrent, isOverridden, method.Modifiers.HasFlag(MethodModifiers.Abstract));
    }

    /// <summary>Creates the item of a constructor.</summary>
    /// <param name="constructor">The constructor.</param>
    /// <param name="isCurrent">Whether it is the overload in use.</param>
    /// <returns>The item, named after its type.</returns>
    public static MethodPickerItem For(ConstructorSpecifier constructor, bool isCurrent = false)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        string typeName = MethodSignatureFormatter.DeclaringTypeName(constructor);
        return new MethodPickerItem(constructor, typeName, typeName, constructor.Arguments.Count, MethodSignatureFormatter.Format(constructor), isCurrent);
    }
}
