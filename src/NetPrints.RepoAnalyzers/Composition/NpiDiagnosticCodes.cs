using Microsoft.CodeAnalysis;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>The diagnostic ids and descriptors of the registration generator (contracts/analyzer-diagnostics.md §5).</summary>
internal static class NpiDiagnosticCodes
{
    /// <summary>A singleton depends on a session-scoped service.</summary>
    public const string SingletonTakesSessionScoped = "NPI001";

    /// <summary>A service attribute on a type that cannot be registered.</summary>
    public const string InvalidRegistration = "NPI002";

    /// <summary>A registered type has no single constructor to call.</summary>
    public const string AmbiguousConstructor = "NPI003";

    /// <summary>Two registrations of one service where a consumer takes a single instance.</summary>
    public const string DuplicateService = "NPI004";

    /// <summary>A type registered for several services cannot share one instance.</summary>
    public const string SharedInstance = "NPI005";

    /// <summary>The category of the composition diagnostics.</summary>
    public const string Category = "Composition";

    /// <summary>Gets the descriptor of <see cref="SingletonTakesSessionScoped"/>.</summary>
    public static readonly DiagnosticDescriptor SingletonTakesSessionScopedRule = new(
        SingletonTakesSessionScoped,
        "A singleton must not depend on a session-scoped service",
        "{0} is a singleton but takes the session-scoped service {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(SingletonTakesSessionScoped));

    /// <summary>Gets the descriptor of <see cref="InvalidRegistration"/>.</summary>
    public static readonly DiagnosticDescriptor InvalidRegistrationRule = new(
        InvalidRegistration,
        "The type cannot be registered as the service",
        "Cannot register {0}: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(InvalidRegistration));

    /// <summary>Gets the descriptor of <see cref="AmbiguousConstructor"/>.</summary>
    public static readonly DiagnosticDescriptor AmbiguousConstructorRule = new(
        AmbiguousConstructor,
        "A registered type needs exactly one constructor to call",
        "{0} has {1}; mark one with [ActivatorUtilitiesConstructor]",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(AmbiguousConstructor));

    /// <summary>Gets the descriptor of <see cref="DuplicateService"/>.</summary>
    public static readonly DiagnosticDescriptor DuplicateServiceRule = new(
        DuplicateService,
        "A service is registered more than once where a consumer takes one instance",
        "{0} is registered more than once in the same scope and {1} takes a single instance of it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(DuplicateService));

    /// <summary>Gets the descriptor of <see cref="SharedInstance"/>.</summary>
    public static readonly DiagnosticDescriptor SharedInstanceRule = new(
        SharedInstance,
        "A type registered for several services must be able to share one instance",
        "{0} is registered for several services but {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(SharedInstance));

    /// <summary>Gets the descriptor with an id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <returns>The descriptor.</returns>
    public static DiagnosticDescriptor For(string id) =>
        id switch
        {
            SingletonTakesSessionScoped => SingletonTakesSessionScopedRule,
            InvalidRegistration => InvalidRegistrationRule,
            AmbiguousConstructor => AmbiguousConstructorRule,
            DuplicateService => DuplicateServiceRule,
            _ => SharedInstanceRule,
        };
}
