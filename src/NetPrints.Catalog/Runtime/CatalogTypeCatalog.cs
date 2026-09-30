using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Catalog;

/// <summary>
/// A <see cref="ITypeCatalog"/> over a loaded <see cref="CatalogDocument"/> (FR-016, FR-017): it answers the reflection queries of the
/// editor for the types the catalog lists, with the specifiers <c>ReflectionConverter</c> produces for the same symbols. Members
/// declared by a cataloged base type are inherited (an override hides the member it overrides); a base type outside the catalog
/// contributes nothing, and a type outside the catalog answers <see langword="false"/> to subclass and cast queries, except that every
/// class, struct and enum is an <c>System.Object</c>, and static classes are offered as types like the live provider
/// does (its test for them never holds), so that the composite provider can ask the other providers.
/// </summary>
public sealed class CatalogTypeCatalog : ITypeCatalog
{
    private const string ImplicitOperator = "op_Implicit";

    private const string ExplicitOperator = "op_Explicit";

    private readonly IReadOnlyList<TypeEntry> entries;

    private readonly Dictionary<string, TypeEntry> byKey = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<MethodEntry> conversions;

    // Variables whose type is a type parameter: the live provider never finds a subclass relation for them.
    private readonly HashSet<VariableSpecifier> genericTyped = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates the catalog over <paramref name="document"/>.</summary>
    /// <param name="document">The loaded catalog.</param>
    public CatalogTypeCatalog(CatalogDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        Info = new CatalogInfo(document.Id, document.Version, [.. document.Assemblies.Select(assembly => assembly.Name)]);

        Dictionary<string, CatalogType> byId = document.Types.ToDictionary(type => type.Id, StringComparer.Ordinal);
        List<TypeEntry> built = [];
        foreach (CatalogType type in document.Types)
        {
            TypeSpecifier declared = SpecifierFactory.Declared(type, byId);
            var entry = new TypeEntry(type, declared, Key(declared));
            bool isEnum = type.Kind == CatalogTypeKind.Enum;
            entry.Constructors = isEnum ? [SpecifierFactory.EnumConstructor(declared)] : [.. (type.Constructors ?? []).Select(constructor => SpecifierFactory.Constructor(constructor, declared))];
            entry.Methods = [.. (type.Methods ?? []).Select(method => new MethodEntry(method, SpecifierFactory.Method(method, declared), entry))];
            entry.Variables = isEnum
                ? [.. (type.EnumMembers ?? []).Select(name => SpecifierFactory.EnumMember(name, declared))]
                : [.. (type.Variables ?? []).Select(variable => SpecifierFactory.Variable(variable, type, declared, byId))];
            foreach (CatalogVariable variable in type.Variables ?? [])
            {
                if (variable.Type.Generic)
                {
                    genericTyped.Add(entry.Variables.First(specifier => specifier.Name == variable.Name));
                }
            }

            built.Add(entry);
            byKey[entry.Key] = entry;
        }

        entries = built;
        foreach (TypeEntry entry in entries)
        {
            entry.Base = entry.Type.BaseType is { } baseType && byKey.TryGetValue(Key(SpecifierFactory.Type(baseType)), out TypeEntry? found) ? found : null;
            entry.ClassChain = ClassChain(entry);
            entry.InterfaceKeys = [.. (entry.Type.Interfaces ?? []).Select(reference => Key(SpecifierFactory.Type(reference)))];
        }

        conversions = [.. entries.SelectMany(entry => entry.Methods).Where(IsConversion)];
    }

    /// <inheritdoc />
    public CatalogInfo Info { get; }

    /// <inheritdoc />
    public IEnumerable<TypeSpecifier> GetNonStaticTypes() => entries.Select(entry => entry.Specifier);

    /// <inheritdoc />
    public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        TypeEntry? entryA = Find(a);
        TypeEntry? entryB = Find(b);
        string keyA = Key(a);
        string keyB = Key(b);
        bool aIsInterface = a.IsInterface || entryA?.Type.Kind == CatalogTypeKind.Interface;

        if (b.IsInterface || entryB?.Type.Kind == CatalogTypeKind.Interface)
        {
            return keyA == keyB || (entryA?.InterfaceKeys.Contains(keyB, StringComparer.Ordinal) ?? false);
        }

        if (keyB == SpecifierFactory.ObjectTypeName + "`0")
        {
            return !aIsInterface;
        }

        return entryA is not null ? entryA.ClassChain.Contains(keyB, StringComparer.Ordinal) : keyA == keyB;
    }

    /// <inheritdoc />
    public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType)
    {
        ArgumentNullException.ThrowIfNull(fromType);
        ArgumentNullException.ThrowIfNull(toType);

        if (fromType == toType || toType.Name == SpecifierFactory.ObjectTypeName || TypeSpecifierIsSubclassOf(fromType, toType))
        {
            return true;
        }

        IReadOnlyList<string> candidateOwners = [.. ChainOf(fromType), .. ChainOf(toType)];
        foreach (MethodEntry conversion in conversions)
        {
            if (conversion.Method.Name != ImplicitOperator || !candidateOwners.Contains(conversion.Owner.Key, StringComparer.Ordinal))
            {
                continue;
            }

            if (conversion.Specifier.Parameters is [{ Value: TypeSpecifier source }] && conversion.Specifier.ReturnTypes is [TypeSpecifier target]
                && (source == fromType || TypeSpecifierIsSubclassOf(fromType, source))
                && (target == toType || TypeSpecifierIsSubclassOf(target, toType)))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier)
    {
        ArgumentNullException.ThrowIfNull(typeSpecifier);
        return Find(typeSpecifier) is { } entry
            ? Inherited(entry).Where(method => !IsOperator(method) && (method.Specifier.Modifiers & (MethodModifiers.Virtual | MethodModifiers.Override | MethodModifiers.Abstract)) != MethodModifiers.None).Select(method => method.Specifier)
            : [];
    }

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
    {
        ArgumentNullException.ThrowIfNull(methodSpecifier);
        if (Find(methodSpecifier.DeclaringType) is not { } entry)
        {
            return [];
        }

        bool isOperator = methodSpecifier.Name.StartsWith("op_", StringComparison.Ordinal);
        bool isStatic = methodSpecifier.Modifiers.HasFlag(MethodModifiers.Static);
        return Inherited(entry)
            .Where(method => method.Method.Name == methodSpecifier.Name
                && method.Method.Visibility == CatalogVisibility.Public
                && method.Specifier.Modifiers.HasFlag(MethodModifiers.Static) == isStatic
                && (isOperator ? IsOperator(method) && !IsConversion(method) : !IsOperator(method)))
            .Select(method => method.Specifier);
    }

    /// <inheritdoc />
    public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
    {
        ArgumentNullException.ThrowIfNull(typeSpecifier);
        return Find(typeSpecifier)?.Constructors ?? [];
    }

    /// <inheritdoc />
    public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier)
    {
        ArgumentNullException.ThrowIfNull(typeSpecifier);
        return Find(typeSpecifier)?.Type.EnumMembers ?? [];
    }

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<MethodEntry> methods;
        if (query.Type is not null)
        {
            TypeEntry? entry = Find(query.Type);
            IEnumerable<MethodEntry> own = entry is null ? [] : Inherited(entry);
            IEnumerable<MethodEntry> extensions = entries.SelectMany(candidate => candidate.Methods)
                .Where(method => SpecifierFactory.Has(method.Method.Modifiers, "extension")
                    && method.Specifier.Parameters is [{ Value: TypeSpecifier first }, ..]
                    && (first == query.Type || TypeSpecifierIsSubclassOf(query.Type, first)));
            methods = own.Concat(extensions);
        }
        else
        {
            methods = entries.SelectMany(entry => entry.Methods).Where(method => !IsConversion(method));
        }

        IEnumerable<MethodSpecifier> result = methods.Select(method => method.Specifier);

        if (query.Static.HasValue)
        {
            result = result.Where(method => method.Modifiers.HasFlag(MethodModifiers.Static) == query.Static.Value);
        }

        if (query.HasGenericArguments.HasValue)
        {
            result = result.Where(method => (method.GenericArguments.Count > 0) == query.HasGenericArguments.Value);
        }

        if (query.VisibleFrom is not null)
        {
            result = result.Where(method => NetPrintsUtil.IsVisible(query.VisibleFrom, method.DeclaringType, method.Visibility, TypeSpecifierIsSubclassOf));
        }

        if (query.ArgumentType is { } argumentType)
        {
            result = result.Where(method => method.Parameters.Any(parameter =>
                parameter.Value is GenericType
                || (parameter.Value is TypeSpecifier type && (type == argumentType || TypeSpecifierIsSubclassOf(argumentType, type)))));
        }

        if (query.ReturnType is { } returnType)
        {
            result = result.Where(method => method.ReturnTypes.Count == 0
                ? VoidIsSubclassOf(returnType)
                : method.ReturnTypes.Any(type =>
                    type is GenericType
                    || (type is TypeSpecifier specifier && (specifier == returnType || TypeSpecifierIsSubclassOf(specifier, returnType)))));
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<VariableSpecifier> variables = query.Type is not null
            ? Find(query.Type) is { } entry ? InheritedVariables(entry) : []
            : entries.SelectMany(candidate => candidate.Variables);

        IEnumerable<VariableSpecifier> result = variables;

        if (query.Static.HasValue)
        {
            result = result.Where(variable => variable.Modifiers.HasFlag(VariableModifiers.Static) == query.Static.Value);
        }

        if (query.VisibleFrom is not null)
        {
            result = result.Where(variable => NetPrintsUtil.IsVisible(query.VisibleFrom, variable.DeclaringType, DeclaredVisibility(variable), TypeSpecifierIsSubclassOf));
        }

        if (query.VariableType is { } variableType)
        {
            result = result.Where(variable => !genericTyped.Contains(variable) && (query.VariableTypeDerivesFrom
                ? TypeSpecifierIsSubclassOf(variable.Type, variableType)
                : TypeSpecifierIsSubclassOf(variableType, variable.Type)));
        }

        return result;
    }

    /// <inheritdoc />
    public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) => FindMethod(methodSpecifier)?.Method.Summary;

    /// <inheritdoc />
    public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) =>
        FindMethod(methodSpecifier)?.Method.Parameters is { } parameters && parameterIndex >= 0 && parameterIndex < parameters.Count ? parameters[parameterIndex].Summary : null;

    /// <inheritdoc />
    public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) =>
        returnIndex == 0 ? FindMethod(methodSpecifier)?.Method.ReturnSummary : null;

    // The live provider sees a void return as System.Void, a struct: it derives from System.ValueType and System.Object.
    private static bool VoidIsSubclassOf(TypeSpecifier type) =>
        type.Name is "System.Void" or "System.ValueType" or SpecifierFactory.ObjectTypeName && type.GenericArguments.Count == 0;

    private static string Key(TypeSpecifier type) => type.Name + "`" + type.GenericArguments.Count;

    private static bool IsOperator(MethodEntry method) => SpecifierFactory.Has(method.Method.Modifiers, "operator");

    private static bool IsConversion(MethodEntry method) =>
        IsOperator(method) && method.Method.Name is ImplicitOperator or ExplicitOperator;

    private static MemberVisibility DeclaredVisibility(VariableSpecifier variable) =>
        variable.GetterVisibility.HasFlag(MemberVisibility.Public) || variable.SetterVisibility.HasFlag(MemberVisibility.Public)
            ? MemberVisibility.Public
            : MemberVisibility.Protected;

    private static string Signature(CatalogMethod method) =>
        $"{method.Name}`{method.GenericParameters?.Count ?? 0}({string.Join(",", (method.Parameters ?? []).Select(parameter => Render(parameter.Type) + ":" + parameter.PassType))})";

    private static string Render(CatalogTypeRef reference) =>
        reference.Args is { Count: > 0 } args ? $"{reference.Name}<{string.Join(",", args.Select(Render))}>" : reference.Name;

    private TypeEntry? Find(TypeSpecifier type) => byKey.GetValueOrDefault(Key(type));

    private IReadOnlyList<string> ChainOf(TypeSpecifier type) => Find(type)?.ClassChain ?? [Key(type)];

    private MethodEntry? FindMethod(MethodSpecifier specifier)
    {
        ArgumentNullException.ThrowIfNull(specifier);
        return Find(specifier.DeclaringType)?.Methods.FirstOrDefault(method =>
            method.Method.Name == specifier.Name && method.Specifier.ArgumentTypes.SequenceEqual(specifier.ArgumentTypes));
    }

    private IReadOnlyList<string> ClassChain(TypeEntry entry)
    {
        List<string> chain = [entry.Key];
        if (entry.Type.Kind == CatalogTypeKind.Interface)
        {
            return chain;
        }

        TypeEntry? current = entry;
        string? last = entry.Key;
        while (current?.Type.BaseType is { } baseType)
        {
            last = Key(SpecifierFactory.Type(baseType));
            chain.Add(last);
            current = current.Base;
        }

        if (last == "System.Enum`0")
        {
            chain.Add("System.ValueType`0");
        }
        else if (last == "System.MulticastDelegate`0")
        {
            chain.Add("System.Delegate`0");
        }

        chain.Add(SpecifierFactory.ObjectTypeName + "`0");
        return chain;
    }

    private static IEnumerable<MethodEntry> Inherited(TypeEntry entry)
    {
        HashSet<string> overridden = new(StringComparer.Ordinal);
        for (TypeEntry? current = entry; current is not null; current = current.Base)
        {
            foreach (MethodEntry method in current.Methods)
            {
                if (IsConversion(method) || overridden.Contains(Signature(method.Method)))
                {
                    continue;
                }

                yield return method;
            }

            foreach (MethodEntry method in current.Methods.Where(method => method.Specifier.Modifiers.HasFlag(MethodModifiers.Override)))
            {
                overridden.Add(Signature(method.Method));
            }
        }
    }

    private static IEnumerable<VariableSpecifier> InheritedVariables(TypeEntry entry)
    {
        for (TypeEntry? current = entry; current is not null; current = current.Base)
        {
            foreach (VariableSpecifier variable in current.Variables)
            {
                yield return variable;
            }
        }
    }

    private sealed class TypeEntry(CatalogType type, TypeSpecifier specifier, string key)
    {
        public CatalogType Type { get; } = type;

        public TypeSpecifier Specifier { get; } = specifier;

        public string Key { get; } = key;

        public TypeEntry? Base { get; set; }

        public IReadOnlyList<string> ClassChain { get; set; } = [];

        public IReadOnlyList<string> InterfaceKeys { get; set; } = [];

        public IReadOnlyList<ConstructorSpecifier> Constructors { get; set; } = [];

        public IReadOnlyList<MethodEntry> Methods { get; set; } = [];

        public IReadOnlyList<VariableSpecifier> Variables { get; set; } = [];
    }

    private sealed record MethodEntry(CatalogMethod Method, MethodSpecifier Specifier, TypeEntry Owner);
}
