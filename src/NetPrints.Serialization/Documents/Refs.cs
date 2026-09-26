#nullable enable
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Serialization.Documents;

/// <summary>
/// Whether a <see cref="VariableRef"/> describes a class member or a method-local variable
/// (document-format.md §1.6). Method-local variables are a sub-phase H (US5) feature; until then
/// every <see cref="VariableRef"/> this project produces or consumes is <see cref="Member"/>. This is
/// a document-level enum: the model's own <c>VariableSpecifier</c> gains a matching <c>Scope</c>
/// member (and, presumably, this enum moves to <c>NetPrints.Core</c>) in sub-phase H.
/// </summary>
public enum VariableScope
{
    /// <summary>A class-level field or property.</summary>
    Member,

    /// <summary>A method- or constructor-local variable.</summary>
    Local,
}

/// <summary>
/// Reference to a type (document-format.md §1.6): a full name, whether it is an unbound generic
/// parameter, whether it is an enum or interface, and its generic arguments (if any).
/// </summary>
/// <param name="Name">Full name of the type (e.g. <c>"System.String"</c>), or the generic parameter's
/// name when <paramref name="Generic"/> is <see langword="true"/>.</param>
/// <param name="Generic"><see langword="true"/> if this reference is to an unbound generic
/// parameter (a <see cref="GenericType"/>) rather than a concrete type.</param>
/// <param name="IsEnum"><see langword="true"/> if the type is an enum.</param>
/// <param name="IsInterface"><see langword="true"/> if the type is an interface.</param>
/// <param name="Args">The type's generic arguments, or <see langword="null"/> if it takes none.</param>
public sealed record TypeRef(string Name, bool Generic = false, bool IsEnum = false, bool IsInterface = false,
    IReadOnlyList<TypeRef>? Args = null);

/// <summary>
/// Reference to a method parameter (document-format.md §1.6): its name, type, how it is passed, and
/// its explicit default value, if any.
/// </summary>
/// <param name="Name">Parameter name.</param>
/// <param name="Type">Parameter type.</param>
/// <param name="PassType">How the parameter is passed (by value, by reference, out, in).</param>
/// <param name="Default">The parameter's explicit default value, present only when the parameter has
/// one.</param>
public sealed record ParameterRef(string Name, TypeRef Type,
    MethodParameterPassType PassType = MethodParameterPassType.Default, TypedValue? Default = null);

/// <summary>
/// Reference to a method (document-format.md §1.6): name, declaring type, parameters, return types,
/// modifiers, visibility and generic arguments.
/// </summary>
/// <param name="Name">Method name.</param>
/// <param name="DeclaringType">Type the method is declared on.</param>
/// <param name="Parameters">The method's parameters, or <see langword="null"/> if it takes none.</param>
/// <param name="ReturnTypes">The method's return values (0, 1 or more), or <see langword="null"/> if
/// it returns nothing.</param>
/// <param name="Modifiers">Method modifiers.</param>
/// <param name="Visibility">Method visibility.</param>
/// <param name="GenericArgs">The method's generic arguments, or <see langword="null"/> if it takes
/// none.</param>
public sealed record MethodRef(string Name, TypeRef DeclaringType, IReadOnlyList<ParameterRef>? Parameters,
    IReadOnlyList<TypeRef>? ReturnTypes, MethodModifiers Modifiers, MemberVisibility Visibility,
    IReadOnlyList<TypeRef>? GenericArgs);

/// <summary>
/// Reference to a constructor (document-format.md §1.6): the constructed type and its parameters.
/// </summary>
/// <param name="DeclaringType">Type the constructor constructs.</param>
/// <param name="Parameters">The constructor's parameters, or <see langword="null"/> if it takes
/// none.</param>
public sealed record ConstructorRef(TypeRef DeclaringType, IReadOnlyList<ParameterRef>? Parameters);

/// <summary>
/// Reference to a variable (field or property, document-format.md §1.6): name, type, declaring type
/// (omitted for a local), getter/setter/overall visibility, modifiers and scope.
/// </summary>
/// <param name="Name">Variable name.</param>
/// <param name="Type">Variable type.</param>
/// <param name="DeclaringType">Type the variable is declared on, or <see langword="null"/> for a
/// method-local variable.</param>
/// <param name="GetterVisibility">Visibility of the variable's getter.</param>
/// <param name="SetterVisibility">Visibility of the variable's setter.</param>
/// <param name="Visibility">Overall visibility of the variable.</param>
/// <param name="Modifiers">Variable modifiers.</param>
/// <param name="Scope">Whether this is a class member or a method-local variable.</param>
public sealed record VariableRef(string Name, TypeRef Type, TypeRef? DeclaringType, MemberVisibility GetterVisibility,
    MemberVisibility SetterVisibility, MemberVisibility Visibility, VariableModifiers Modifiers,
    VariableScope Scope = VariableScope.Member);

/// <summary>
/// A typed, unconnected pin value (document-format.md §1.6, §1.4/§2.6): the value's runtime type (full
/// CLR name) and its string form (<c>null</c> for a reference-typed value that is itself
/// <see langword="null"/>). See <c>TypedValueConverter</c> for the conversion rules.
/// </summary>
/// <param name="Type">Full CLR name of the value's runtime type.</param>
/// <param name="Value">The value's string form, or <see langword="null"/>.</param>
public sealed record TypedValue(string Type, string? Value);
