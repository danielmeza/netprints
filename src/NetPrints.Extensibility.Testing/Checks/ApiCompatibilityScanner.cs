using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK017: finds the NetPrints types and members an extension assembly references that the host does not have.</summary>
internal static class ApiCompatibilityScanner
{
    private const byte TypeReferenceTable = 0x01;
    private const byte TypeSpecificationTable = 0x1B;
    private const byte MemberReferenceTable = 0x0A;
    private const byte MethodSpecificationTable = 0x2B;
    private const byte TwoByteOpcodePrefix = 0xFE;
    private const int TokenSize = 4;
    private const int TokenTableOffset = 3;

    // call, callvirt, newobj, ldfld, ldflda, stfld, ldsfld, ldsflda, stsfld, castclass, isinst, box, unbox, unbox.any, newarr, ldtoken, ldobj, stobj, cpobj, ldelem, stelem, ldelema, mkrefany, refanyval
    private static readonly HashSet<byte> TokenOpcodes =
    [
        0x28, 0x6F, 0x73, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x80, 0x74, 0x75, 0x8C, 0x79, 0xA5, 0x8D, 0xD0, 0x71, 0x81, 0x70, 0xA3, 0xA4, 0x8F, 0xC6, 0xC2,
    ];

    // ldftn, ldvirtftn, initobj, constrained, sizeof (each follows 0xFE)
    private static readonly HashSet<byte> TokenOpcodesAfterPrefix = [0x06, 0x07, 0x15, 0x16, 0x1C];

    /// <summary>Scans an assembly.</summary>
    /// <param name="assemblyPath">The extension assembly.</param>
    /// <param name="host">The host's NetPrints assemblies.</param>
    /// <param name="cancellationToken">Token to cancel the scan.</param>
    /// <returns>The missing types and members, ordered.</returns>
    public static IReadOnlyList<MissingMember> Scan(string assemblyPath, HostApi host, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        MetadataReader reader = pe.GetMetadataReader();
        string own = reader.GetString(reader.GetAssemblyDefinition().Name);
        Dictionary<int, string> users = FindUsers(pe, reader);
        var missing = new SortedDictionary<(string Assembly, string Type, string Member), string>(Comparer<(string, string, string)>.Create(Compare));
        var missingTypes = new HashSet<TypeReferenceHandle>();

        foreach (TypeReferenceHandle handle in reader.TypeReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? assembly = MetadataNames.AssemblyOf(reader, handle);
            string typeName = SignatureText.TypeName(reader, handle);
            if (assembly is not null && HostAssemblyNames.Contract.Contains(assembly) && host.FindType(assembly, typeName) is null)
            {
                missingTypes.Add(handle);
                missing.TryAdd((assembly, typeName, string.Empty), UserOf(users, MetadataTokens.GetToken(handle), own));
            }
        }

        foreach (MemberReferenceHandle handle in reader.MemberReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MemberReference member = reader.GetMemberReference(handle);
            if (MetadataNames.TypeReferenceOf(reader, member.Parent) is not { } typeHandle || missingTypes.Contains(typeHandle))
            {
                continue;
            }

            string? assembly = MetadataNames.AssemblyOf(reader, typeHandle);
            string typeName = SignatureText.TypeName(reader, typeHandle);
            if (assembly is null || !HostAssemblyNames.Contract.Contains(assembly) || host.FindType(assembly, typeName) is not { } type)
            {
                continue;
            }

            string name = reader.GetString(member.Name);
            bool isMethod = member.GetKind() == MemberReferenceKind.Method;
            string text;
            string key;
            if (isMethod)
            {
                MethodSignature<string> signature = member.DecodeMethodSignature(SignatureText.Instance, null);
                text = SignatureText.MethodText(name, signature);
                key = SignatureText.MethodKey(signature);
            }
            else
            {
                text = name;
                key = member.DecodeFieldSignature(SignatureText.Instance, null);
            }

            if (!host.HasMember(type, name, isMethod, key))
            {
                missing.TryAdd((assembly, typeName, text), UserOf(users, MetadataTokens.GetToken(handle), own));
            }
        }

        return [.. missing.Select(entry => new MissingMember(entry.Key.Assembly, entry.Key.Type, entry.Key.Member.Length == 0 ? null : entry.Key.Member, entry.Value))];
    }

    private static int Compare((string Assembly, string Type, string Member) left, (string Assembly, string Type, string Member) right)
    {
        int result = string.CompareOrdinal(left.Assembly, right.Assembly);
        if (result == 0)
        {
            result = string.CompareOrdinal(left.Type, right.Type);
        }

        return result == 0 ? string.CompareOrdinal(left.Member, right.Member) : result;
    }

    private static string UserOf(Dictionary<int, string> users, int token, string fallback) =>
        users.TryGetValue(token, out string? user) ? user : fallback;

    private static Dictionary<int, string> FindUsers(PEReader pe, MetadataReader reader)
    {
        var users = new Dictionary<int, string>();
        foreach (TypeDefinitionHandle typeHandle in reader.TypeDefinitions)
        {
            TypeDefinition type = reader.GetTypeDefinition(typeHandle);
            string name = SignatureText.TypeName(reader, typeHandle);
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = reader.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                byte[] code = [.. pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes() ?? []];
                for (int index = 1; index + TokenSize < code.Length; index++)
                {
                    bool opcode = TokenOpcodes.Contains(code[index - 1])
                        || (index >= 2 && TokenOpcodesAfterPrefix.Contains(code[index - 1]) && code[index - 2] == TwoByteOpcodePrefix);
                    if (!opcode)
                    {
                        continue;
                    }

                    byte table = code[index + TokenTableOffset];
                    if (table is TypeReferenceTable or MemberReferenceTable or MethodSpecificationTable or TypeSpecificationTable)
                    {
                        int token = BitConverter.ToInt32(code, index);
                        Attribute(users, reader, token, name);
                    }
                }
            }
        }

        return users;
    }

    private static void Attribute(Dictionary<int, string> users, MetadataReader reader, int token, string user)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        if (!RowExists(reader, handle))
        {
            return;
        }

        if (handle.Kind == HandleKind.MethodSpecification)
        {
            handle = reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method;
            token = MetadataTokens.GetToken(handle);
        }

        users.TryAdd(token, user);
        if (handle.Kind == HandleKind.MemberReference && MetadataNames.TypeReferenceOf(reader, reader.GetMemberReference((MemberReferenceHandle)handle).Parent) is { } parent)
        {
            users.TryAdd(MetadataTokens.GetToken(parent), user);
        }
    }

    private static bool RowExists(MetadataReader reader, EntityHandle handle)
    {
        if (!MetadataTokens.TryGetTableIndex(handle.Kind, out TableIndex table))
        {
            return false;
        }

        return MetadataTokens.GetRowNumber(reader, handle) <= reader.GetTableRowCount(table);
    }
}
