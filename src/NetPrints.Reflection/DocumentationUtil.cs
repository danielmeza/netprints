using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace NetPrints.Reflection
{
    /// <summary>
    /// Reads XML documentation summary/param/returns text for reflected methods from each assembly's
    /// documentation file, given directly (<see cref="NetPrints.Projects.ResolvedAssembly.DocumentationPath"/>,
    /// resolved by <c>IProjectSystem.LoadAsync</c>, project-system.md §4) rather than guessed, caching
    /// lookups per assembly, method and parameter.
    /// </summary>
    public class DocumentationUtil
    {
        private readonly Dictionary<string, XmlDocument?> cachedDocuments =
            new Dictionary<string, XmlDocument?>();

        private readonly Dictionary<string, string?> cachedMethodSummaries =
            new Dictionary<string, string?>();

        private readonly Dictionary<Tuple<string, string>, string?> cachedMethodParameterInfos =
            new Dictionary<Tuple<string, string>, string?>();

        private readonly Dictionary<string, string?> cachedMethodReturnInfo =
            new Dictionary<string, string?>();

        private readonly Microsoft.CodeAnalysis.Compilation compilation;
        private readonly IReadOnlyDictionary<string, string> documentationPaths;

        /// <summary>
        /// Creates a documentation util resolving assembly documentation files through
        /// <paramref name="documentationPaths"/>.
        /// </summary>
        /// <param name="compilation">Compilation to resolve an assembly symbol's file path through.</param>
        /// <param name="documentationPaths">Each referenced assembly's file path mapped to its
        /// documentation file path; an assembly missing from this map, or whose path does not exist,
        /// has no documentation.</param>
        public DocumentationUtil(Microsoft.CodeAnalysis.Compilation compilation, IReadOnlyDictionary<string, string> documentationPaths)
        {
            this.compilation = compilation;
            this.documentationPaths = documentationPaths;
        }

        private string? GetAssemblyPath(IAssemblySymbol assembly)
        {
            MetadataReference? reference = compilation.GetMetadataReference(assembly);
            if (reference is PortableExecutableReference peReference)
            {
                return peReference.FilePath;
            }
            return null;
        }

        private string GetMethodInfoKey(IMethodSymbol methodInfo)
        {
            string key = $"M:{methodInfo.ContainingType.GetFullName()}.{methodInfo.Name}";

            if (methodInfo.Parameters.Length > 0)
            {
                key += "(";
                key += string.Join(",", methodInfo.Parameters.Select(p => p.Type.GetFullName()));
                key += ")";
            }

            return key;
        }

        private XmlDocument? GetAssemblyDocumentationDocument(IAssemblySymbol assembly)
        {
            string? assemblyPath = GetAssemblyPath(assembly);
            if (assemblyPath == null)
            {
                return null;
            }

            if (cachedDocuments.TryGetValue(assemblyPath, out XmlDocument? cached))
            {
                return cached;
            }

            XmlDocument? doc = null;
            if (documentationPaths.TryGetValue(assemblyPath, out string? docPath) && File.Exists(docPath))
            {
                try
                {
                    doc = new XmlDocument();
                    using var stream = File.OpenRead(docPath);
                    doc.Load(stream);
                }
                catch
                {
                    doc = null;
                }
            }

            // Cached whether or not documentation was found, so the lookup is not repeated.
            cachedDocuments[assemblyPath] = doc;
            return doc;
        }

        /// <summary>
        /// Gets the summary text for a method.
        /// </summary>
        /// <param name="methodInfo">Method to get summary text for.</param>
        /// <returns>Summary text for a method.</returns>
        public string? GetMethodSummary(IMethodSymbol methodInfo)
        {
            string methodKey = GetMethodInfoKey(methodInfo);

            if (cachedMethodSummaries.ContainsKey(methodKey))
            {
                return cachedMethodSummaries[methodKey];
            }

            string? documentation = null;

            XmlDocument? doc = GetAssemblyDocumentationDocument(methodInfo.ContainingAssembly);
            if (doc != null)
            {
                XmlNodeList? nodes = doc.SelectNodes($"doc/members/member[@name='{methodKey}']/summary");

                if (nodes != null && nodes.Count > 0)
                {
                    documentation = nodes.Item(0)?.InnerText;
                }

                cachedMethodSummaries.Add(methodKey, documentation);
            }

            return documentation;
        }

        /// <summary>
        /// Gets the summary text of a method's parameter.
        /// </summary>
        /// <param name="parameterSymbol">Parameter to get the summary text for.</param>
        /// <returns>Summary text of a method's parameter.</returns>
        public string? GetMethodParameterInfo(IParameterSymbol parameterSymbol)
        {
            IMethodSymbol methodSymbol = (IMethodSymbol)parameterSymbol.ContainingSymbol;
            string methodKey = GetMethodInfoKey(methodSymbol);
            Tuple<string, string> cacheKey = new Tuple<string, string>(methodKey, parameterSymbol.Name);
            if (cachedMethodParameterInfos.ContainsKey(cacheKey))
            {
                return cachedMethodParameterInfos[cacheKey];
            }

            string? documentation = null;

            XmlDocument? doc = GetAssemblyDocumentationDocument(methodSymbol.ContainingAssembly);
            if (doc != null)
            {
                string searchName = $"M:{methodSymbol.ContainingType.GetFullName()}.{methodSymbol.Name}";
                if (methodSymbol.Parameters.Length > 0)
                {
                    searchName += "(";
                    searchName += string.Join(",", methodSymbol.Parameters.Select(p => p.Type.GetFullName()));
                    searchName += ")";
                }

                XmlNodeList? nodes = doc.SelectNodes($"doc/members/member[@name='{searchName}']/param[@name='{parameterSymbol.Name}']");

                if (nodes != null && nodes.Count > 0)
                {
                    documentation = nodes.Item(0)?.InnerText;
                }

                cachedMethodParameterInfos.Add(cacheKey, documentation);
            }

            return documentation;
        }

        /// <summary>
        /// Gets a method's return information.
        /// </summary>
        /// <param name="methodSymbol">Method to get return information for.</param>
        /// <returns>Return information for the method.</returns>
        public string? GetMethodReturnInfo(IMethodSymbol methodSymbol)
        {
            string methodKey = GetMethodInfoKey(methodSymbol);

            if (cachedMethodReturnInfo.ContainsKey(methodKey))
            {
                return cachedMethodReturnInfo[methodKey];
            }

            string? documentation = null;

            XmlDocument? doc = GetAssemblyDocumentationDocument(methodSymbol.ContainingType.ContainingAssembly);

            if (doc != null)
            {
                string searchName = $"M:{methodSymbol.ContainingType.GetFullName()}.{methodSymbol.Name}";
                if (methodSymbol.Parameters.Length > 0)
                {
                    searchName += "(";
                    searchName += string.Join(",", methodSymbol.Parameters.Select(p => p.Type.GetFullName()));
                    searchName += ")";
                }

                XmlNodeList? nodes = doc.SelectNodes($"doc/members/member[@name='{searchName}']/returns");

                if (nodes != null && nodes.Count > 0)
                {
                    documentation = nodes.Item(0)?.InnerText;
                }

                cachedMethodReturnInfo.Add(methodKey, documentation);
            }

            return documentation;
        }
    }
}
