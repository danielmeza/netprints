namespace NetPrints.Catalog;

/// <summary>The kind of a cataloged type. Written in lower case.</summary>
public enum CatalogTypeKind
{
    /// <summary>A class.</summary>
    Class,

    /// <summary>A struct.</summary>
    Struct,

    /// <summary>An interface.</summary>
    Interface,

    /// <summary>An enum.</summary>
    Enum,

    /// <summary>A delegate.</summary>
    Delegate,
}

/// <summary>The visibility of a cataloged member or accessor. Written in lower case.</summary>
public enum CatalogVisibility
{
    /// <summary>Visible everywhere.</summary>
    Public,

    /// <summary>Visible to derived types.</summary>
    Protected,
}

/// <summary>How a parameter is passed when it is not by value. Written in lower case.</summary>
public enum CatalogPassType
{
    /// <summary>By reference (<c>ref</c>).</summary>
    Reference,

    /// <summary>An output parameter (<c>out</c>).</summary>
    Out,

    /// <summary>A read-only reference (<c>in</c>).</summary>
    In,
}

/// <summary>Whether a variable is a property or a field. Written in lower case.</summary>
public enum CatalogVariableKind
{
    /// <summary>A property.</summary>
    Property,

    /// <summary>A field.</summary>
    Field,
}
