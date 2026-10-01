using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NetPrints.Catalog;

/// <summary>Reads the catalogs a library embeds through <c>[assembly: NetPrintsEmbeddedCatalog(id, schemaVersion, json)]</c> (FR-028).</summary>
public static class EmbeddedCatalogReader
{
    private const string AttributeNamespace = "NetPrints.Annotations";

    private const string AttributeName = "NetPrintsEmbeddedCatalogAttribute";

    private const ushort CustomAttributeProlog = 1;

    /// <summary>Reads the embedded catalogs of an assembly file from its metadata, without loading the assembly.</summary>
    /// <param name="assemblyPath">The assembly file.</param>
    /// <returns>The embedded catalogs in attribute order; empty when the file is not a managed assembly or embeds none.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="CatalogFormatException">An embedded catalog is not a supported catalog (NPC101, NPC102).</exception>
    public static IReadOnlyList<CatalogDocument> Read(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        using FileStream stream = new(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            using PEReader peReader = new(stream);
            return peReader.HasMetadata ? ReadMetadata(peReader.GetMetadataReader()) : [];
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>Reads the embedded catalogs of a loaded assembly.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The embedded catalogs in attribute order; empty when it embeds none.</returns>
    /// <exception cref="CatalogFormatException">An embedded catalog is not a supported catalog (NPC101, NPC102).</exception>
    public static IReadOnlyList<CatalogDocument> Read(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        List<CatalogDocument> documents = [];
        foreach (CustomAttributeData data in assembly.GetCustomAttributesData())
        {
            if (data.AttributeType is { Name: AttributeName, Namespace: AttributeNamespace } && data.ConstructorArguments is [_, _, { Value: string json }])
            {
                documents.Add(CatalogReader.Read(json));
            }
        }

        return documents;
    }

    private static List<CatalogDocument> ReadMetadata(MetadataReader reader)
    {
        List<CatalogDocument> documents = [];
        foreach (CustomAttributeHandle handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            CustomAttribute attribute = reader.GetCustomAttribute(handle);
            if (IsEmbeddedCatalogAttribute(reader, attribute) && ReadJson(reader.GetBlobReader(attribute.Value)) is { } json)
            {
                documents.Add(CatalogReader.Read(json));
            }
        }

        return documents;
    }

    private static bool IsEmbeddedCatalogAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        TypeReferenceHandle typeHandle;
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                MemberReference member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                if (member.Parent.Kind != HandleKind.TypeReference)
                {
                    return false;
                }

                typeHandle = (TypeReferenceHandle)member.Parent;
                return Matches(reader, reader.GetTypeReference(typeHandle).Namespace, reader.GetTypeReference(typeHandle).Name);
            case HandleKind.MethodDefinition:
                TypeDefinition type = reader.GetTypeDefinition(reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType());
                return Matches(reader, type.Namespace, type.Name);
            default:
                return false;
        }
    }

    private static bool Matches(MetadataReader reader, StringHandle @namespace, StringHandle name) =>
        reader.StringComparer.Equals(name, AttributeName) && reader.StringComparer.Equals(@namespace, AttributeNamespace);

    /// <summary>Decodes the blob of the constructor <c>(string id, int schemaVersion, string json)</c>.</summary>
    private static string? ReadJson(BlobReader blob)
    {
        if (blob.ReadUInt16() != CustomAttributeProlog)
        {
            return null;
        }

        blob.ReadSerializedString();
        blob.ReadInt32();
        return blob.ReadSerializedString();
    }
}
