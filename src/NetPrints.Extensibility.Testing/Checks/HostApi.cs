using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>The types and members of the NetPrints assemblies in a host folder, read through metadata only; nothing is loaded.</summary>
internal sealed class HostApi : IDisposable
{
    private const string AssemblyPattern = "NetPrints.*.dll";
    private const char MethodPrefix = 'M';
    private const char FieldPrefix = 'F';

    private readonly Dictionary<string, HostAssembly> _assemblies = new(StringComparer.Ordinal);

    /// <summary>Gets the simple names of the host assemblies, ordered.</summary>
    public IReadOnlyList<string> AssemblyNames => [.. _assemblies.Keys.Order(StringComparer.Ordinal)];

    /// <summary>Opens every NetPrints assembly in <paramref name="folder"/>.</summary>
    /// <param name="folder">The host folder.</param>
    /// <returns>The index; dispose it to release the files.</returns>
    public static HostApi Open(string folder)
    {
        var api = new HostApi();
        foreach (string file in Directory.EnumerateFiles(folder, AssemblyPattern, SearchOption.TopDirectoryOnly))
        {
            HostAssembly.TryOpen(file, api._assemblies);
        }

        return api;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (HostAssembly assembly in _assemblies.Values)
        {
            assembly.Dispose();
        }
    }

    /// <summary>Finds a type of a host assembly.</summary>
    /// <param name="assembly">The assembly's simple name.</param>
    /// <param name="typeName">The type's full name, nested types joined with <c>+</c>.</param>
    /// <returns>The type, or <see langword="null"/> when the host does not have it.</returns>
    public HostType? FindType(string assembly, string typeName) =>
        _assemblies.TryGetValue(assembly, out HostAssembly? found) && found.Types.TryGetValue(typeName, out TypeDefinitionHandle handle)
            ? new HostType(found, handle)
            : null;

    /// <summary>Whether a type, a base type or an implemented interface declares a member.</summary>
    /// <param name="type">The type the extension names.</param>
    /// <param name="name">The member name.</param>
    /// <param name="isMethod">Whether the member is a method; otherwise a field.</param>
    /// <param name="key">The signature key of the member.</param>
    /// <returns><see langword="true"/> when the host has the member.</returns>
    public bool HasMember(HostType type, string name, bool isMethod, string key) =>
        HasMember(type, (isMethod ? MethodPrefix : FieldPrefix) + ":" + name + "|" + key, []);

    private bool HasMember(HostType type, string entry, HashSet<(string, TypeDefinitionHandle)> visited)
    {
        if (!visited.Add((type.Assembly.Name, type.Handle)))
        {
            return false;
        }

        if (type.Assembly.MembersOf(type.Handle).Contains(entry))
        {
            return true;
        }

        MetadataReader reader = type.Assembly.Reader;
        TypeDefinition definition = reader.GetTypeDefinition(type.Handle);
        if (Resolve(type.Assembly, definition.BaseType) is { } parent && HasMember(parent, entry, visited))
        {
            return true;
        }

        foreach (InterfaceImplementationHandle implementation in definition.GetInterfaceImplementations())
        {
            if (Resolve(type.Assembly, reader.GetInterfaceImplementation(implementation).Interface) is { } contract && HasMember(contract, entry, visited))
            {
                return true;
            }
        }

        return false;
    }

    private HostType? Resolve(HostAssembly from, EntityHandle handle)
    {
        MetadataReader reader = from.Reader;
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return new HostType(from, (TypeDefinitionHandle)handle);
            case HandleKind.TypeReference:
                var reference = (TypeReferenceHandle)handle;
                string? scope = MetadataNames.AssemblyOf(reader, reference);
                return scope is null ? null : FindType(scope, SignatureText.TypeName(reader, reference));
            case HandleKind.TypeSpecification:
                return MetadataNames.GenericDefinitionOf(reader, (TypeSpecificationHandle)handle) is { } definition ? Resolve(from, definition) : null;
            default:
                return null;
        }
    }

    /// <summary>One host assembly with the index the lookups use.</summary>
    internal sealed class HostAssembly : IDisposable
    {
        private readonly PEReader _pe;
        private readonly Dictionary<TypeDefinitionHandle, HashSet<string>> _members = [];

        private HostAssembly(string path)
        {
            _pe = new PEReader([.. File.ReadAllBytes(path)]);
            Reader = _pe.GetMetadataReader();
            Name = Reader.GetString(Reader.GetAssemblyDefinition().Name);
            foreach (TypeDefinitionHandle handle in Reader.TypeDefinitions)
            {
                Types[SignatureText.TypeName(Reader, handle)] = handle;
            }
        }

        public string Name { get; }

        public MetadataReader Reader { get; }

        public Dictionary<string, TypeDefinitionHandle> Types { get; } = new(StringComparer.Ordinal);

        public static void TryOpen(string path, Dictionary<string, HostAssembly> into)
        {
            try
            {
                var assembly = new HostAssembly(path);
                into[assembly.Name] = assembly;
            }
            catch (Exception ex) when (ex is InvalidOperationException or BadImageFormatException or IOException)
            {
                // A file that is not a managed assembly is not part of the host's API.
            }
        }

        public HashSet<string> MembersOf(TypeDefinitionHandle handle)
        {
            if (_members.TryGetValue(handle, out HashSet<string>? known))
            {
                return known;
            }

            var entries = new HashSet<string>(StringComparer.Ordinal);
            TypeDefinition definition = Reader.GetTypeDefinition(handle);
            foreach (MethodDefinitionHandle method in definition.GetMethods())
            {
                MethodDefinition item = Reader.GetMethodDefinition(method);
                entries.Add(MethodPrefix + ":" + Reader.GetString(item.Name) + "|" + SignatureText.MethodKey(item.DecodeSignature(SignatureText.Instance, null)));
            }

            foreach (FieldDefinitionHandle field in definition.GetFields())
            {
                FieldDefinition item = Reader.GetFieldDefinition(field);
                entries.Add(FieldPrefix + ":" + Reader.GetString(item.Name) + "|" + item.DecodeSignature(SignatureText.Instance, null));
            }

            _members[handle] = entries;
            return entries;
        }

        public void Dispose() => _pe.Dispose();
    }

    /// <summary>A type of a host assembly.</summary>
    /// <param name="Assembly">The assembly that defines it.</param>
    /// <param name="Handle">Its definition.</param>
    internal sealed record HostType(HostAssembly Assembly, TypeDefinitionHandle Handle);
}
