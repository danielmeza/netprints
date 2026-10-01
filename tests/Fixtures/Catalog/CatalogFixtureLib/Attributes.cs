using System;

namespace Fixture.Attributes;

/// <summary>Flags read by the <c>fixture-flags</c> profile.</summary>
[Flags]
public enum ExposeFlags
{
    /// <summary>Nothing is exposed.</summary>
    None = 0,

    /// <summary>The member can be called.</summary>
    Callable = 1,

    /// <summary>The member can be read.</summary>
    Readable = 2,

    /// <summary>The member can be written.</summary>
    Writable = 4,
}

/// <summary>Marks a member for the <c>fixture-flags</c> profile.</summary>
/// <param name="flags">What the member exposes.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ExposeAttribute(ExposeFlags flags) : Attribute
{
    /// <summary>Gets what the member exposes.</summary>
    public ExposeFlags Flags { get; } = flags;
}
