using System.Collections.Generic;

namespace NetPrints.Workspace.Run;

/// <summary>The build configurations the editor and <c>netprints run</c> offer.</summary>
public static class RunConfigurations
{
    /// <summary>The <c>Debug</c> configuration, the default.</summary>
    public const string Debug = "Debug";

    /// <summary>The <c>Release</c> configuration.</summary>
    public const string Release = "Release";

    /// <summary>Gets the configurations the editor lists, <see cref="Debug"/> first.</summary>
    public static IReadOnlyList<string> All { get; } = [Debug, Release];
}
