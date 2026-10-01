namespace Fixture;

/// <summary>Version 2 of the shared library: <see cref="Describe(string)"/> takes the caller's name.</summary>
public static class SharedLib
{
    /// <summary>Describes this version, as version 1 does: the call is source and binary compatible.</summary>
    /// <returns>The version marker.</returns>
    public static string Describe() => "shared-lib-v2";

    /// <summary>Describes this version.</summary>
    /// <param name="caller">The caller's name.</param>
    /// <returns>The version marker.</returns>
    public static string Describe(string caller) => "shared-lib-v2:" + caller;
}
