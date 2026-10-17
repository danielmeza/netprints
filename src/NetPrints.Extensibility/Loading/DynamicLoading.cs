namespace NetPrints.Extensibility.Loading;

/// <summary>The reason text of the trimming annotations on the code that loads extensions and builds their services at run time.</summary>
internal static class DynamicLoading
{
    /// <summary>Extensions are assemblies found and loaded at run time, and their services are built by reflection over their types.</summary>
    public const string Reason = "Extensions are loaded from assemblies at run time and their services are created by reflection.";
}
