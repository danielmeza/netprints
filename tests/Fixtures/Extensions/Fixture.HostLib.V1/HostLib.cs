namespace Fixture;

/// <summary>Version 1 of a library the test host loads itself, so that an extension built against version 2 meets an older host copy.</summary>
public static class HostLib
{
    /// <summary>Describes this version.</summary>
    /// <returns>The version marker.</returns>
    public static string Describe() => "host-lib-v1";
}
