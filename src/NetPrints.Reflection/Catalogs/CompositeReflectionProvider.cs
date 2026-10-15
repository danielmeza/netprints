#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// Combines several <see cref="IReflectionProvider"/>s (extension catalogs in registry order, then the live
/// provider) into one (extension-points.md §4). Boolean queries are true when any provider says so,
/// enumerations are concatenated in provider order without duplicates, and documentation comes from the
/// first provider that has it. Holds no state of its own.
/// </summary>
public sealed class CompositeReflectionProvider : IReflectionProvider, ICatalogScope
{
    /// <summary>
    /// Creates a composite over <paramref name="providers"/>.
    /// </summary>
    /// <param name="providers">Providers in precedence order; the first occurrence of a duplicate wins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="providers"/> is empty.</exception>
    public CompositeReflectionProvider(IReadOnlyList<IReflectionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        if (providers.Count == 0)
        {
            throw new ArgumentException("A composite reflection provider needs at least one provider.", nameof(providers));
        }

        Providers = providers.ToArray();
    }

    /// <summary>
    /// The combined providers, in precedence order.
    /// </summary>
    public IReadOnlyList<IReflectionProvider> Providers { get; }

    /// <inheritdoc />
    public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b) =>
        Providers.Any(provider => provider.TypeSpecifierIsSubclassOf(a, b));

    /// <inheritdoc />
    public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType) =>
        Providers.Any(provider => provider.HasImplicitCast(fromType, toType));

    /// <inheritdoc />
    public IEnumerable<TypeSpecifier> GetNonStaticTypes() =>
        Providers.SelectMany(provider => provider.GetNonStaticTypes()).Distinct();

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier) =>
        Providers.SelectMany(provider => provider.GetOverridableMethodsForType(typeSpecifier)).Distinct();

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier) =>
        Providers.SelectMany(provider => provider.GetPublicMethodOverloads(methodSpecifier)).Distinct();

    /// <inheritdoc />
    public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier) =>
        Providers.SelectMany(provider => provider.GetConstructors(typeSpecifier)).Distinct(SpecifierComparers.Constructors);

    /// <inheritdoc />
    public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier) =>
        Providers.SelectMany(provider => provider.GetEnumNames(typeSpecifier)).Distinct();

    /// <inheritdoc />
    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Type is { } type && HasCatalogs
            ? Scoped(type, query, (provider, q) => provider.GetMethods(q), method => method.DeclaringType, RetargetMethods, null)
            : Providers.SelectMany(provider => provider.GetMethods(query)).Distinct();
    }

    /// <inheritdoc />
    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Type is { } type && HasCatalogs
            ? Scoped(type, query, (provider, q) => provider.GetVariables(q), variable => variable.DeclaringType, RetargetVariables, SpecifierComparers.Variables)
            : Providers.SelectMany(provider => provider.GetVariables(query)).Distinct(SpecifierComparers.Variables);
    }

    /// <inheritdoc />
    public string? GetHidingCatalogId(TypeSpecifier type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return CoveringCatalog(type) is { } catalog && !Lists(catalog, type) ? catalog.Info.Id : null;
    }

    /// <inheritdoc />
    public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) =>
        Providers.Select(provider => provider.GetMethodDocumentation(methodSpecifier)).FirstOrDefault(text => text is not null);

    /// <inheritdoc />
    public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) =>
        Providers.Select(provider => provider.GetMethodParameterDocumentation(methodSpecifier, parameterIndex))
            .FirstOrDefault(text => text is not null);

    /// <inheritdoc />
    public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) =>
        Providers.Select(provider => provider.GetMethodReturnDocumentation(methodSpecifier, returnIndex))
            .FirstOrDefault(text => text is not null);

    private bool HasCatalogs => Providers.Any(provider => provider is ITypeCatalog);

    private static ReflectionProviderMethodQuery RetargetMethods(ReflectionProviderMethodQuery query, TypeSpecifier type) => new()
    {
        Type = type,
        Static = query.Static,
        VisibleFrom = query.VisibleFrom,
        ReturnType = query.ReturnType,
        ArgumentType = query.ArgumentType,
        HasGenericArguments = query.HasGenericArguments,
    };

    private static ReflectionProviderVariableQuery RetargetVariables(ReflectionProviderVariableQuery query, TypeSpecifier type) => new()
    {
        Type = type,
        Static = query.Static,
        VisibleFrom = query.VisibleFrom,
        VariableType = query.VariableType,
        VariableTypeDerivesFrom = query.VariableTypeDerivesFrom,
    };

    private static bool Lists(ITypeCatalog catalog, TypeSpecifier type) =>
        catalog.GetNonStaticTypes().Contains(type)
        || catalog.GetMethods(new ReflectionProviderMethodQuery().WithType(type)).Any()
        || catalog.GetVariables(new ReflectionProviderVariableQuery().WithType(type)).Any()
        || catalog.GetConstructors(type).Any();

    private ITypeCatalog? CoveringCatalog(TypeSpecifier type)
    {
        string? assembly = Providers.OfType<ITypeAssemblyLocator>().Select(locator => locator.GetAssemblyName(type)).FirstOrDefault(name => name is not null);
        return assembly is null
            ? null
            : Providers.OfType<ITypeCatalog>().FirstOrDefault(catalog => catalog.Info.CoveredAssemblyNames.Contains(assembly, StringComparer.Ordinal));
    }

    // A type-scoped query: each declaring type answers from the catalog that covers its assembly, else from the providers (research R10).
    private List<T> Scoped<T, TQuery>(
        TypeSpecifier type,
        TQuery query,
        Func<IReflectionProvider, TQuery, IEnumerable<T>> run,
        Func<T, TypeSpecifier?> declaringType,
        Func<TQuery, TypeSpecifier, TQuery> retarget,
        IEqualityComparer<T>? comparer)
    {
        ITypeCatalog? covering = CoveringCatalog(type);
        if (covering is not null && !Lists(covering, type))
        {
            return [];
        }

        List<T> all = [.. Providers.SelectMany(provider => run(provider, query))];
        TypeSpecifier[] declaring = [.. all.Select(declaringType).OfType<TypeSpecifier>().Distinct()];
        if (covering is null && declaring.All(declared => CoveringCatalog(declared) is null))
        {
            return [.. all.Distinct(comparer)];
        }

        List<T> scoped = [];
        foreach (TypeSpecifier declared in new[] { type }.Concat(declaring).Distinct())
        {
            ITypeCatalog? catalog = declared == type ? covering : CoveringCatalog(declared);
            if (catalog is null)
            {
                scoped.AddRange(all.Where(item => declaringType(item) == declared));
            }
            else if (Lists(catalog, declared))
            {
                scoped.AddRange(run(catalog, retarget(query, declared)));
            }
        }

        return [.. scoped.Distinct(comparer)];
    }
}
