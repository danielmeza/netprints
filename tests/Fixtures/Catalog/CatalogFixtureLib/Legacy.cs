using System;

namespace Fixture.Legacy;

/// <summary>Obsolete members of every kind.</summary>
public class Old
{
    /// <summary>An obsolete method that still compiles.</summary>
    [Obsolete("Use NewName.")]
    public void OldName()
    {
    }

    /// <summary>An obsolete method whose use is an error.</summary>
    [Obsolete("Removed.", true)]
    public void Removed()
    {
    }

    /// <summary>The replacement.</summary>
    public void NewName()
    {
    }
}

/// <summary>An obsolete type.</summary>
[Obsolete]
public sealed class Ancient
{
    /// <summary>Gets a value.</summary>
    public int Value { get; }
}

/// <summary>An enum with an obsolete member and explicit values.</summary>
public enum Level
{
    /// <summary>Low.</summary>
    Low = 1,

    /// <summary>High.</summary>
    High = 10,

    /// <summary>Old alias for high.</summary>
    [Obsolete("Use High.")]
    Big = 10,
}
