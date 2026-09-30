namespace Fixture;

/// <summary>Version 2 of the shared library: <see cref="Describe(string)"/> takes the caller's name.</summary>
public static class SharedLib
{
    /// <summary>Describes this version.</summary>
    /// <param name="caller">The caller's name.</param>
    /// <returns>The version marker.</returns>
    public static string Describe(string caller) => "shared-lib-v2:" + caller;
}
