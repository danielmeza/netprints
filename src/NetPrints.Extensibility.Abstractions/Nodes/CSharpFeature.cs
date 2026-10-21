using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Extensibility.Nodes;

/// <summary>The id of a <see cref="CSharpFeature"/>, namespaced with the contributing extension's id.</summary>
/// <param name="Value">The id text.</param>
[Experimental(ExperimentalApiIds.CSharpFeatures, UrlFormat = ExperimentalApiIds.UrlFormat)]
public readonly record struct CSharpFeatureId(string Value)
{
    /// <inheritdoc/>
    public override string ToString() => Value;
}

/// <summary>
/// Whether a <see cref="CSharpFeature"/> can be used on a C# target, and why not (ADR-0038).
/// </summary>
/// <param name="IsAvailable">Whether the feature can be used.</param>
/// <param name="Reason">Why it cannot, or <see langword="null"/> when it can.</param>
/// <param name="Fixes">The ids of the quick fixes that would make it available; empty when none is known.</param>
[Experimental(ExperimentalApiIds.CSharpFeatures, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record Availability(bool IsAvailable, string? Reason, IReadOnlyList<string> Fixes)
{
    /// <summary>Gets the answer for a feature that can be used.</summary>
    public static Availability Available { get; } = new(true, null, []);

    /// <summary>Creates the answer for a feature that cannot be used.</summary>
    /// <param name="reason">Why the feature is unavailable.</param>
    /// <param name="fixes">The ids of the quick fixes that would make it available.</param>
    /// <returns>The unavailable answer.</returns>
    public static Availability Unavailable(string reason, params string[] fixes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new Availability(false, reason, fixes);
    }
}

/// <summary>
/// A C# feature a node search entry needs: the oldest language version that has it and the types it relies on. An entry that
/// <see cref="NodeSearchEntry.Requires"/> a feature is offered only on targets where <see cref="Check"/> says it is available.
/// </summary>
/// <param name="Id">The feature id.</param>
/// <param name="MinimumVersion">The oldest language version that has the feature.</param>
/// <param name="RequiredTypes">The full names of the types the feature relies on; empty when it needs none.</param>
[Experimental(ExperimentalApiIds.CSharpFeatures, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record CSharpFeature(CSharpFeatureId Id, CSharpLanguageVersion MinimumVersion, IReadOnlyList<string> RequiredTypes)
{
    /// <summary>Checks the feature against a target.</summary>
    /// <param name="target">The project's C# target.</param>
    /// <param name="typeExists">Tells whether a required type is known to the project; <see langword="null"/> skips the type check.</param>
    /// <returns>Whether the feature is available on <paramref name="target"/>, and why not.</returns>
    public Availability Check(CSharpTarget target, Func<string, bool>? typeExists = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!target.Check(MinimumVersion))
        {
            return Availability.Unavailable($"{Id} needs C# {MinimumVersion}; the project uses C# {target.Version}.");
        }

        string? missing = typeExists is null ? null : RequiredTypes.FirstOrDefault(type => !typeExists(type));
        return missing is null ? Availability.Available : Availability.Unavailable($"{Id} needs the type {missing}, which the project does not reference.");
    }
}
