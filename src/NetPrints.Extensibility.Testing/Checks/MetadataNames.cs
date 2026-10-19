using System.Reflection.Metadata;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>Reads the assembly a type reference points to and the open type behind a constructed one.</summary>
internal static class MetadataNames
{
    private const SignatureTypeCode GenericInstance = SignatureTypeCode.GenericTypeInstance;

    /// <summary>Finds the simple name of the assembly a type reference resolves to, through its declaring types.</summary>
    /// <param name="reader">The reader of the assembly that holds the reference.</param>
    /// <param name="handle">The type reference.</param>
    /// <returns>The assembly's simple name, or <see langword="null"/> for a reference to a module or the current assembly.</returns>
    public static string? AssemblyOf(MetadataReader reader, TypeReferenceHandle handle)
    {
        EntityHandle scope = reader.GetTypeReference(handle).ResolutionScope;
        while (scope.Kind == HandleKind.TypeReference)
        {
            scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
        }

        return scope.Kind == HandleKind.AssemblyReference ? reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name) : null;
    }

    /// <summary>Finds the generic type a constructed type is made from.</summary>
    /// <param name="reader">The reader of the assembly that holds the type specification.</param>
    /// <param name="handle">The type specification.</param>
    /// <returns>The open type, or <see langword="null"/> when the specification is not a generic instantiation of a class or struct.</returns>
    public static EntityHandle? GenericDefinitionOf(MetadataReader reader, TypeSpecificationHandle handle)
    {
        BlobReader blob = reader.GetBlobReader(reader.GetTypeSpecification(handle).Signature);
        if (blob.ReadSignatureTypeCode() != GenericInstance)
        {
            return null;
        }

        blob.ReadSignatureTypeCode();
        return blob.ReadTypeHandle();
    }

    /// <summary>Finds the type reference a member's parent is, or the one a constructed parent is made from.</summary>
    /// <param name="reader">The reader of the assembly that holds the member reference.</param>
    /// <param name="parent">The member's parent.</param>
    /// <returns>The type reference, or <see langword="null"/> when the parent is a type of the assembly itself or something else.</returns>
    public static TypeReferenceHandle? TypeReferenceOf(MetadataReader reader, EntityHandle parent)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            return (TypeReferenceHandle)parent;
        }

        if (parent.Kind == HandleKind.TypeSpecification && GenericDefinitionOf(reader, (TypeSpecificationHandle)parent) is { Kind: HandleKind.TypeReference } open)
        {
            return (TypeReferenceHandle)open;
        }

        return null;
    }
}
