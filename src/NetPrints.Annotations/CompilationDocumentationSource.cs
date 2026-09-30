using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using NetPrints.Catalog;

namespace NetPrints.Annotations
{
    /// <summary>An <see cref="IDocumentationSource"/> over the documentation comments of a compilation's own sources, resolved lazily by documentation id.</summary>
    internal sealed class CompilationDocumentationSource : IDocumentationSource
    {
        private readonly Compilation compilation;

        private readonly Dictionary<string, XmlDocumentationSource?> cache = new Dictionary<string, XmlDocumentationSource?>(StringComparer.Ordinal);

        public CompilationDocumentationSource(Compilation compilation)
        {
            this.compilation = compilation;
        }

        public string? GetSummary(string documentationId) => Documentation(documentationId)?.GetSummary(documentationId);

        public string? GetParameter(string documentationId, string parameterName) => Documentation(documentationId)?.GetParameter(documentationId, parameterName);

        public string? GetReturns(string documentationId) => Documentation(documentationId)?.GetReturns(documentationId);

        private XmlDocumentationSource? Documentation(string documentationId)
        {
            if (cache.TryGetValue(documentationId, out XmlDocumentationSource? known))
            {
                return known;
            }

            XmlDocumentationSource? source = null;
            string xml = DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationId, compilation)?.GetDocumentationCommentXml() ?? string.Empty;
            if (xml.Length > 0)
            {
                source = XmlDocumentationSource.FromText("<doc><members>" + xml + "</members></doc>");
            }

            cache.Add(documentationId, source);
            return source;
        }
    }
}
