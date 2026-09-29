using NetPrints.Core;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Settings;
using NetPrints.Reflection;
using NetPrints.Translator;

namespace NetPrints.TestExtension;

/// <summary>Adds an obsolete attribute and a using to every class; makes <c>Partial*</c> classes partial.</summary>
public sealed class TestClassEmitter : IClassEmitter
{
    /// <inheritdoc />
    public string Id => "netprints.test/class";

    /// <inheritdoc />
    public void EmitClass(ClassEmitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Attributes.Add("System.Obsolete(\"test\")");
        context.Usings.Add("System.Linq");
        if (context.Declaration.Name.StartsWith("Partial", StringComparison.Ordinal))
        {
            context.ExtraModifiers.Add("partial");
        }
    }
}

/// <summary>Declares the properties of <c>Partial*</c> classes as partial.</summary>
public sealed class TestMemberEmitter : IMemberEmitter
{
    /// <inheritdoc />
    public string Id => "netprints.test/member";

    /// <inheritdoc />
    public void EmitMember(MemberEmitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Kind == EmittedMemberKind.Property && context.Declaration.Name.StartsWith("Partial", StringComparison.Ordinal))
        {
            context.DeclarePartial = true;
        }
    }
}

/// <summary>A catalog with one type, <c>NetPrints.TestLib.Widget</c>.</summary>
public static class TestCatalog
{
    /// <summary>The id of the catalog.</summary>
    public const string Id = "netprints.test/catalog";

    /// <summary>The one type the catalog offers.</summary>
    public static TypeSpecifier Widget { get; } = new("NetPrints.TestLib.Widget");

    /// <summary>Creates the catalog.</summary>
    /// <returns>The catalog.</returns>
    public static ITypeCatalog Create() => new InMemoryTypeCatalog(
        new CatalogInfo(Id, "1.0.0", ["NetPrints.TestLib"]),
        [Widget],
        [],
        [],
        [],
        new Dictionary<TypeSpecifier, IReadOnlyList<string>>(),
        new Dictionary<MethodSpecifier, string>());
}

/// <summary>The <c>netprints.test</c> project profile: the default template plus a test property.</summary>
public sealed class TestProfile : IProjectProfile
{
    /// <summary>The profile id.</summary>
    public const string ProfileId = "netprints.test";

    /// <summary>The MSBuild property the profile's template sets and the extension asks the project system to capture.</summary>
    public const string ModeProperty = "NetPrintsTestMode";

    /// <inheritdoc />
    public string Id => ProfileId;

    /// <inheritdoc />
    public string DisplayName => "NetPrints test project";

    /// <inheritdoc />
    public string DefaultTargetFramework => "net10.0";

    /// <inheritdoc />
    public string ProjectTemplate =>
        """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{TargetFramework}</TargetFramework>
            <RootNamespace>{RootNamespace}</RootNamespace>
            <NetPrintsProfile>{ProfileId}</NetPrintsProfile>
            <NetPrintsTestMode>on</NetPrintsTestMode>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="NetPrints.Sdk" Version="{NetPrintsSdkVersion}" PrivateAssets="all" />
          </ItemGroup>

        </Project>

        """;

    /// <inheritdoc />
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; } = [TypeSpecifier.FromType<object>()];

    /// <inheritdoc />
    public IReadOnlyList<ClassTemplate> ClassTemplates => DefaultProjectProfile.Instance.ClassTemplates;

    /// <inheritdoc />
    public string? CatalogProfileId => null;
}

/// <summary>The extension's settings section.</summary>
/// <param name="Greeting">A free-text value.</param>
public sealed record TestSettings(string Greeting)
{
    /// <summary>The section: id <c>netprints.test</c>, default greeting <c>hello</c>.</summary>
    public static ExtensionSettingsDescriptor<TestSettings> Descriptor { get; } =
        new(TestExtension.Id, TestJsonContext.Default.TestSettings, new TestSettings("hello"));
}

/// <summary>The factory <c>test</c>: an in-memory pair whose host end is kept in <see cref="LastHost"/>.</summary>
public sealed class TestHostChannelFactory : IHostChannelFactory
{
    /// <summary>The id <c>NETPRINTS_HOST_CHANNEL</c> names.</summary>
    public const string FactoryId = "test";

    /// <inheritdoc />
    public string Id => FactoryId;

    /// <summary>The host's end of the most recently created channel, or <see langword="null"/> before the first.</summary>
    public IHostChannel? LastHost { get; private set; }

    /// <inheritdoc />
    public IHostChannel Create(HostLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (InMemoryHostChannel editor, InMemoryHostChannel host) = InMemoryHostChannel.CreatePair(FactoryId);
        LastHost = host;
        return editor;
    }
}
