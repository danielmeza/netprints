using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>Renders types and member signatures as text that does not depend on the assembly a type is read from.</summary>
internal sealed class SignatureText : ISignatureTypeProvider<string, object?>
{
    private const char NestedSeparator = '+';

    public static SignatureText Instance { get; } = new();

    public static string TypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        TypeDefinition definition = reader.GetTypeDefinition(handle);
        string name = reader.GetString(definition.Name);
        TypeDefinitionHandle outer = definition.GetDeclaringType();
        if (!outer.IsNil)
        {
            return TypeName(reader, outer) + NestedSeparator + name;
        }

        string space = reader.GetString(definition.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }

    public static string TypeName(MetadataReader reader, TypeReferenceHandle handle)
    {
        TypeReference reference = reader.GetTypeReference(handle);
        string name = reader.GetString(reference.Name);
        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return TypeName(reader, (TypeReferenceHandle)reference.ResolutionScope) + NestedSeparator + name;
        }

        string space = reader.GetString(reference.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }

    public static string MethodText(string name, MethodSignature<string> signature) =>
        $"{name}({string.Join(',', signature.ParameterTypes)})";

    public static string MethodKey(MethodSignature<string> signature) =>
        $"{signature.GenericParameterCount}|{signature.ReturnType}|{string.Join(',', signature.ParameterTypes)}";

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeName(reader, handle);

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => TypeName(reader, handle);

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetPinnedType(string elementType) => elementType;

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "<" + string.Join(',', typeArguments) + ">";

    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
}
