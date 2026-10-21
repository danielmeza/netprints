using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetPrints.Core;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// The host services every extension's own provider gets (ADR-0031 decision 3); nothing else of the host is reachable from it.
/// </summary>
[Experimental(ExperimentalApiIds.ExtensionServices, UrlFormat = ExperimentalApiIds.UrlFormat)]
public static class HostExports
{
    /// <summary>
    /// Gets the service types an extension's provider resolves besides its own registrations: <c>ILoggerFactory</c>, <c>ILogger&lt;T&gt;</c> for the extension's own types,
    /// <c>TimeProvider</c> and <c>IOptionsMonitor&lt;T&gt;</c> for the extension's own settings sections. Generic entries are the open generic definitions.
    /// </summary>
    public static IReadOnlyList<Type> AllowedServiceTypes { get; } = [typeof(ILoggerFactory), typeof(ILogger<>), typeof(TimeProvider), typeof(IOptionsMonitor<>)];
}
