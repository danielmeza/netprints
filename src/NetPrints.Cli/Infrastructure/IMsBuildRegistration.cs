using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NetPrints.Workspace;

namespace NetPrints.Cli.Infrastructure;

/// <summary>The seam over <see cref="MsBuildRegistration"/>, so tests script the "no SDK" outcome.</summary>
internal interface IMsBuildRegistration
{
    /// <summary>Registers an MSBuild instance unless one is already registered.</summary>
    /// <param name="logger">Receives the registration failure.</param>
    /// <returns><see langword="true"/> when an MSBuild instance is registered; <see langword="false"/> when no SDK was found.</returns>
    bool EnsureRegistered(ILogger logger);
}

/// <summary>Registers MSBuild through <see cref="MsBuildRegistration"/>.</summary>
internal sealed class MsBuildRegistrationAdapter : IMsBuildRegistration
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool EnsureRegistered(ILogger logger) => MsBuildRegistration.EnsureRegistered(logger);
}
