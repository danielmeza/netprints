using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Reflection
{
    /// <summary>
    /// Per-provider cache of the members of type symbols. Owned by one <see cref="ReflectionProvider"/>,
    /// so it lives and dies with that provider's compilation and nothing is shared between providers
    /// (the earlier static cache was process-wide state).
    /// </summary>
    public sealed class MemberCache
    {
        internal ConditionalWeakTable<ITypeSymbol, List<ISymbol>> Members { get; } = new ConditionalWeakTable<ITypeSymbol, List<ISymbol>>();
    }

    /// <summary>
    /// Roslyn symbol helpers used by <see cref="ReflectionProvider"/>: member enumeration (cached
    /// per <see cref="MemberCache"/>), accessibility and subclass checks, and full type names.
    /// </summary>
    public static class ISymbolExtensions
    {

        /// <summary>
        /// Gets all members of a symbol including inherited ones, but not overriden ones.
        /// </summary>
        public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol symbol, MemberCache cache)
        {
            if (cache.Members.TryGetValue(symbol, out var allMembers))
            {
                return allMembers;
            }

            var members = new List<ISymbol>();
            var overridenMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            var startSymbol = symbol;
            ITypeSymbol? current = symbol;

            while (current != null)
            {
                var symbolMembers = current.GetMembers();

                // Add symbols which weren't overriden yet
                List<ISymbol> newMembers = symbolMembers.Where(m => !(m is IMethodSymbol methodSymbol) || !overridenMethods.Contains(methodSymbol)).ToList();

                members.AddRange(newMembers);

                // Recursively add overriden methods
                List<IMethodSymbol> newOverridenMethods = symbolMembers.OfType<IMethodSymbol>().ToList();
                while (newOverridenMethods.Count > 0)
                {
                    newOverridenMethods.ForEach(m => overridenMethods.Add(m));
                    newOverridenMethods = newOverridenMethods
                        .Select(m => m.OverriddenMethod)
                        .OfType<IMethodSymbol>()
                        .ToList();
                }

                current = current.BaseType;
            }

            cache.Members.AddOrUpdate(startSymbol, members.ToList());

            return members;
        }

        /// <summary>
        /// Returns whether <paramref name="symbol"/> is declared <c>public</c>.
        /// </summary>
        /// <param name="symbol">Symbol to check.</param>
        /// <returns><see langword="true"/> if the symbol's declared accessibility is public.</returns>
        public static bool IsPublic(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public;
        }

        /// <summary>
        /// Returns whether <paramref name="symbol"/> is declared <c>protected</c> (exactly; not
        /// <c>protected internal</c> or <c>private protected</c>).
        /// </summary>
        /// <param name="symbol">Symbol to check.</param>
        /// <returns><see langword="true"/> if the symbol's declared accessibility is protected.</returns>
        public static bool IsProtected(this ISymbol symbol)
        {
            return symbol.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Protected;
        }

        /// <summary>
        /// Returns <paramref name="symbol"/>'s ordinary and operator methods (see
        /// <see cref="GetAllMembers"/> for what "all" includes), excluding conversion operators (see
        /// <see cref="GetConverters"/>).
        /// </summary>
        /// <param name="symbol">Type to get methods for.</param>
        /// <param name="cache">Member cache to resolve <paramref name="symbol"/>'s members through.</param>
        /// <returns>The type's ordinary and operator methods.</returns>
        public static IEnumerable<IMethodSymbol> GetMethods(this ITypeSymbol symbol, MemberCache cache)
        {
            return symbol.GetAllMembers(cache)
                    .Where(member => member.Kind == SymbolKind.Method)
                    .Cast<IMethodSymbol>()
                    .Where(method => method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.BuiltinOperator || method.MethodKind == MethodKind.UserDefinedOperator);
        }

        /// <summary>
        /// Returns <paramref name="symbol"/>'s user-defined conversion operators (implicit and explicit).
        /// </summary>
        /// <param name="symbol">Type to get conversion operators for.</param>
        /// <param name="cache">Member cache to resolve <paramref name="symbol"/>'s members through.</param>
        /// <returns>The type's conversion operators.</returns>
        public static IEnumerable<IMethodSymbol> GetConverters(this ITypeSymbol symbol, MemberCache cache)
        {
            return symbol.GetAllMembers(cache)
                    .Where(member => member.Kind == SymbolKind.Method)
                    .Cast<IMethodSymbol>()
                    .Where(method => method.MethodKind == MethodKind.Conversion);
        }

        /// <summary>
        /// Returns whether <paramref name="symbol"/> derives from, or (when <paramref name="cls"/> is
        /// an interface) implements, <paramref name="cls"/>.
        /// </summary>
        /// <param name="symbol">Candidate subclass, or <see langword="null"/> (returns <see langword="false"/>).</param>
        /// <param name="cls">Candidate base class or interface, or <see langword="null"/> (returns <see langword="false"/>).</param>
        /// <returns><see langword="true"/> if <paramref name="symbol"/> derives from or implements <paramref name="cls"/>.</returns>
        public static bool IsSubclassOf(this ITypeSymbol? symbol, ITypeSymbol? cls)
        {
            if (symbol is null || cls is null)
            {
                return false;
            }

            // If cls is an interface type, check if the interface is implemented
            // TODO: Currently only checking full name and type parameter count for interfaces.
            if (cls.TypeKind == TypeKind.Interface && cls is INamedTypeSymbol namedCls)
            {
                bool IsSameInterface(INamedTypeSymbol a, INamedTypeSymbol b)
                {
                    return a.GetFullName() == b.GetFullName() && a.TypeParameters.Length == b.TypeParameters.Length;
                }

                return (symbol is INamedTypeSymbol namedSymbol && IsSameInterface(namedSymbol, namedCls))
                    || symbol.AllInterfaces.Any(interf =>
                        cls is INamedTypeSymbol namedCls && IsSameInterface(interf, namedCls));
            }

            // Traverse base types to find out if symbol inherits from cls
            ITypeSymbol? candidateBaseType = symbol;
            while (candidateBaseType != null)
            {
                // Identity, not SymbolEqualityComparer: see implementation-notes.md "T013" (changes search results).
                if (ReferenceEqualityComparer.Instance.Equals(candidateBaseType, cls))
                {
                    return true;
                }

                candidateBaseType = candidateBaseType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Returns <paramref name="typeSymbol"/>'s metadata name, prefixed with its containing
        /// namespace's metadata name (dot-separated) unless it is in the global namespace.
        /// </summary>
        /// <param name="typeSymbol">Type to get the full name of.</param>
        /// <returns>The type's namespace-qualified metadata name.</returns>
        public static string GetFullName(this ITypeSymbol typeSymbol)
        {
            string fullName = typeSymbol.MetadataName;
            if (typeSymbol.ContainingNamespace != null && !typeSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                fullName = $"{typeSymbol.ContainingNamespace.MetadataName}.{fullName}";
            }
            return fullName;
        }
    }

    /// <summary>
    /// <see cref="IReflectionProvider"/> backed by a Roslyn <see cref="CSharpCompilation"/> built from
    /// the given assemblies, source files and in-memory sources. See <see cref="MemoizedReflectionProvider"/>
    /// for a caching wrapper around repeated queries.
    /// </summary>
    public class ReflectionProvider : IReflectionProvider
    {
        private readonly MemberCache memberCache = new MemberCache();
        private readonly CSharpCompilation compilation;
        private readonly DocumentationUtil documentationUtil;
        private readonly List<IMethodSymbol> extensionMethods;
        private readonly IReadOnlySet<string> excludedAssemblyNames;

        private static (EmitResult, Stream) CompileInMemory(CSharpCompilation compilation)
        {
            Stream stream = new MemoryStream();
            var compilationResults = compilation.Emit(stream);
            stream.Seek(0, SeekOrigin.Begin);
            return (compilationResults, stream);
        }

        private static SyntaxTree ParseSyntaxTree(string source)
        {
            // LanguageVersion.Preview is not defined in the Roslyn version used
            // at the time of writing. However MaxValue - 1 (as defined in the newer versions
            // see https://github.com/dotnet/roslyn/blob/472276accaf70a8356747dc7111cfb6231871077/src/Compilers/CSharp/Portable/LanguageVersion.cs#L135
            // seems to work.
            const LanguageVersion previewVersion = (LanguageVersion)(int.MaxValue - 1);

            // Return a syntax tree of our source code
            return CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion: previewVersion));
        }

        /// <summary>
        /// Creates a ReflectionProvider from resolved assemblies and source files.
        /// </summary>
        /// <param name="assemblies">Assemblies to reference, resolved by <c>IProjectSystem.LoadAsync</c>
        /// (project-system.md §4), each carrying its own documentation file path if one exists. A path
        /// that does not exist is skipped instead of throwing; callers resolve references with
        /// <see cref="ReferenceAssemblyResolver"/> or <c>IProjectSystem</c> first (FR-009).</param>
        /// <param name="sources">C# source files to compile alongside <paramref name="assemblies"/>
        /// (a project's generated classes and other <c>Compile</c> items).</param>
        /// <param name="excludedAssemblyNames">Simple names of assemblies (typically ones a type
        /// catalog already covers, extension-points.md §4) whose types are skipped by every
        /// enumeration (<see cref="GetNonStaticTypes"/>, the untyped queries of <see cref="GetMethods"/>
        /// and <see cref="GetVariables"/>); the assemblies stay referenced so user sources still bind
        /// against their types.</param>
        public ReflectionProvider(IReadOnlyList<ResolvedAssembly> assemblies, IReadOnlyList<SourceFile> sources, IReadOnlySet<string> excludedAssemblyNames)
        {
            ArgumentNullException.ThrowIfNull(assemblies);
            ArgumentNullException.ThrowIfNull(sources);
            this.excludedAssemblyNames = excludedAssemblyNames ?? throw new ArgumentNullException(nameof(excludedAssemblyNames));

            // Assemblies whose file does not exist are skipped instead of throwing; callers resolve
            // references with ReferenceAssemblyResolver or IProjectSystem first (FR-009).
            List<ResolvedAssembly> existingAssemblies = assemblies.Where(a => File.Exists(a.Path)).ToList();
            var documentationPaths = new Dictionary<string, string>();
            foreach (ResolvedAssembly assembly in existingAssemblies)
            {
                if (assembly.DocumentationPath is { } docPath)
                {
                    documentationPaths[assembly.Path] = docPath;
                }
            }

            var assemblyReferences = existingAssemblies
                .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Path))
                .ToList();

            // Create syntax trees from sources
            var syntaxTrees = sources.Select(source => source.Text).Distinct().Select(text => ParseSyntaxTree(text));

            compilation = CSharpCompilation.Create("C", syntaxTrees, assemblyReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Try to compile, on success create a new compilation that references the created assembly instead of the sources.
            // The compilation will fail eg. if the sources have references to the not-yet-compiled assembly.
            (EmitResult compilationResults, Stream stream) = CompileInMemory(compilation);

            if (compilationResults.Success)
            {
                assemblyReferences.Add(MetadataReference.CreateFromStream(stream));
                compilation = CSharpCompilation.Create("C", references: assemblyReferences);
            }

            extensionMethods = new List<IMethodSymbol>(GetValidTypes().SelectMany(t => t.GetMethods(memberCache).Where(m => m.IsExtensionMethod)));

            documentationUtil = new DocumentationUtil(compilation, documentationPaths);
        }

        /// <summary>
        /// Gets all classes declared in the compilation's syntax trees.
        /// Useful for when they can not be compiled into assemblies because
        /// of errors and we still want their symbols.
        /// </summary>
        private IEnumerable<INamedTypeSymbol> GetSyntaxTreeTypes()
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var model = compilation.GetSemanticModel(syntaxTree, true);
                var classSyntaxes = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                // OfType also drops the (unexpected, for a class syntax node of this model) null case.
                var classes = classSyntaxes.Select(syntax => model.GetDeclaredSymbol(syntax)).OfType<INamedTypeSymbol>();
                foreach (var cls in classes)
                {
                    yield return cls;
                }
            }
        }

        private IEnumerable<INamedTypeSymbol> GetTypeNestedTypes(INamedTypeSymbol typeSymbol)
        {
            var typeMembers = typeSymbol.GetTypeMembers();
            return typeMembers.Concat(typeMembers.SelectMany(t => GetTypeNestedTypes(t)));
        }

        private IEnumerable<INamedTypeSymbol> GetNamespaceTypes(INamespaceSymbol namespaceSymbol)
        {
            IEnumerable<INamedTypeSymbol> types = namespaceSymbol.GetTypeMembers();
            types = types.Concat(types.SelectMany(t => GetTypeNestedTypes(t)));
            return types.Concat(namespaceSymbol.GetNamespaceMembers().SelectMany(ns => GetNamespaceTypes(ns)));
        }

        /// <summary>
        /// Types offered by every enumeration query, excluding the excluded assemblies' (the
        /// constructor's own doc comment): a specific, already-known type is still resolved by name
        /// (<see cref="GetValidTypes(string)"/>, via <see cref="GetTypeFromSpecifier(TypeSpecifier)"/>)
        /// regardless of exclusion, so its members bind correctly even when it does not show up in a
        /// search.
        /// </summary>
        private IEnumerable<INamedTypeSymbol> GetValidTypes()
        {
            return compilation.SourceModule.ReferencedAssemblySymbols
                .Where(module => !excludedAssemblyNames.Contains(module.Name))
                .SelectMany(module => GetNamespaceTypes(module.GlobalNamespace))
                .Concat(GetSyntaxTreeTypes());
        }

        private IEnumerable<INamedTypeSymbol> GetValidTypes(string name)
        {
            return compilation.SourceModule.ReferencedAssemblySymbols.Select(module =>
            {
                try
                { return module.GetTypeByMetadataName(name); }
                catch { return null; }
            })
            .OfType<INamedTypeSymbol>()
            .Concat(GetSyntaxTreeTypes().Where(t => t.GetFullName() == name)); // TODO: Correct full name
        }

        #region IReflectionProvider
        /// <inheritdoc/>
        public IEnumerable<TypeSpecifier> GetNonStaticTypes()
        {
            return GetValidTypes().Where(
                    t => t.IsPublic() && !(t.IsAbstract && t.IsSealed))
                .OrderBy(t => t.ContainingNamespace?.Name)
                .ThenBy(t => t.Name)
                .Select(t => ReflectionConverter.TypeSpecifierFromSymbol(t));
        }

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier)
        {
            ITypeSymbol? type = GetTypeFromSpecifier(typeSpecifier);

            if (type != null)
            {
                // Get all overridable methods, ignore special ones (properties / events)

                return type.GetMethods(memberCache)
                    .Where(m =>
                        (m.IsVirtual || m.IsOverride || m.IsAbstract)
                        && m.MethodKind == MethodKind.Ordinary)
                    .OrderBy(m => m.ContainingNamespace?.Name)
                    .ThenBy(m => m.ContainingType?.Name)
                    .ThenBy(m => m.Name)
                    .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));
            }
            else
            {
                return new MethodSpecifier[0];
            }
        }

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
        {
            ITypeSymbol? type = GetTypeFromSpecifier(methodSpecifier.DeclaringType);

            // TODO: Get a better way to determine is a method specifier is an operator.
            bool isOperator = methodSpecifier.Name.StartsWith("op_");

            if (type != null)
            {
                return type.GetMethods(memberCache)
                        .Where(m =>
                            m.Name == methodSpecifier.Name
                            && m.IsPublic()
                            && m.IsStatic == methodSpecifier.Modifiers.HasFlag(MethodModifiers.Static)
                            && (isOperator ?
                                (m.MethodKind == MethodKind.BuiltinOperator || m.MethodKind == MethodKind.UserDefinedOperator) :
                                m.MethodKind == MethodKind.Ordinary))
                        .OrderBy(m => m.ContainingNamespace?.Name)
                        .ThenBy(m => m.ContainingType?.Name)
                        .ThenBy(m => m.Name)
                        .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));
            }
            else
            {
                return new MethodSpecifier[0];
            }
        }

        /// <inheritdoc/>
        public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
        {
            var symbol = GetTypeFromSpecifier<INamedTypeSymbol>(typeSpecifier);

            if (symbol != null)
            {
                return symbol.Constructors.Select(c => ReflectionConverter.ConstructorSpecifierFromSymbol(c));
            }

            return new ConstructorSpecifier[0];
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier)
        {
            var symbol = GetTypeFromSpecifier(typeSpecifier);

            if (symbol != null)
            {
                return symbol.GetAllMembers(memberCache)
                    .Where(member => member.Kind == SymbolKind.Field)
                    .Select(member => member.Name);
            }

            return new string[0];
        }

        /// <inheritdoc/>
        public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b)
        {
            ITypeSymbol? typeA = GetTypeFromSpecifier(a);
            ITypeSymbol? typeB = GetTypeFromSpecifier(b);

            return typeA != null && typeB != null && typeA.IsSubclassOf(typeB);
        }

        private T? GetTypeFromSpecifier<T>(TypeSpecifier specifier)
            where T : class, ITypeSymbol
        {
            return (T?)GetTypeFromSpecifier(specifier);
        }

        private readonly Dictionary<TypeSpecifier, ITypeSymbol?> cachedTypeSpecifierSymbols = new Dictionary<TypeSpecifier, ITypeSymbol?>();

        private ITypeSymbol? GetTypeFromSpecifier(TypeSpecifier specifier)
        {
            if (cachedTypeSpecifierSymbols.TryGetValue(specifier, out var symbol))
            {
                return symbol;
            }

            string lookupName = specifier.Name;

            // Find array ranks and remove them from the lookup name.
            // Example: int[][,] -> arrayRanks: { 1, 2 }, lookupName: int
            Stack<int> arrayRanks = new Stack<int>();
            while (lookupName.EndsWith("]"))
            {
                lookupName = lookupName.Remove(lookupName.Length - 1);
                int arrayRank = 1;
                while (lookupName.EndsWith(","))
                {
                    arrayRank++;
                    lookupName = lookupName.Remove(lookupName.Length - 1);
                }
                arrayRanks.Push(arrayRank);

                if (lookupName.Last() != '[')
                {
                    throw new Exception("Expected [ in lookupName");
                }

                lookupName = lookupName.Remove(lookupName.Length - 1);
            }

            if (specifier.GenericArguments.Count > 0)
                lookupName += $"`{specifier.GenericArguments.Count}";

            IEnumerable<INamedTypeSymbol> types = GetValidTypes(lookupName);

            ITypeSymbol? foundType = null;

            foreach (INamedTypeSymbol t in types)
            {
                if (t != null)
                {
                    if (specifier.GenericArguments.Count > 0)
                    {
                        var typeArguments = specifier.GenericArguments
                            .Select(baseType => baseType is TypeSpecifier typeSpec ?
                                GetTypeFromSpecifier(typeSpec) ?? throw new InvalidOperationException($"Could not resolve generic argument type '{typeSpec}'.") :
                                t.TypeArguments[specifier.GenericArguments.IndexOf(baseType)])
                            .ToArray();
                        foundType = t.Construct(typeArguments);
                    }
                    else
                    {
                        foundType = t;
                    }

                    break;
                }
            }

            if (foundType != null)
            {
                // Make array
                //while (arrayRanks.TryPop(out int arrayRank))
                while (arrayRanks.Count > 0)
                {
                    int arrayRank = arrayRanks.Pop();
                    foundType = compilation.CreateArrayTypeSymbol(foundType, arrayRank);
                }
            }

            cachedTypeSpecifierSymbols.Add(specifier, foundType);

            return foundType;
        }

        private IMethodSymbol? GetMethodInfoFromSpecifier(MethodSpecifier specifier)
        {
            INamedTypeSymbol? declaringType = GetTypeFromSpecifier<INamedTypeSymbol>(specifier.DeclaringType);
            return declaringType?.GetMethods(memberCache).FirstOrDefault(
                    m => m.Name == specifier.Name
                    && m.Parameters.Select(p => ReflectionConverter.BaseTypeSpecifierFromSymbol(p.Type)).SequenceEqual(specifier.ArgumentTypes));
        }

        // Documentation

        /// <inheritdoc/>
        public string? GetMethodDocumentation(MethodSpecifier methodSpecifier)
        {
            IMethodSymbol? methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if (methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodSummary(methodInfo);
        }

        /// <inheritdoc/>
        public string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex)
        {
            IMethodSymbol? methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if (methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodParameterInfo(methodInfo.Parameters[parameterIndex]);
        }

        /// <inheritdoc/>
        public string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex)
        {
            IMethodSymbol? methodInfo = GetMethodInfoFromSpecifier(methodSpecifier);

            if (methodInfo == null)
            {
                return null;
            }

            return documentationUtil.GetMethodReturnInfo(methodInfo);
        }

        /// <inheritdoc/>
        public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType)
        {
            // Check if there exists a conversion that is implicit between the types.

            ITypeSymbol? fromSymbol = GetTypeFromSpecifier(fromType);
            ITypeSymbol? toSymbol = GetTypeFromSpecifier(toType);

            return fromSymbol != null && toSymbol != null
                && compilation.ClassifyConversion(fromSymbol, toSymbol).IsImplicit;
        }

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
        {
            IEnumerable<IMethodSymbol> methodSymbols;

            // Check if type is set (no type => get all methods)
            if (!(query.Type is null))
            {
                // Get all methods of the type
                ITypeSymbol? type = GetTypeFromSpecifier(query.Type);

                if (type == null)
                {
                    return new MethodSpecifier[0];
                }

                methodSymbols = type.GetMethods(memberCache);

                var extensions = extensionMethods.Where(m => type.IsSubclassOf(m.Parameters[0].Type)).ToList();

                // Add applicable extension methods
                methodSymbols = methodSymbols.Concat(extensions);
            }
            else
            {
                // Get all methods of all public types
                methodSymbols = GetValidTypes()
                                .Where(t => t.IsPublic())
                                .SelectMany(t => t.GetMethods(memberCache));
            }

            // Check static
            if (query.Static.HasValue)
            {
                methodSymbols = methodSymbols.Where(m => m.IsStatic == query.Static.Value);
            }

            // Check has generic arguments
            if (query.HasGenericArguments.HasValue)
            {
                methodSymbols = methodSymbols.Where(m => query.HasGenericArguments.Value ?
                    m.TypeParameters.Any() :
                    !m.TypeParameters.Any());
            }

            // Check visibility
            if (!(query.VisibleFrom is null))
            {
                methodSymbols = methodSymbols.Where(m => NetPrintsUtil.IsVisible(query.VisibleFrom,
                    ReflectionConverter.TypeSpecifierFromSymbol(m.ContainingType),
                    ReflectionConverter.VisibilityFromAccessibility(m.DeclaredAccessibility),
                    TypeSpecifierIsSubclassOf));
            }

            // Check argument type
            if (!(query.ArgumentType is null))
            {
                var searchType = GetTypeFromSpecifier(query.ArgumentType);

                methodSymbols = methodSymbols
                    .Where(m => m.Parameters
                        .Select(p => p.Type)
                        .Any(t => SymbolEqualityComparer.Default.Equals(t, searchType)
                                    || searchType.IsSubclassOf(t)
                                    || t.TypeKind == TypeKind.TypeParameter));
            }

            // Check return type
            if (!(query.ReturnType is null))
            {
                var searchType = GetTypeFromSpecifier(query.ReturnType);

                methodSymbols = methodSymbols
                    .Where(m => SymbolEqualityComparer.Default.Equals(m.ReturnType, searchType)
                                || m.ReturnType.IsSubclassOf(searchType)
                                || m.ReturnType.TypeKind == TypeKind.TypeParameter);
            }

            var methodSpecifiers = methodSymbols
                .OrderBy(m => m.ContainingNamespace?.Name)
                .ThenBy(m => m.ContainingType?.Name)
                .ThenBy(m => m.Name)
                .Select(m => ReflectionConverter.MethodSpecifierFromSymbol(m));

            // HACK: Add default operators which we can not find by
            //       reflection at this time.
            if (query.HasGenericArguments != true && query.Static != false)
            {
                var defaultOperatorSpecifiers = DefaultOperatorSpecifiers.All;

                if (!(query.Type is null))
                {
                    defaultOperatorSpecifiers = defaultOperatorSpecifiers.Where(t => t.DeclaringType == query.Type);
                }

                if (!(query.ReturnType is null))
                {
                    defaultOperatorSpecifiers = defaultOperatorSpecifiers.Where(t => t.ReturnTypes.Any(rt => rt == query.ReturnType));
                }

                if (!(query.ArgumentType is null))
                {
                    defaultOperatorSpecifiers = defaultOperatorSpecifiers.Where(t => t.ArgumentTypes.Any(at => at == query.ArgumentType));
                }

                methodSpecifiers = defaultOperatorSpecifiers.Concat(methodSpecifiers);
            }

            return methodSpecifiers;
        }

        /// <inheritdoc/>
        public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
        {
            // Note: Currently we handle fields and properties in this function
            //       so there is some extra logic for handling the fields.
            //       This should be unified or seperated later.

            static ITypeSymbol TypeSymbolFromFieldOrProperty(ISymbol symbol)
            {
                if (symbol is IFieldSymbol fieldSymbol)
                {
                    return fieldSymbol.Type;
                }
                else if (symbol is IPropertySymbol propertySymbol)
                {
                    return propertySymbol.Type;
                }

                throw new ArgumentException("symbol not a property nor field symbol.");
            }

            IEnumerable<ISymbol> propertySymbols;

            // Check if type is set (no type => get all methods)
            if (!(query.Type is null))
            {
                // Get all properties of the type
                ITypeSymbol? type = GetTypeFromSpecifier(query.Type);

                if (type == null)
                {
                    return new VariableSpecifier[0];
                }

                propertySymbols = type.GetAllMembers(memberCache)
                    .Where(m => m.Kind == SymbolKind.Property || m.Kind == SymbolKind.Field);
            }
            else
            {
                // Get all properties of all public types
                propertySymbols = GetValidTypes()
                    .SelectMany(t => t.GetAllMembers(memberCache)
                        .Where(m => m.Kind == SymbolKind.Property || m.Kind == SymbolKind.Field));
            }

            // Check static
            if (query.Static.HasValue)
            {
                propertySymbols = propertySymbols.Where(m => m.IsStatic == query.Static.Value);
            }

            // Check visibility
            if (!(query.VisibleFrom is null))
            {
                propertySymbols = propertySymbols.Where(p => NetPrintsUtil.IsVisible(query.VisibleFrom,
                    ReflectionConverter.TypeSpecifierFromSymbol(p.ContainingType),
                    ReflectionConverter.VisibilityFromAccessibility(p.DeclaredAccessibility),
                    TypeSpecifierIsSubclassOf));
            }

            // Check property type
            if (!(query.VariableType is null))
            {
                var searchType = GetTypeFromSpecifier(query.VariableType);

                propertySymbols = propertySymbols.Where(p => query.VariableTypeDerivesFrom ?
                    TypeSymbolFromFieldOrProperty(p).IsSubclassOf(searchType) :
                    searchType.IsSubclassOf(TypeSymbolFromFieldOrProperty(p)));
            }

            return propertySymbols
                .OrderBy(p => p.ContainingNamespace?.Name)
                .ThenBy(p => p.ContainingType?.Name)
                .ThenBy(p => p.Name)
                .Select(p => p is IPropertySymbol propertySymbol ? ReflectionConverter.VariableSpecifierFromSymbol(propertySymbol) : ReflectionConverter.VariableSpecifierFromField((IFieldSymbol)p));
        }

        #endregion
    }
}
