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
public sealed class CompositeReflectionProvider : IReflectionProvider
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
    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query) =>
        Providers.SelectMany(provider => provider.GetMethods(query)).Distinct();

    /// <inheritdoc />
    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query) =>
        Providers.SelectMany(provider => provider.GetVariables(query)).Distinct(SpecifierComparers.Variables);

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
}
