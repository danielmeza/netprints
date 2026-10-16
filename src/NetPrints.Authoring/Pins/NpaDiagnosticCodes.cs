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

    /// <summary>A declared pin key is in neither <c>NodePins.Shipped.txt</c> nor <c>NodePins.Unshipped.txt</c>.</summary>
    public const string UnrecordedKey = "NPA010";

    /// <summary>A shipped pin key is no longer declared.</summary>
    public const string ShippedKeyGone = "NPA011";

    /// <summary>A node kind declares pins, but the project has neither <c>NodePins.Shipped.txt</c> nor <c>NodePins.Unshipped.txt</c>, so no key is tracked.</summary>
    public const string TrackingMissing = "NPA012";

    /// <summary>A convention resource key is missing from the neutral resx.</summary>
    public const string MissingResourceKey = "NPA040";

    /// <summary>A satellite resx has a key the neutral one lacks, or a placeholder count differs.</summary>
    public const string SatelliteMismatch = "NPA041";

    /// <summary>A resx key that no declaration uses.</summary>
    public const string UnusedResourceKey = "NPA042";

    /// <summary>The category of every NPA diagnostic.</summary>
    public const string Category = "Authoring";
}
