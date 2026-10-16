namespace NetPrints.Authoring.Pins;

/// <summary>The diagnostic ids of the authoring pin analyzers (contracts/analyzer-diagnostics.md §3).</summary>
internal static class NpaDiagnosticCodes
{
    /// <summary>A pin property or its class is not partial, the property is not get-only, or its pin type does not match the attribute.</summary>
    public const string PinShape = "NPA001";

    /// <summary>Duplicate pin key in one collection, including base-class pins.</summary>
    public const string DuplicateKey = "NPA002";

    /// <summary>The constructor does not call <c>DeclarePins()</c> exactly once, or adds pins to a declared collection before it.</summary>
    public const string DeclarePinsCall = "NPA003";

    /// <summary>Invalid pin key: empty, or containing a dot, a slash or a tilde.</summary>
    public const string InvalidKey = "NPA004";

    /// <summary>A base class adds pins imperatively to a collection in which the convention declares pins.</summary>
    public const string BaseAddsPins = "NPA005";

    /// <summary>A <c>[DefaultValue]</c> does not convert to the pin's type.</summary>
    public const string DefaultValueType = "NPA006";

    /// <summary>The <c>[PinType]</c> member is missing or has the wrong type.</summary>
    public const string PinTypeMember = "NPA007";

    /// <summary>A pin property name does not end in <c>Pin</c> and has no <c>[Pin]</c> attribute.</summary>
    public const string NameWithoutSuffix = "NPA008";

    /// <summary>Pin properties in a class without <c>[NodeKind]</c> or <c>[NodeBase]</c>.</summary>
    public const string MissingMarker = "NPA009";

    /// <summary>The category of every NPA diagnostic.</summary>
    public const string Category = "Authoring";
}
