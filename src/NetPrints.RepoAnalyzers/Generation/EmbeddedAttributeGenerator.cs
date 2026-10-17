using Microsoft.CodeAnalysis;

namespace NetPrints.RepoAnalyzers.Generation;

/// <summary>
/// Adds the compiler's <c>Embedded</c> attribute definition once for the internal generators, whose injected attributes carry it
/// so that a project never sees the same attribute of another assembly it has <c>InternalsVisibleTo</c> access to.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EmbeddedAttributeGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterPostInitializationOutput(static output => output.AddEmbeddedAttributeDefinition());
}
