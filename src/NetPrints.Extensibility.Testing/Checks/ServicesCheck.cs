using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK013: the extension's service provider builds and validates, holds only the extension's own services, and releases scoped services with their session.</summary>
internal sealed class ServicesCheck : ConformanceCheck
{
    private const string ProviderRule = "the service provider builds, validates and holds only the extension's own services";
    private const string LeakRule = "a scoped or transient service is released with its session";
    private const string RejectedPrefix = "service ";
    private const string BuildFailureMarker = "service registrations";

    public override string Code => NpkDiagnosticCodes.Services;

    public override string Name => "Service providers";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.SubjectId is not { } id)
        {
            return Task.FromResult(CheckOutcome.Skip("no extension besides the built-in one was loaded"));
        }

        ExtensionRegistry registry = context.Harness.Registry;
        var findings = new List<ConformanceFinding>();
        foreach (ExtensionLoadResult.Failed failure in registry.Results.OfType<ExtensionLoadResult.Failed>().Where(result => result.Id == id && IsBuildFailure(result)))
        {
            findings.Add(Finding(ProviderRule, $"{failure.Code} {failure.Reason}"));
        }

        foreach (ExtensionContributionIssue issue in registry.Issues.Where(issue => issue.ExtensionId == id && IsRejectedService(issue)))
        {
            findings.Add(Finding(ProviderRule, $"{issue.Code} {issue.Contribution}: {issue.Reason}"));
        }

        foreach (ServiceDescriptor descriptor in registry.ServiceDescriptorsOf(id))
        {
            if (descriptor.Lifetime == ServiceLifetime.Singleton || descriptor.IsKeyedService || descriptor.ServiceType.ContainsGenericParameters)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            (WeakReference? instance, string? problem) = ResolveAndRelease(registry, id, descriptor.ServiceType);
            if (problem is not null)
            {
                findings.Add(Finding(LeakRule, $"{descriptor.ServiceType.Name}: {problem}"));
                continue;
            }

            if (instance is not null && IsStillAlive(instance))
            {
                findings.Add(Finding(LeakRule, $"{descriptor.ServiceType.Name} ({descriptor.Lifetime}) is still reachable after its session is disposed"));
            }
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }

    public static bool IsBuildFailure(ExtensionLoadResult.Failed failure) => failure.Reason.Contains(BuildFailureMarker, StringComparison.Ordinal);

    public static bool IsRejectedService(ExtensionContributionIssue issue) => issue.Contribution.StartsWith(RejectedPrefix, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference? Instance, string? Problem) ResolveAndRelease(ExtensionRegistry registry, string id, Type serviceType)
    {
        using IServiceScope scope = registry.OpenSession(id);
        try
        {
            object? instance = scope.ServiceProvider.GetService(serviceType);
            return (instance is null ? null : new WeakReference(instance), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return (null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsStillAlive(WeakReference instance)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return instance.IsAlive;
    }
}
