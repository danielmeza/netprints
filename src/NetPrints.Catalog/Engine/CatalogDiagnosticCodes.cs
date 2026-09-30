namespace NetPrints.Catalog;

/// <summary>The diagnostic codes of the catalog engine and readers (contracts/catalog.md §3).</summary>
public static class CatalogDiagnosticCodes
{
    /// <summary>NPC001, error: a catalog request names an assembly that is not referenced.</summary>
    public const string UnreferencedAssembly = "NPC001";

    /// <summary>NPC002, error: unknown profile id.</summary>
    public const string UnknownProfile = "NPC002";

    /// <summary>NPC003, error: profile file unreadable or invalid.</summary>
    public const string InvalidProfileFile = "NPC003";

    /// <summary>NPC004, warning: annotation on a member or type that is not public; ignored.</summary>
    public const string IgnoredAnnotation = "NPC004";

    /// <summary>NPC005, warning: member skipped because a type it uses comes from an assembly that is not available.</summary>
    public const string SkippedMemberMissingAssembly = "NPC005";

    /// <summary>NPC006, error: two catalogs with the same id in one build.</summary>
    public const string DuplicateBuildCatalogId = "NPC006";

    /// <summary>NPC007, warning: an annotated library is compiled without documentation comments, so its own catalog has no documentation.</summary>
    public const string MissingDocumentation = "NPC007";

    /// <summary>NPC101, error: the catalog <c>schemaVersion</c> is not supported by this reader.</summary>
    public const string UnsupportedSchemaVersion = "NPC101";

    /// <summary>NPC102, error: the catalog file is malformed.</summary>
    public const string MalformedCatalog = "NPC102";

    /// <summary>NPC103, warning: two loaded catalogs share an id; the later one is ignored.</summary>
    public const string DuplicateLoadedCatalogId = "NPC103";
}
