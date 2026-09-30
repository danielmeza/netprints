namespace Fixture;

/// <summary>Version 1 of the shared library: <see cref="Describe()"/> takes no argument.</summary>
public static class SharedLib
{
    /// <summary>Describes this version.</summary>
    /// <returns>The version marker.</returns>
    public static string Describe() => "shared-lib-v1";
}
