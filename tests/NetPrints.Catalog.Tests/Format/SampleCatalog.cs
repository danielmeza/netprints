namespace NetPrints.Catalog.Tests.Format;

/// <summary>A hand-built catalog that exercises every property and every canonical writing rule.</summary>
internal static class SampleCatalog
{
    public const string GoldenFileName = "writer.npcat.json";

    private static CatalogTypeRef Type(string name) => new() { Name = name };

    public static CatalogDocument Build() => new()
    {
        Id = "samplelib",
        Version = "1.2.3.4",
        Profile = "public-api",
        Assemblies =
        [
            new CatalogAssembly { Name = "SampleLib", Version = "1.2.3.4" },
            new CatalogAssembly { Name = "SampleLib.Extras", Version = "1.0.0.0" },
        ],
        Types =
        [
            new CatalogType
            {
                Id = "T:Sample.Color",
                Namespace = "Sample",
                Name = "Color",
                Kind = CatalogTypeKind.Enum,
                EnumMembers = ["Red", "Green", "Blue"],
                Summary = "Quote \" backslash \\ tab \t newline \n cr \r bell \u0001 unit \u001f accents éü smile \U0001F600.",
            },
            new CatalogType
            {
                Id = "T:Sample.Geometry.Vector2",
                Namespace = "Sample.Geometry",
                Name = "Vector2",
                Kind = CatalogTypeKind.Struct,
                Modifiers = ["sealed"],
                Interfaces = [Type("System.IEquatable"), new CatalogTypeRef { Name = "System.IFormattable", IsInterface = true }],
                Summary = "A 2D vector.",
                Node = new CatalogNodeHint { DisplayName = "Vector 2D", Category = "Math|Vectors", Keywords = ["point", "vec"] },
                Constructors =
                [
                    new CatalogConstructor
                    {
                        Id = "M:Sample.Geometry.Vector2.#ctor(System.Single,System.Single)",
                        Visibility = CatalogVisibility.Public,
                        Parameters = [new CatalogParameter { Name = "x", Type = Type("System.Single") }, new CatalogParameter { Name = "y", Type = Type("System.Single") }],
                        Summary = "Creates a vector.",
                    },
                ],
                Methods =
                [
                    new CatalogMethod
                    {
                        Id = "M:Sample.Geometry.Vector2.Add(Sample.Geometry.Vector2,Sample.Geometry.Vector2)",
                        Name = "Add",
                        Visibility = CatalogVisibility.Public,
                        Modifiers = ["static"],
                        Parameters =
                        [
                            new CatalogParameter { Name = "a", Type = Type("Sample.Geometry.Vector2") },
                            new CatalogParameter { Name = "b", Type = Type("Sample.Geometry.Vector2"), Summary = "The second vector." },
                        ],
                        ReturnType = Type("Sample.Geometry.Vector2"),
                        ReturnSummary = "The sum.",
                        Summary = "Adds two vectors.",
                    },
                    new CatalogMethod
                    {
                        Id = "M:Sample.Geometry.Vector2.TryParse(System.String,Sample.Geometry.Vector2@,System.Int32,System.Object[])",
                        Name = "TryParse",
                        Visibility = CatalogVisibility.Protected,
                        Modifiers = ["static", "virtual"],
                        GenericParameters = ["T"],
                        Parameters =
                        [
                            new CatalogParameter { Name = "text", Type = Type("System.String") },
                            new CatalogParameter { Name = "result", Type = Type("Sample.Geometry.Vector2"), PassType = CatalogPassType.Out },
                            new CatalogParameter { Name = "radix", Type = Type("System.Int32"), Default = new CatalogTypedValue { Type = "System.Int32", Value = "10" } },
                            new CatalogParameter { Name = "rest", Type = Type("System.Object"), Params = true },
                            new CatalogParameter { Name = "source", Type = Type("System.String"), PassType = CatalogPassType.Reference, Default = new CatalogTypedValue { Type = "System.String" } },
                        ],
                        ReturnType = new CatalogTypeRef
                        {
                            Name = "System.Collections.Generic.List",
                            Args = [new CatalogTypeRef { Name = "T", Generic = true }, new CatalogTypeRef { Name = "Sample.Color", IsEnum = true }],
                        },
                        Obsolete = new CatalogObsoleteInfo { Message = "Use \"Parse\" instead.", Error = true },
                        Node = new CatalogNodeHint { Category = "Math" },
                    },
                    new CatalogMethod
                    {
                        Id = "M:Sample.Geometry.Vector2.Reset",
                        Name = "Reset",
                        Visibility = CatalogVisibility.Public,
                        Obsolete = new CatalogObsoleteInfo(),
                    },
                ],
                Variables =
                [
                    new CatalogVariable
                    {
                        Id = "F:Sample.Geometry.Vector2.Zero",
                        Name = "Zero",
                        Kind = CatalogVariableKind.Field,
                        Type = Type("Sample.Geometry.Vector2"),
                        Modifiers = ["static", "readonly"],
                        Get = CatalogVisibility.Public,
                    },
                    new CatalogVariable
                    {
                        Id = "P:Sample.Geometry.Vector2.X",
                        Name = "X",
                        Kind = CatalogVariableKind.Property,
                        Type = Type("System.Single"),
                        Get = CatalogVisibility.Public,
                        Set = CatalogVisibility.Protected,
                        Summary = "The x component.",
                    },
                ],
            },
            new CatalogType
            {
                Id = "T:Sample.Geometry.Vector2.Comparer",
                Name = "Comparer",
                Kind = CatalogTypeKind.Class,
                Modifiers = ["abstract"],
                GenericParameters = ["TKey", "TValue"],
                DeclaringType = "T:Sample.Geometry.Vector2",
                BaseType = new CatalogTypeRef { Name = "Sample.Base", Args = [Type("System.String")] },
            },
        ],
    };
}
