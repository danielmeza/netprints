using Microsoft.CodeAnalysis;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.SmartEnums;

/// <summary>The diagnostic ids and descriptors of the smart enum generator (contracts/analyzer-diagnostics.md §5).</summary>
internal static class SmartEnumDiagnosticCodes
{
    /// <summary>A smart enum type that the generator cannot extend.</summary>
    public const string Shape = "NPI010";

    /// <summary>A smart enum member that is not a static readonly field, or a lookup that reads <c>All</c> too early.</summary>
    public const string Member = "NPI011";

    /// <summary>The category of the smart enum diagnostics.</summary>
    public const string Category = "SmartEnum";

    /// <summary>Gets the descriptor of <see cref="Shape"/>.</summary>
    public static readonly DiagnosticDescriptor ShapeRule = new(
        Shape,
        "A smart enum must be a top-level abstract partial class",
        "{0} is marked [SmartEnum] but is not a top-level abstract partial class",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(Shape));

    /// <summary>Gets the descriptor of <see cref="Member"/>.</summary>
    public static readonly DiagnosticDescriptor MemberRule = new(
        Member,
        "A smart enum member must be a static readonly field, declared before any lookup that reads All",
        "{0}: {1}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: DiagnosticHelpLinks.For(Member));
}
