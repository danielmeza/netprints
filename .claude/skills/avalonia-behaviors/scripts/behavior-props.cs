// Lists a Xaml.Behaviors type's public properties, including inherited ones, with their XML doc summaries.
// Usage: dotnet run behavior-props.cs -- <TypeName>      e.g. ExecuteCommandOnKeyDownBehavior
// The package version comes from the nearest Directory.Packages.props; packages come from the NuGet cache.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length != 1)
{
    Console.Error.WriteLine("usage: dotnet run behavior-props.cs -- <TypeName>");
    return 2;
}

var cache = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
var version = FindPinnedVersion() ?? LatestCachedVersion(cache);
if (version is null)
{
    Console.Error.WriteLine($"No Xaml.Behaviors packages found under {cache}; run a restore first.");
    return 1;
}

var types = new Dictionary<string, TypeEntry>(StringComparer.Ordinal);
foreach (var dll in Directory.EnumerateDirectories(cache, "xaml.behaviors*")
             .Select(p => Path.Combine(p, version, "lib"))
             .Where(Directory.Exists)
             .Select(lib => Directory.EnumerateDirectories(lib).OrderByDescending(d => d, StringComparer.Ordinal).First())
             .SelectMany(d => Directory.EnumerateFiles(d, "*.dll")))
{
    Index(dll, types);
}

var match = types.Values.Where(t => t.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase)).ToList();
if (match.Count == 0)
{
    var similar = types.Values.Where(t => t.Name.Contains(args[0], StringComparison.OrdinalIgnoreCase)).Select(t => t.Name).Distinct().Order().Take(20);
    Console.WriteLine($"No type named {args[0]} in Xaml.Behaviors {version}. Similar: {string.Join(", ", similar)}");
    return 1;
}

var type = match[0];
Console.WriteLine($"{type.Display} ({type.Assembly} {version})");
if (type.Summary(type.DocId) is { Length: > 0 } s) Console.WriteLine($"  {s}");
var chain = new List<string>();
for (TypeEntry? t = type; t is not null; t = t.BaseKey is { } k && types.TryGetValue(k, out var b) ? b : null)
{
    if (t != type) chain.Add(t.Display);
    foreach (var (name, propType, docId) in t.Properties)
    {
        var from = t == type ? "" : $"  [from {t.Display}]";
        Console.WriteLine($"  {name} : {propType}{from}");
        if (t.Summary(docId) is { Length: > 0 } ps) Console.WriteLine($"      {ps}");
    }
    if (t.BaseKey is { } key && !types.ContainsKey(key)) chain.Add(Display(key));
}
Console.WriteLine($"Base chain: {string.Join(" -> ", chain)}");
return 0;

string? FindPinnedVersion()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
    {
        var props = Path.Combine(dir.FullName, "Directory.Packages.props");
        if (!File.Exists(props)) continue;
        var m = Regex.Match(File.ReadAllText(props), "Include=\"Xaml\\.Behaviors\\.Interactions\"\\s+Version=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : null;
    }
    return null;
}

static string? LatestCachedVersion(string cache)
{
    var dir = Path.Combine(cache, "xaml.behaviors.interactions");
    return Directory.Exists(dir)
        ? Directory.EnumerateDirectories(dir).Select(Path.GetFileName).OfType<string>().OrderByDescending(v => Version.TryParse(v, out var x) ? x : new Version(0, 0)).FirstOrDefault()
        : null;
}

static string Display(string metadataName) => Regex.Replace(metadataName, "`(\\d+)", m => m.Groups[1].Value == "1" ? "<T>" : "<...>");

static void Index(string dll, Dictionary<string, TypeEntry> types)
{
    var xmlPath = Path.ChangeExtension(dll, ".xml");
    var docs = File.Exists(xmlPath)
        ? XDocument.Load(xmlPath).Descendants("member")
            .GroupBy(m => m.Attribute("name")?.Value ?? "").ToDictionary(g => g.Key, g => g.First().Element("summary"))
        : new Dictionary<string, XElement?>();
    using var pe = new PEReader(File.OpenRead(dll));
    var md = pe.GetMetadataReader();
    var names = new NameProvider();
    var assembly = md.GetString(md.GetAssemblyDefinition().Name);
    foreach (var h in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(h);
        if (!td.GetDeclaringType().IsNil || (td.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) continue;
        var ns = md.GetString(td.Namespace);
        var name = md.GetString(td.Name);
        string? baseKey = td.BaseType.IsNil ? null : td.BaseType.Kind switch
        {
            HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)td.BaseType).Name),
            HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)td.BaseType).Name),
            _ => md.GetTypeSpecification((TypeSpecificationHandle)td.BaseType).DecodeSignature(names, null).Split('<')[0],
        };
        var props = new List<(string, string, string)>();
        foreach (var ph in td.GetProperties())
        {
            var p = md.GetPropertyDefinition(ph);
            var getter = p.GetAccessors().Getter;
            if (getter.IsNil || (md.GetMethodDefinition(getter).Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
            if ((md.GetMethodDefinition(getter).Attributes & MethodAttributes.Static) != 0) continue;
            var pname = md.GetString(p.Name);
            props.Add((pname, Display(p.DecodeSignature(names, null).ReturnType), $"P:{ns}.{name}.{pname}"));
        }
        types.TryAdd(name, new TypeEntry(Display(name), name, assembly, baseKey, $"T:{ns}.{name}", props, docs));
    }
}

sealed record TypeEntry(string Display, string Name, string Assembly, string? BaseKey, string DocId,
    List<(string Name, string Type, string DocId)> Properties, Dictionary<string, XElement?> Docs)
{
    public string? Summary(string docId) => Docs.TryGetValue(docId, out var e) && e is not null
        ? Regex.Replace(string.Concat(e.Nodes().Select(n => n switch
        {
            XElement x when x.Attribute("cref") is { } c => c.Value.Split('.', ':').Last(),
            XElement x when x.Attribute("langword") is { } l => l.Value,
            XElement x => x.Value,
            _ => n.ToString(),
        })), "\\s+", " ").Trim()
        : null;
}

sealed class NameProvider : ISignatureTypeProvider<string, object?>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
    public string GetByReferenceType(string elementType) => elementType;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => $"{genericType}<{string.Join(", ", typeArguments)}>";
    public string GetGenericMethodParameter(object? genericContext, int index) => $"M{index}";
    public string GetGenericTypeParameter(object? genericContext, int index) => index == 0 ? "T" : $"T{index}";
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString().ToLowerInvariant();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
}
