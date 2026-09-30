using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NetPrints.Catalog;

namespace NetPrints.Annotations
{
    /// <summary>
    /// Embeds NetPrints catalogs in the consuming assembly (contracts/annotations.md section 3): the annotated symbols of
    /// its own sources and the catalogs of referenced assemblies that <c>[assembly: NetPrintsCatalog]</c> requests. The
    /// attribute definitions are its post-initialization output.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class CatalogGenerator : IIncrementalGenerator
    {
        internal const string OwnStep = "OwnCatalog";

        internal const string ReferencedStep = "ReferencedCatalog";

        internal const string OutputsStep = "CatalogOutputs";

        private const string CatalogAttributeName = "NetPrints.Annotations.NetPrintsCatalogAttribute";

        private const string TypeAttributeName = "NetPrints.Annotations.NetPrintsTypeAttribute";

        private const string NodeAttributeName = "NetPrints.Annotations.NetPrintsNodeAttribute";

        private const string RootNamespaceProperty = "build_property.RootNamespace";

        private const string DocumentationMetadata = "build_metadata.AdditionalFiles.NetPrintsReferenceDocumentation";

        private const string ProfileFileSuffix = ".npprofile.json";

        private const string DocumentationFileSuffix = ".xml";

        private const string SelfHintName = "NetPrintsCatalog.Self.g.cs";

        private const string HintPrefix = "NetPrintsCatalog.";

        private const string HintSuffix = ".g.cs";

        private const string TrueText = "true";

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static output =>
            {
                output.AddEmbeddedAttributeDefinition();
                output.AddSource(AttributeSources.HintName, SourceText.From(AttributeSources.Source, Encoding.UTF8));
            });

            IncrementalValueProvider<string?> rootNamespace = context.AnalyzerConfigOptionsProvider
                .Select(static (options, _) => options.GlobalOptions.TryGetValue(RootNamespaceProperty, out string? value) ? value : null);

            IncrementalValueProvider<EquatableArray<AdditionalFileModel>> files = context.AdditionalTextsProvider
                .Combine(context.AnalyzerConfigOptionsProvider)
                .Select(static (pair, token) => ReadFile(pair.Left, pair.Right, token))
                .Where(static file => file is not null)
                .Collect()
                .Select(static (all, _) => new EquatableArray<AdditionalFileModel>(all.OfType<AdditionalFileModel>().OrderBy(file => file.Path, StringComparer.Ordinal).ToArray()));

            IncrementalValueProvider<BuildInputs> inputs = context.MetadataReferencesProvider
                .Collect()
                .Select(static (references, _) => new EquatableArray<MetadataReference>(references.ToArray()))
                .Combine(files)
                .Combine(rootNamespace)
                .Select(static (pair, _) => new BuildInputs(pair.Left.Left, pair.Left.Right, pair.Right));

            IncrementalValuesProvider<CatalogRequest> requests = context.SyntaxProvider
                .ForAttributeWithMetadataName(CatalogAttributeName, static (_, _) => true, static (attribute, token) => ReadRequests(attribute, token))
                .SelectMany(static (found, _) => found);

            IncrementalValuesProvider<CatalogOutput> referenced = requests
                .Combine(inputs)
                .Select(static (pair, token) => BuildReferenced(pair.Left, pair.Right, token))
                .WithTrackingName(ReferencedStep);

            IncrementalValueProvider<bool> hasAnnotations = HasAny(context, TypeAttributeName)
                .Combine(HasAny(context, NodeAttributeName))
                .Select(static (pair, _) => pair.Left || pair.Right);

            IncrementalValuesProvider<CatalogOutput> own = hasAnnotations
                .Combine(context.CompilationProvider)
                .Combine(rootNamespace)
                .Select(static (pair, token) => BuildOwn(pair.Left.Left, pair.Left.Right, pair.Right, token))
                .WithTrackingName(OwnStep)
                .SelectMany(static (output, _) => output);

            IncrementalValueProvider<(ImmutableArray<CatalogOutput> Own, ImmutableArray<CatalogOutput> Referenced)> outputs = own
                .Collect()
                .Combine(referenced.Collect())
                .Select(static (pair, _) => (pair.Left, pair.Right))
                .WithTrackingName(OutputsStep);

            context.RegisterSourceOutput(outputs, static (production, pair) => Emit(production, pair.Own, pair.Referenced));
        }

        private static IncrementalValueProvider<bool> HasAny(IncrementalGeneratorInitializationContext context, string attributeName) =>
            context.SyntaxProvider
                .ForAttributeWithMetadataName(attributeName, static (_, _) => true, static (_, _) => true)
                .Collect()
                .Select(static (found, _) => !found.IsDefaultOrEmpty);

        private static AdditionalFileModel? ReadFile(AdditionalText text, AnalyzerConfigOptionsProvider options, CancellationToken cancellationToken)
        {
            string fileName = FileNameOf(text.Path);
            bool isProfile = fileName.EndsWith(ProfileFileSuffix, StringComparison.OrdinalIgnoreCase);
            bool isDocumentation = fileName.EndsWith(DocumentationFileSuffix, StringComparison.OrdinalIgnoreCase)
                && options.GetOptions(text).TryGetValue(DocumentationMetadata, out string? flag)
                && string.Equals(flag, TrueText, StringComparison.OrdinalIgnoreCase);
            if (!isProfile && !isDocumentation)
            {
                return null;
            }

            string? content = text.GetText(cancellationToken)?.ToString();
            return content is null ? null : new AdditionalFileModel(text.Path, fileName, content, isProfile, isDocumentation);
        }

        private static string FileNameOf(string path)
        {
            int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
            return separator < 0 ? path : path.Substring(separator + 1);
        }

        private static EquatableArray<CatalogRequest> ReadRequests(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            List<CatalogRequest> requests = new List<CatalogRequest>();
            foreach (AttributeData attribute in context.Attributes)
            {
                if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not string assemblyName)
                {
                    continue;
                }

                string? id = null;
                string? profile = null;
                string? accessor = null;
                string[] include = Array.Empty<string>();
                string[] exclude = Array.Empty<string>();
                foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
                {
                    switch (argument.Key)
                    {
                        case "Id":
                            id = argument.Value.Value as string;
                            break;
                        case "Profile":
                            profile = argument.Value.Value as string;
                            break;
                        case "AccessorName":
                            accessor = argument.Value.Value as string;
                            break;
                        case "Include":
                            include = Strings(argument.Value);
                            break;
                        case "Exclude":
                            exclude = Strings(argument.Value);
                            break;
                    }
                }

                LocationInfo? location = attribute.ApplicationSyntaxReference is { } reference
                    ? LocationInfo.From(reference.GetSyntax(cancellationToken).GetLocation())
                    : null;
                requests.Add(new CatalogRequest(assemblyName, id, profile, new EquatableArray<string>(include), new EquatableArray<string>(exclude), accessor, location));
            }

            return new EquatableArray<CatalogRequest>(requests.ToArray());
        }

        private static string[] Strings(TypedConstant constant) =>
            constant.Kind == TypedConstantKind.Array && !constant.IsNull
                ? constant.Values.Select(value => value.Value as string).OfType<string>().ToArray()
                : Array.Empty<string>();

        private static CatalogOutput BuildReferenced(CatalogRequest request, BuildInputs inputs, CancellationToken cancellationToken)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "NetPrints.Catalog.Build",
                syntaxTrees: null,
                references: inputs.References.Items,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));

            IAssemblySymbol? assembly = compilation.SourceModule.ReferencedAssemblySymbols
                .Where(symbol => string.Equals(symbol.Name, request.AssemblyName, StringComparison.Ordinal))
                .OrderByDescending(symbol => symbol.Identity.Version)
                .FirstOrDefault();
            if (assembly is null)
            {
                return CatalogOutput.Failed(Error(CatalogDiagnosticCodes.UnreferencedAssembly, $"The assembly '{request.AssemblyName}' is not referenced.", request.Location));
            }

            if (!TryResolveProfile(request, inputs, out CatalogProfile? profile, out DiagnosticModel? failure) || profile is null)
            {
                return CatalogOutput.Failed(failure ?? Error(CatalogDiagnosticCodes.UnknownProfile, "The profile could not be resolved.", request.Location));
            }

            IEnumerable<string> documentation = inputs.Files.Items
                .Where(file => file.IsDocumentation && string.Equals(file.FileName, request.AssemblyName + DocumentationFileSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Text);
            cancellationToken.ThrowIfCancellationRequested();

            CatalogBuildResult result;
            try
            {
                result = CatalogBuilder.Build(compilation, new[] { assembly }, new CatalogProfileFilter(profile), new XmlDocumentationSource(documentation), new CatalogIdentity(request.Id));
            }
            catch (ArgumentException exception)
            {
                return CatalogOutput.Failed(Error(CatalogDiagnosticCodes.InvalidProfileFile, exception.Message, request.Location));
            }

            return Produce(result, request.AccessorName, inputs.RootNamespace, HintPrefix + result.Document.Id + HintSuffix, _ => request.Location);
        }

        private static EquatableArray<CatalogOutput> BuildOwn(bool hasAnnotations, Compilation compilation, string? rootNamespace, CancellationToken cancellationToken)
        {
            if (!hasAnnotations)
            {
                return default;
            }

            cancellationToken.ThrowIfCancellationRequested();
            CatalogIdentity identity = new CatalogIdentity(CatalogIdentity.DeriveId(compilation.AssemblyName ?? string.Empty), compilation.Assembly.Identity.Version.ToString());
            CatalogBuildResult result = CatalogBuilder.Build(
                compilation,
                new[] { compilation.Assembly },
                new CatalogProfileFilter(BuiltInCatalogProfiles.Annotated),
                new CompilationDocumentationSource(compilation),
                identity);
            return new EquatableArray<CatalogOutput>(new[] { Produce(result, null, rootNamespace, SelfHintName, source => SourceLocationOf(compilation, source)) });
        }

        private static CatalogOutput Produce(CatalogBuildResult result, string? accessorName, string? rootNamespace, string hintName, Func<string?, LocationInfo?> locate)
        {
            string accessor = accessorName is not null && CatalogCSharpEmitter.IsValidClassName(accessorName)
                ? accessorName
                : EmbeddedCatalogEmitter.DefaultAccessorName(result.Document.Id);
            DiagnosticModel[] diagnostics = result.Diagnostics
                .Select(diagnostic => new DiagnosticModel(
                    diagnostic.Code,
                    diagnostic.Severity == CatalogDiagnosticSeverity.Error,
                    diagnostic.Source is null ? diagnostic.Message : diagnostic.Source + ": " + diagnostic.Message,
                    locate(diagnostic.Source)))
                .ToArray();
            return new CatalogOutput(result.Document.Id, hintName, EmbeddedCatalogEmitter.Emit(result.Document, rootNamespace, accessor), new EquatableArray<DiagnosticModel>(diagnostics));
        }

        private static LocationInfo? SourceLocationOf(Compilation compilation, string? documentationId)
        {
            if (documentationId is null)
            {
                return null;
            }

            Location? location = DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationId, compilation)?.Locations.FirstOrDefault(candidate => candidate.IsInSource);
            return location is null ? null : LocationInfo.From(location);
        }

        private static bool TryResolveProfile(CatalogRequest request, BuildInputs inputs, out CatalogProfile? profile, out DiagnosticModel? failure)
        {
            failure = null;
            string name = request.Profile ?? CatalogProfile.PublicApiId;
            CatalogProfile? resolved = BuiltInCatalogProfiles.TryGet(name);
            if (resolved is null && name.EndsWith(ProfileFileSuffix, StringComparison.Ordinal))
            {
                AdditionalFileModel? file = inputs.Files.Items.FirstOrDefault(candidate => candidate.IsProfile && string.Equals(candidate.FileName, name, StringComparison.Ordinal));
                if (file is null)
                {
                    failure = Error(CatalogDiagnosticCodes.InvalidProfileFile, $"The profile file '{name}' is not an AdditionalFiles item.", request.Location);
                }
                else
                {
                    try
                    {
                        resolved = ProfileJson.Parse(file.Text);
                    }
                    catch (CatalogFormatException exception)
                    {
                        failure = Error(CatalogDiagnosticCodes.InvalidProfileFile, $"The profile file '{name}' is invalid: {exception.Message}", request.Location);
                    }
                }
            }
            else if (resolved is null)
            {
                failure = Error(CatalogDiagnosticCodes.UnknownProfile, $"Unknown profile '{name}'; use a built-in id or the file name of a *{ProfileFileSuffix} additional file.", request.Location);
            }

            profile = resolved is null ? null : Narrow(resolved, request);
            return profile is not null;
        }

        private static CatalogProfile Narrow(CatalogProfile profile, CatalogRequest request) =>
            request.Include.Length == 0 && request.Exclude.Length == 0
                ? profile
                : profile with
                {
                    IncludeTypes = (profile.IncludeTypes ?? Array.Empty<string>()).Concat(request.Include).ToArray(),
                    ExcludeTypes = (profile.ExcludeTypes ?? Array.Empty<string>()).Concat(request.Exclude).ToArray(),
                };

        private static DiagnosticModel Error(string code, string message, LocationInfo? location) => new DiagnosticModel(code, true, message, location);

        private static void Emit(SourceProductionContext production, ImmutableArray<CatalogOutput> own, ImmutableArray<CatalogOutput> referenced)
        {
            List<CatalogOutput> all = own.Concat(referenced).ToList();
            foreach (CatalogOutput output in all)
            {
                foreach (DiagnosticModel diagnostic in output.Diagnostics)
                {
                    production.ReportDiagnostic(GeneratorDiagnostics.ToDiagnostic(diagnostic));
                }
            }

            HashSet<string> duplicated = new HashSet<string>(
                all.Where(output => output.Succeeded)
                    .GroupBy(output => output.Id, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .OfType<string>(),
                StringComparer.Ordinal);
            foreach (string id in duplicated.OrderBy(id => id, StringComparer.Ordinal))
            {
                production.ReportDiagnostic(GeneratorDiagnostics.ToDiagnostic(Error(CatalogDiagnosticCodes.DuplicateBuildCatalogId, $"More than one catalog has the id '{id}'; none of them is emitted.", null)));
            }

            HashSet<string> hints = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogOutput output in all.OrderBy(output => output.HintName, StringComparer.Ordinal))
            {
                if (output.Source is { } source && output.HintName is { } hint && output.Id is { } id && !duplicated.Contains(id) && hints.Add(hint))
                {
                    production.AddSource(hint, SourceText.From(source, Encoding.UTF8));
                }
            }
        }
    }
}
