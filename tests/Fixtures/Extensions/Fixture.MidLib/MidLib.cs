namespace Fixture;

/// <summary>A private library of an extension whose own reference to the shared library is built against version 2.</summary>
public static class MidLib
{
    /// <summary>Describes the shared library this library calls.</summary>
    /// <returns>The shared library's marker for this library.</returns>
    public static string Describe() => SharedLib.Describe("mid");
}
