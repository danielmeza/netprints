using NetPrints.RepoAnalyzers.Generation;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>How long a registered service lives.</summary>
internal enum RegistrationLifetime
{
    /// <summary>One instance for the container.</summary>
    Singleton,

    /// <summary>One instance for each project session.</summary>
    SessionScoped,
}

/// <summary>How the generated code supplies a constructor or factory parameter.</summary>
internal enum DependencyKind
{
    /// <summary>Resolved from the provider.</summary>
    Service,

    /// <summary>Resolved from the provider when the service is registered here, else the default value is passed.</summary>
    Optional,

    /// <summary>Every registration of the service.</summary>
    Enumerable,

    /// <summary>The provider itself.</summary>
    Provider,

    /// <summary>An <c>ILogger&lt;T&gt;</c> created by the logger factory.</summary>
    Logger,

    /// <summary>A non-generic <c>ILogger</c> whose category is the registered type.</summary>
    CategoryLogger,
}

/// <summary>A parameter of a constructor or factory method.</summary>
/// <param name="Kind">How it is supplied.</param>
/// <param name="Type">The fully qualified type (the element type for a sequence, the category type for a logger).</param>
/// <param name="Detail">The default value literal of an optional parameter, or the logger category.</param>
internal sealed record Dependency(DependencyKind Kind, string Type, string Detail);

/// <summary>A constructor call or a static factory call and its parameters.</summary>
/// <param name="Type">The fully qualified type that is constructed or that holds the factory.</param>
/// <param name="Factory">The factory method name, or an empty string for a constructor.</param>
/// <param name="Arguments">The parameters in order.</param>
internal sealed record Creation(string Type, string Factory, EquatableArray<Dependency> Arguments);

/// <summary>A diagnostic found while reading, held until the output step.</summary>
/// <param name="Id">The diagnostic id.</param>
/// <param name="Location">Where to report it.</param>
/// <param name="First">The first message argument.</param>
/// <param name="Second">The second message argument.</param>
internal sealed record DiagnosticModel(string Id, LocationInfo Location, string First, string Second);

/// <summary>One service attribute on a type.</summary>
/// <param name="Service">The fully qualified service type.</param>
/// <param name="Implementation">The fully qualified implementation type.</param>
/// <param name="Lifetime">The lifetime.</param>
/// <param name="IsDisposable">Whether the implementation is disposable.</param>
/// <param name="Creation">How to create it, or null when the registration is invalid.</param>
/// <param name="Location">The attribute.</param>
/// <param name="Diagnostics">The problems found while reading it.</param>
internal sealed record Registration(
    string Service,
    string Implementation,
    RegistrationLifetime Lifetime,
    bool IsDisposable,
    Creation? Creation,
    LocationInfo Location,
    EquatableArray<DiagnosticModel> Diagnostics);

/// <summary>A parameter of a scope factory method.</summary>
/// <param name="Type">The fully qualified type.</param>
/// <param name="Name">The parameter name.</param>
internal sealed record MethodParameter(string Type, string Name);

/// <summary>A partial <c>Create</c> method of a scope factory.</summary>
/// <param name="Modifiers">The modifiers of the declaration without <c>partial</c>.</param>
/// <param name="ReturnType">The fully qualified return type.</param>
/// <param name="Name">The method name.</param>
/// <param name="Parameters">The parameters.</param>
/// <param name="ReturnCreation">How to construct the return type when it is a constructible class, else null.</param>
/// <param name="Location">The method name.</param>
internal sealed record FactoryMethod(
    string Modifiers,
    string ReturnType,
    string Name,
    EquatableArray<MethodParameter> Parameters,
    Creation? ReturnCreation,
    LocationInfo Location);

/// <summary>A partial class marked <c>[ScopeFactory]</c>.</summary>
/// <param name="Namespace">The containing namespace, or an empty string.</param>
/// <param name="Name">The class name.</param>
/// <param name="Methods">The partial <c>Create</c> methods without a body.</param>
internal sealed record ScopeFactoryModel(string Namespace, string Name, EquatableArray<FactoryMethod> Methods);
