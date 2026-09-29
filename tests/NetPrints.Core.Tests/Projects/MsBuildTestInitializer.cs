using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Workspace;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// Registers MSBuild (project-system.md §4) once, before any test in this assembly runs. Required
    /// before any <c>Microsoft.Build</c>-namespace type is loaded (<see cref="MsBuildRegistration"/>'s
    /// own doc): a <see cref="ModuleInitializerAttribute"/> method is the one place guaranteed to run
    /// before that, regardless of which test class the runner happens to touch first.
    /// </summary>
    internal static class MsBuildTestInitializer
    {
        [ModuleInitializer]
        public static void EnsureMsBuildRegistered() => MsBuildRegistration.EnsureRegistered(NullLogger.Instance);
    }
}
