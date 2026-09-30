using Microsoft.CodeAnalysis;
using NetPrints.Catalog;

namespace NetPrints.Annotations
{
    /// <summary>The descriptors of NPC001 to NPC006 (contracts/catalog.md section 3), tracked in <c>AnalyzerReleases.Unshipped.md</c>.</summary>
    internal static class GeneratorDiagnostics
    {
        public const string Category = "NetPrints.Catalog";

        public const string HelpLink = "https://danielmeza.github.io/netprints/docs/guide/catalogs#diagnostics";

        private const string MessageFormat = "{0}";

        public static readonly DiagnosticDescriptor UnreferencedAssembly = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.UnreferencedAssembly,
            "A catalog request names an assembly that is not referenced",
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor UnknownProfile = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.UnknownProfile,
            "Unknown catalog profile",
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor InvalidProfileFile = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.InvalidProfileFile,
            "Catalog profile file or catalog id is invalid",
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor IgnoredAnnotation = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.IgnoredAnnotation,
            "NetPrints annotation on a symbol that is not public",
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor SkippedMember = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.SkippedMemberMissingAssembly,
            "Catalog member skipped because an assembly is not available",
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static readonly DiagnosticDescriptor DuplicateCatalogId = new DiagnosticDescriptor(
            CatalogDiagnosticCodes.DuplicateBuildCatalogId,
            "Two catalogs have the same id",
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink);

        public static Diagnostic ToDiagnostic(DiagnosticModel model) =>
            Diagnostic.Create(DescriptorOf(model.Code), model.Location?.ToLocation() ?? Location.None, model.Message);

        private static DiagnosticDescriptor DescriptorOf(string code)
        {
            switch (code)
            {
                case CatalogDiagnosticCodes.UnreferencedAssembly:
                    return UnreferencedAssembly;
                case CatalogDiagnosticCodes.UnknownProfile:
                    return UnknownProfile;
                case CatalogDiagnosticCodes.InvalidProfileFile:
                    return InvalidProfileFile;
                case CatalogDiagnosticCodes.IgnoredAnnotation:
                    return IgnoredAnnotation;
                case CatalogDiagnosticCodes.SkippedMemberMissingAssembly:
                    return SkippedMember;
                default:
                    return DuplicateCatalogId;
            }
        }
    }
}
