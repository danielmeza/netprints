#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// An <see cref="ITypeCatalog"/> backed by precomputed lists (extension-points.md §4). Immutable: the
/// constructor copies its arguments. It carries no inheritance or conversion data, so
/// <see cref="TypeSpecifierIsSubclassOf"/> and <see cref="HasImplicitCast"/> always answer
/// <see langword="false"/> and the composite falls through to the other providers.
/// </summary>
public sealed class InMemoryTypeCatalog : ITypeCatalog
{
    private readonly IReadOnlyList<TypeSpecifier> nonStaticTypes;
    private readonly IReadOnlyList<MethodSpecifier> methods;
    private readonly IReadOnlyList<VariableSpecifier> variables;
    private readonly IReadOnlyList<ConstructorSpecifier> constructors;
    private readonly IReadOnlyDictionary<TypeSpecifier, IReadOnlyList<string>> enumNames;
    private readonly IReadOnlyDictionary<MethodSpecifier, string> documentation;

    /// <summary>
    /// Creates a catalog over the given contents.
    /// </summary>
    /// <param name="info">Identity and covered assemblies.</param>
    /// <param name="nonStaticTypes">Types the catalog offers.</param>
    /// <param name="methods">Every method the catalog knows, static and instance.</param>
    /// <param name="variables">Every field and property the catalog knows.</param>
    /// <param name="constructors">Every constructor the catalog knows.</param>
    /// <param name="enumNames">Member names per enum type.</param>
    /// <param name="documentation">Summary text per method.</param>
    public InMemoryTypeCatalog(
        CatalogInfo info,
        IReadOnlyList<TypeSpecifier> nonStaticTypes,
        IReadOnlyList<MethodSpecifier> methods,
        IReadOnlyList<VariableSpecifier> variables,
        IReadOnlyList<ConstructorSpecifier> constructors,
        IReadOnlyDictionary<TypeSpecifier, IReadOnlyList<string>> enumNames,
        IReadOnlyDictionary<MethodSpecifier, string> documentation)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        this.nonStaticTypes = Copy(nonStaticTypes);
        this.methods = Copy(methods);
        this.variables = Copy(variables);
        this.constructors = Copy(constructors);
        this.enumNames = new Dictionary<TypeSpecifier, IReadOnlyList<string>>(
            enumNames ?? throw new ArgumentNullException(nameof(enumNames)));
        this.documentation = new Dictionary<MethodSpecifier, string>(
            documentation ?? throw new ArgumentNullException(nameof(documentation)));
    }

    /// <inheritdoc />
    public CatalogInfo Info { get; }

    /// <inheritdoc />
    public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b) => false;

    /// <inheritdoc />
    public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType) => false;

    /// <inheritdoc />
    public IEnumerable<TypeSpecifier> GetNonStaticTypes() => nonStaticTypes;

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier) =>
        methods.Where(method => method.DeclaringType == typeSpecifier
            && (method.Modifiers.HasFlag(MethodModifiers.Virtual)
                || method.Modifiers.HasFlag(MethodModifiers.Override)
                || method.Modifiers.HasFlag(MethodModifiers.Abstract)));

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier) =>
        methods.Where(method => method.DeclaringType == methodSpecifier.DeclaringType
            && method.Name == methodSpecifier.Name
            && method.Visibility.HasFlag(MemberVisibility.Public)
            && method.Modifiers.HasFlag(MethodModifiers.Static) == methodSpecifier.Modifiers.HasFlag(MethodModifiers.Static));

    /// <inheritdoc />
    public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier) =>
        constructors.Where(constructor => constructor.DeclaringType == typeSpecifier);

    /// <inheritdoc />
    public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier) =>
        enumNames.TryGetValue(typeSpecifier, out IReadOnlyList<string>? names) ? names : [];

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<MethodSpecifier> result = methods;

        if (query.Type is not null)
        {
            result = result.Where(method => method.DeclaringType == query.Type);
        }

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

        if (query.ArgumentType is not null)
        {
            result = result.Where(method => method.ArgumentTypes.Any(type => type is TypeSpecifier specifier && specifier == query.ArgumentType));
        }

        if (query.ReturnType is not null)
        {
            result = result.Where(method => method.ReturnTypes.Any(type => type is TypeSpecifier specifier && specifier == query.ReturnType));
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IEnumerable<VariableSpecifier> result = variables;

        if (query.Type is not null)
        {
            result = result.Where(variable => variable.DeclaringType == query.Type);
        }

        if (query.Static.HasValue)
        {
            result = result.Where(variable => variable.Modifiers.HasFlag(VariableModifiers.Static) == query.Static.Value);
        }

        if (query.VisibleFrom is not null)
        {
            result = result.Where(variable => NetPrintsUtil.IsVisible(query.VisibleFrom, variable.DeclaringType, variable.Visibility, TypeSpecifierIsSubclassOf));
        }

        if (query.VariableType is not null)
        {
            result = result.Where(variable => variable.Type == query.VariableType);
        }

        return result;
    }

    /// <inheritdoc />
    public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) =>
        documentation.TryGetValue(methodSpecifier, out string? text) ? text : null;

    /// <inheritdoc />
    public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) => null;

    /// <inheritdoc />
    public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) => null;

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source) =>
        (source ?? throw new ArgumentNullException(nameof(source))).ToArray();
}
