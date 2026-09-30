using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Annotations
{
    /// <summary>Embeds NetPrints catalogs in the consuming assembly; the attribute definitions are its post-initialization output.</summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class CatalogGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static output =>
            {
                output.AddEmbeddedAttributeDefinition();
                output.AddSource(AttributeSources.HintName, SourceText.From(AttributeSources.Source, System.Text.Encoding.UTF8));
            });
        }
    }
}
