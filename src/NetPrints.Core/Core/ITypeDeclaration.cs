#nullable enable
namespace NetPrints.Core;

/// <summary>
/// A type the translator declares in generated code: what the translation seams
/// (<c>NetPrints.Translator</c> node translators and emitters) may rely on without assuming the declaration
/// is a class. <see cref="ClassGraph"/> is the only implementation today.
/// </summary>
public interface ITypeDeclaration
{
    /// <summary>
    /// Name of the type without namespace.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Namespace the type is in, empty for none.
    /// </summary>
    string Namespace { get; }

    /// <summary>
    /// Name of the type with its namespace, if any.
    /// </summary>
    string FullName { get; }

    /// <summary>
    /// Visibility of the type.
    /// </summary>
    MemberVisibility Visibility { get; }
}
