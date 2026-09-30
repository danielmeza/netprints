namespace FixtureDependent;

/// <summary>A type whose method takes a type of the fixture library, so cataloging it needs that assembly.</summary>
public class UsesFixture
{
    /// <summary>Uses a type of another assembly.</summary>
    /// <param name="value">The value.</param>
    public void Take(Fixture.Legacy.Old value)
    {
    }

    /// <summary>Uses nothing but the framework.</summary>
    /// <returns>One.</returns>
    public int Plain() => 1;
}
