namespace NetPrintsFixture.Runtime;

/// <summary>A private dependency whose name starts with the host's own prefix.</summary>
public static class Helper
{
    /// <summary>Names this helper.</summary>
    /// <returns>The marker.</returns>
    public static string Name() => "netprints-fixture-runtime";
}
