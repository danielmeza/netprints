using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Catalog;

/// <summary>
/// Builds NetPrints specifiers from catalog records with the rules of <c>ReflectionConverter</c>, so a catalog answers with
/// the values the live provider would produce for the same symbols (data-model.md §1, FR-016).
/// </summary>
internal static class SpecifierFactory
{
    public const string ObjectTypeName = "System.Object";

    /// <summary>The <c>Outer+Inner</c> name, namespace-qualified, of a cataloged type.</summary>
    public static string FullName(CatalogType type, IReadOnlyDictionary<string, CatalogType> byId)
    {
        if (type.DeclaringType is { } outer && byId.TryGetValue(outer, out CatalogType? declaring))
        {
            return FullName(declaring, byId) + "+" + type.Name;
        }

        return string.IsNullOrEmpty(type.Namespace) ? type.Name : type.Namespace + "." + type.Name;
    }

    /// <summary>The specifier of a cataloged type: its own generic parameters are its arguments, as for the declaring type of a member.</summary>
    public static TypeSpecifier Declared(CatalogType type, IReadOnlyDictionary<string, CatalogType> byId) =>
        new(FullName(type, byId), type.Kind == CatalogTypeKind.Enum, type.Kind == CatalogTypeKind.Interface, type.GenericParameters?.Select(name => (BaseType)new GenericType(name)));

    public static TypeSpecifier Type(CatalogTypeRef reference) =>
        new(reference.Name, reference.IsEnum, reference.IsInterface, reference.Args?.Select(Base));

    public static BaseType Base(CatalogTypeRef reference) => reference.Generic ? new GenericType(reference.Name) : Type(reference);

    /// <summary>
    /// The type of a field or property. <c>ReflectionConverter</c> turns a type parameter here into a <see cref="TypeSpecifier"/> named
    /// after the declaring type (<c>Ns.Box+T</c>), and so does this, until the live provider changes.
    /// </summary>
    public static TypeSpecifier VariableType(CatalogTypeRef reference, CatalogType owner, IReadOnlyDictionary<string, CatalogType> byId) =>
        reference.Generic ? new TypeSpecifier(FullName(owner, byId) + "+" + reference.Name) : Type(reference);

    public static MemberVisibility Visibility(CatalogVisibility visibility) =>
        visibility == CatalogVisibility.Protected ? MemberVisibility.Protected : MemberVisibility.Public;

    public static MethodModifiers Modifiers(IReadOnlyList<string>? modifiers)
    {
        MethodModifiers result = MethodModifiers.None;
        foreach (string modifier in modifiers ?? [])
        {
            result |= modifier switch
            {
                "static" => MethodModifiers.Static,
                "abstract" => MethodModifiers.Abstract,
                "virtual" => MethodModifiers.Virtual,
                "override" => MethodModifiers.Override,
                "sealed" => MethodModifiers.Sealed,
                _ => MethodModifiers.None,
            };
        }

        return result;
    }

    public static bool Has(IReadOnlyList<string>? modifiers, string modifier) => modifiers is not null && modifiers.Contains(modifier, StringComparer.Ordinal);

    public static MethodParameter Parameter(CatalogParameter parameter) =>
        new(
            parameter.Name,
            Base(parameter.Type),
            parameter.PassType switch
            {
                CatalogPassType.Reference => MethodParameterPassType.Reference,
                CatalogPassType.Out => MethodParameterPassType.Out,
                CatalogPassType.In => MethodParameterPassType.In,
                _ => MethodParameterPassType.Default,
            },
            parameter.Default is not null,
            DefaultValue(parameter.Default))
        {
            IsParams = parameter.Params,
        };

    public static MethodSpecifier Method(CatalogMethod method, TypeSpecifier declaringType) =>
        new(
            method.Name,
            (method.Parameters ?? []).Select(Parameter),
            method.ReturnType is { } returnType ? [Base(returnType)] : [],
            Modifiers(method.Modifiers),
            Visibility(method.Visibility),
            declaringType,
            [.. (method.GenericParameters ?? []).Select(name => (BaseType)new GenericType(name))]);

    public static ConstructorSpecifier Constructor(CatalogConstructor constructor, TypeSpecifier declaringType) =>
        new((constructor.Parameters ?? []).Select(Parameter), declaringType);

    /// <summary>The static constant the live provider lists for an enum member.</summary>
    public static VariableSpecifier EnumMember(string name, TypeSpecifier enumType) =>
        new(name, enumType, MemberVisibility.Public, MemberVisibility.Public, enumType, VariableModifiers.Static | VariableModifiers.Const);

    /// <summary>The parameterless constructor the live provider lists for an enum.</summary>
    public static ConstructorSpecifier EnumConstructor(TypeSpecifier enumType) => new([], enumType);

    public static VariableSpecifier Variable(CatalogVariable variable, CatalogType owner, TypeSpecifier declaringType, IReadOnlyDictionary<string, CatalogType> byId)
    {
        VariableModifiers modifiers = VariableModifiers.None;
        if (Has(variable.Modifiers, "static"))
        {
            modifiers |= VariableModifiers.Static;
        }

        if (Has(variable.Modifiers, "readonly"))
        {
            modifiers |= VariableModifiers.ReadOnly;
        }

        if (Has(variable.Modifiers, "const"))
        {
            modifiers |= VariableModifiers.Const;
        }

        return new VariableSpecifier(
            variable.Name,
            VariableType(variable.Type, owner, byId),
            variable.Get is { } get ? Visibility(get) : MemberVisibility.Private,
            variable.Kind == CatalogVariableKind.Field ? Visibility(variable.Get ?? CatalogVisibility.Public) : variable.Set is { } set ? Visibility(set) : MemberVisibility.Private,
            declaringType,
            modifiers);
    }

    /// <summary>The value of a parameter default as the live provider carries it: the constant of its runtime type.</summary>
    public static object? DefaultValue(CatalogTypedValue? typed)
    {
        if (typed?.Value is not { } text)
        {
            return null;
        }

        CultureInfo invariant = CultureInfo.InvariantCulture;
        return typed.Type switch
        {
            "System.Boolean" => bool.Parse(text),
            "System.Char" => text.Length > 0 ? text[0] : default(char),
            "System.SByte" => sbyte.Parse(text, invariant),
            "System.Byte" => byte.Parse(text, invariant),
            "System.Int16" => short.Parse(text, invariant),
            "System.UInt16" => ushort.Parse(text, invariant),
            "System.Int32" => int.Parse(text, invariant),
            "System.UInt32" => uint.Parse(text, invariant),
            "System.Int64" => long.Parse(text, invariant),
            "System.UInt64" => ulong.Parse(text, invariant),
            "System.Single" => float.Parse(text, invariant),
            "System.Double" => double.Parse(text, invariant),
            "System.Decimal" => decimal.Parse(text, invariant),
            _ => text,
        };
    }
}
