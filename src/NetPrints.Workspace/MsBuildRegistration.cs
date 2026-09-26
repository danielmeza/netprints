#nullable enable
using System;
using System.Linq;
using Microsoft.Build.Locator;
using Microsoft.Extensions.Logging;

namespace NetPrints.Workspace;

/// <summary>
/// Registers the MSBuild assemblies <c>NetPrints.Workspace</c> loads types from (project-system.md
/// §4), using the same "newest Visual Studio instance, else the .NET SDK" logic as UnrealSharp's
/// editor integration (<c>UnrealSharp.Plugins.Main.TryRegisterMSBuild</c>). Must run before any
/// <c>Microsoft.Build</c>-namespace type is loaded — the Desktop and CLI entry points call this first
/// thing in <c>Main</c>, and test projects that touch MSBuild call it from a
/// <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/> method — because a type's
/// static constructor can trigger the load the moment the type is just-in-time compiled, even if the
/// method referencing it is never actually called.
/// </summary>
public static class MsBuildRegistration
{
    /// <summary>
    /// Ensures an MSBuild instance is registered with <see cref="MSBuildLocator"/>: the newest Visual
    /// Studio instance <see cref="MSBuildLocator.QueryVisualStudioInstances()"/> reports, or (when none
    /// is found) whatever <see cref="MSBuildLocator.RegisterDefaults"/> resolves, typically the
    /// currently active .NET SDK.
    /// </summary>
    /// <param name="logger">Logger a failed registration is reported to (event 4005).</param>
    /// <returns><see langword="true"/> if an MSBuild instance is registered, whether by this call or
    /// an earlier one (<see cref="MSBuildLocator.IsRegistered"/> is checked first, since
    /// <see cref="MSBuildLocator"/> does not support switching instances once registered);
    /// <see langword="false"/> if no Visual Studio instance or .NET SDK could be found.</returns>
    public static bool EnsureRegistered(ILogger logger)
    {
        if (MSBuildLocator.IsRegistered)
        {
            return true;
        }

        VisualStudioInstance? instance = MSBuildLocator.QueryVisualStudioInstances()
            .OrderByDescending(candidate => candidate.Version)
            .FirstOrDefault();

        if (instance is not null)
        {
            MSBuildLocator.RegisterInstance(instance);
            return true;
        }

        try
        {
            MSBuildLocator.RegisterDefaults();
            return true;
        }
        catch (InvalidOperationException)
        {
            Log.NoMsBuildInstanceFound(logger);
            return false;
        }
    }
}
