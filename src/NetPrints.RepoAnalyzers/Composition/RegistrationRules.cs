using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>NPI001, NPI004 and NPI005: the rules that need every registration of the compilation.</summary>
internal static class RegistrationRules
{
    private const string DisposableReason = "it is disposable and the container would dispose the one instance once for each registration";

    private const string LifetimeReason = "the registrations have different lifetimes";

    /// <summary>Gets the reason several registrations of one type cannot share an instance, or null when they can.</summary>
    /// <param name="group">The registrations of one implementation type, more than one.</param>
    /// <returns>The reason, or null.</returns>
    public static string? SharedProblem(IReadOnlyList<Registration> group)
    {
        if (group.Any(registration => registration.IsDisposable))
        {
            return DisposableReason;
        }

        return group.Select(registration => registration.Lifetime).Distinct().Count() > 1 ? LifetimeReason : null;
    }

    /// <summary>Groups the registrations by implementation type, each group in source order.</summary>
    /// <param name="registrations">The registrations.</param>
    /// <returns>The groups.</returns>
    public static IEnumerable<IReadOnlyList<Registration>> ByImplementation(IReadOnlyList<Registration> registrations) =>
        registrations
            .GroupBy(registration => registration.Implementation, StringComparer.Ordinal)
            .Select(group => (IReadOnlyList<Registration>)[.. group.OrderBy(registration => registration.Location.FilePath, StringComparer.Ordinal).ThenBy(registration => registration.Location.Span.Start)])
            .OrderBy(group => group[0].Implementation, StringComparer.Ordinal);

    /// <summary>Finds the problems that span registrations.</summary>
    /// <param name="registrations">Every registration of the compilation.</param>
    /// <returns>The diagnostics to report.</returns>
    public static IReadOnlyList<DiagnosticModel> Check(IReadOnlyList<Registration> registrations)
    {
        List<DiagnosticModel> found = [];
        Registration[] valid = [.. registrations.Where(registration => registration.Creation is not null)];
        found.AddRange(SingletonsTakingSessionScoped(valid));
        found.AddRange(DuplicateServices(valid));
        found.AddRange(SharedInstances(registrations));
        return found;
    }

    private static IEnumerable<DiagnosticModel> SingletonsTakingSessionScoped(Registration[] valid)
    {
        HashSet<string> sessionScoped = [.. valid.Where(registration => registration.Lifetime == RegistrationLifetime.SessionScoped).Select(registration => registration.Service)];
        foreach (Registration singleton in valid.Where(registration => registration.Lifetime == RegistrationLifetime.Singleton))
        {
            foreach (Dependency dependency in Taken(singleton).Where(dependency => sessionScoped.Contains(dependency.Type)))
            {
                yield return new DiagnosticModel(NpiDiagnosticCodes.SingletonTakesSessionScoped, singleton.Location, singleton.Implementation, dependency.Type);
            }
        }
    }

    private static IEnumerable<DiagnosticModel> DuplicateServices(Registration[] valid)
    {
        IEnumerable<IGrouping<(string Service, RegistrationLifetime Lifetime), Registration>> groups = valid
            .GroupBy(registration => (registration.Service, registration.Lifetime))
            .Where(group => group.Count() > 1);

        foreach (IGrouping<(string Service, RegistrationLifetime Lifetime), Registration> group in groups)
        {
            Registration? consumer = valid.FirstOrDefault(registration => Taken(registration).Any(dependency => dependency.Kind != DependencyKind.Enumerable && dependency.Type == group.Key.Service));
            if (consumer is null)
            {
                continue;
            }

            foreach (Registration duplicate in group.OrderBy(registration => registration.Location.FilePath, StringComparer.Ordinal).ThenBy(registration => registration.Location.Span.Start).Skip(1))
            {
                yield return new DiagnosticModel(NpiDiagnosticCodes.DuplicateService, duplicate.Location, group.Key.Service, consumer.Implementation);
            }
        }
    }

    private static IEnumerable<DiagnosticModel> SharedInstances(IReadOnlyList<Registration> registrations)
    {
        foreach (IReadOnlyList<Registration> group in ByImplementation(registrations).Where(group => group.Count > 1))
        {
            string? problem = SharedProblem(group);
            if (problem is null)
            {
                continue;
            }

            foreach (Registration other in group.Skip(1))
            {
                yield return new DiagnosticModel(NpiDiagnosticCodes.SharedInstance, other.Location, other.Implementation, problem);
            }
        }
    }

    private static IEnumerable<Dependency> Taken(Registration registration) =>
        registration.Creation?.Arguments.Where(dependency => dependency.Kind is DependencyKind.Service or DependencyKind.Optional or DependencyKind.Enumerable) ?? [];
}
