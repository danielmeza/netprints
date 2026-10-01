using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetPrints.Catalog;

/// <summary>The temporary SDK project that resolves assembly and package sources (research R9).</summary>
internal static class TemporaryCatalogProject
{
    public const string FileName = "catalog.csproj";

    private const string SdkName = "Microsoft.NET.Sdk";

    private const string ProjectAttribute = "Project";

    private const string SdkAttribute = "Sdk";

    private const string IncludeAttribute = "Include";

    private const string False = "false";

    private const int HashLength = 16;

    private static readonly string[] IsolationProperties =
    [
        "ImportDirectoryBuildProps",
        "ImportDirectoryBuildTargets",
        "ImportDirectoryPackagesProps",
        "ManagePackageVersionsCentrally",
    ];

    /// <summary>The directory <c>obj/netprints-catalog/&lt;hash&gt;</c> for these inputs, next to <paramref name="baseDirectory"/>.</summary>
    public static string DirectoryFor(string baseDirectory, string targetFramework, IReadOnlyList<string> assemblies, IReadOnlyList<(string Id, string Version)> packages, IReadOnlyList<string> referencePaths)
    {
        StringBuilder text = new();
        text.Append("tfm=").Append(targetFramework).Append('\n');
        foreach (string assembly in assemblies.Order(StringComparer.Ordinal))
        {
            text.Append("asm=").Append(assembly).Append('\n');
        }

        foreach ((string id, string version) in packages.OrderBy(p => p.Id, StringComparer.Ordinal).ThenBy(p => p.Version, StringComparer.Ordinal))
        {
            text.Append("pkg=").Append(id).Append('@').Append(version).Append('\n');
        }

        foreach (string path in referencePaths.Order(StringComparer.Ordinal))
        {
            text.Append("ref=").Append(path).Append('\n');
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..HashLength];
        return Path.Combine(baseDirectory, "obj", "netprints-catalog", hash);
    }

    /// <summary>Renders the project text: the isolation properties, explicit SDK imports, the references and the extra search paths.</summary>
    public static string Render(string targetFramework, IReadOnlyList<string> assemblies, IReadOnlyList<(string Id, string Version)> packages, IReadOnlyList<string> referencePaths)
    {
        XElement project = new(
            "Project",
            new XElement("PropertyGroup", IsolationProperties.Select(name => new XElement(name, False))),
            Import("Sdk.props"),
            new XElement(
                "PropertyGroup",
                new XElement("TargetFramework", targetFramework),
                new XElement("OutputType", "Library"),
                new XElement("EnableDefaultItems", False),
                new XElement("GenerateAssemblyInfo", False),
                new XElement("NuGetAudit", False),
                new XElement("CopyLocalLockFileAssemblies", False),
                new XElement("_ResolveReferenceDependencies", "true")),
            new XElement(
                "ItemGroup",
                assemblies.Select(path => new XElement("Reference", new XAttribute(IncludeAttribute, Path.GetFileNameWithoutExtension(path)), new XElement("HintPath", path))),
                packages.Select(package => new XElement("PackageReference", new XAttribute(IncludeAttribute, package.Id), new XAttribute("Version", package.Version)))),
            Import("Sdk.targets"),
            DependenciesTarget());

        if (referencePaths.Count > 0)
        {
            project.Add(new XElement("PropertyGroup", new XElement("AssemblySearchPaths", "$(AssemblySearchPaths);" + string.Join(';', referencePaths))));
        }

        StringBuilder builder = new();
        using (XmlWriter writer = XmlWriter.Create(builder, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, NewLineChars = "\n" }))
        {
            project.WriteTo(writer);
        }

        return builder.Append('\n').ToString();
    }

    // A design-time build skips the dependencies of a Reference unless _ResolveReferenceDependencies is set; with it, ResolveAssemblyReferences finds them
    // in the reference's own directory and on AssemblySearchPaths, but the compiler is only handed the references, so they join ReferencePath here.
    // Without this the dependency is missing from the compilation and members using it are dropped (NPC005).
    private static XElement DependenciesTarget() =>
        new(
            "Target",
            new XAttribute("Name", "NetPrintsCatalogDependencies"),
            new XAttribute("AfterTargets", "ResolveAssemblyReferences"),
            new XElement("ItemGroup", new XElement("ReferencePath", new XAttribute(IncludeAttribute, "@(ReferenceDependencyPaths)"), new XAttribute("Exclude", "@(ReferencePath)"))));

    private static XElement Import(string project) =>
        new("Import", new XAttribute(ProjectAttribute, project), new XAttribute(SdkAttribute, SdkName));
}
