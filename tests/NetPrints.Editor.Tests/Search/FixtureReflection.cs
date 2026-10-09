using System.Collections.ObjectModel;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Search;

/// <summary>
/// A reflection host that answers from the shared runtime host but only with a small, fixed set of members, so the node search
/// goldens stay readable and do not move with the runtime. No catalog covers the types it keeps.
/// </summary>
public sealed class FixtureReflectionHost(IReflectionHost inner) : IReflectionHost
{
    private IReflectionProvider? provider;
    private IReflectionProvider? source;

    public bool IsLoaded => inner.IsLoaded;

    public Task Loaded => inner.Loaded;

    public IReflectionProvider Provider
    {
        get
        {
            if (provider is null || !ReferenceEquals(source, inner.Provider))
            {
                source = inner.Provider;
                provider = new FixtureProvider(source);
            }

            return provider;
        }
    }

    public ProjectSnapshot? Snapshot => inner.Snapshot;

    public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

    public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

    public Task ReloadAsync(Project project, CancellationToken cancellationToken = default) => inner.ReloadAsync(project, cancellationToken);

    public event EventHandler? Reloaded
    {
        add => inner.Reloaded += value;
        remove => inner.Reloaded -= value;
    }
}

/// <summary>The fixed member set of <see cref="FixtureReflectionHost"/>.</summary>
public sealed class FixtureProvider(IReflectionProvider inner) : IReflectionProvider
{
    private static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();
    private static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();
    private static readonly TypeSpecifier ObjectType = TypeSpecifier.FromType<object>();
    private static readonly TypeSpecifier ConsoleType = TypeSpecifier.FromType(typeof(Console));

    private static readonly TypeSpecifier[] Types = [ObjectType, StringType, IntType, TypeSpecifier.FromType<List<int>>()];

    private static bool Takes(MethodSpecifier method, params TypeSpecifier[] parameters) =>
        method.Parameters.Select(p => p.Value).SequenceEqual(parameters);

    private static bool Keep(MethodSpecifier method) =>
        (method.DeclaringType == ConsoleType && method.Name == "WriteLine" && (Takes(method) || Takes(method, StringType) || Takes(method, IntType)))
        || (method.DeclaringType == ConsoleType && method.Name == "ReadLine")
        || (method.DeclaringType == TypeSpecifier.FromType(typeof(Math)) && method.Name == "Max" && Takes(method, IntType, IntType))
        || (method.DeclaringType == StringType && method.Name is "ToUpperInvariant" or "IsNullOrEmpty")
        || (method.DeclaringType == ObjectType && method.Name is "ToString" or "GetHashCode");

    private static bool Keep(VariableSpecifier variable) =>
        (variable.DeclaringType == StringType && variable.Name is "Empty" or "Length")
        || (variable.DeclaringType == IntType && variable.Name == "MaxValue");

    public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b) => inner.TypeSpecifierIsSubclassOf(a, b);

    public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType) => inner.HasImplicitCast(fromType, toType);

    public IEnumerable<TypeSpecifier> GetNonStaticTypes() => Types;

    public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier) => inner.GetOverridableMethodsForType(typeSpecifier);

    public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier) => inner.GetPublicMethodOverloads(methodSpecifier);

    public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier) => inner.GetConstructors(typeSpecifier);

    public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier) => inner.GetEnumNames(typeSpecifier);

    public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query) => inner.GetMethods(query).Where(Keep);

    public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query) => inner.GetVariables(query).Where(Keep);

    public string? GetMethodDocumentation(MethodSpecifier methodSpecifier) => inner.GetMethodDocumentation(methodSpecifier);

    public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex) => inner.GetMethodParameterDocumentation(methodSpecifier, parameterIndex);

    public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex) => inner.GetMethodReturnDocumentation(methodSpecifier, returnIndex);
}
