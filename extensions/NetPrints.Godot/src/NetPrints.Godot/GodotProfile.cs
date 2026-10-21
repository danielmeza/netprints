using NetPrints.Core;

namespace NetPrints.Godot;

/// <summary>The <c>godot</c> project profile: a Godot.NET.Sdk project whose graph classes are partial, so Godot can attach them as scripts.</summary>
public sealed class GodotProfile : IProjectProfile
{
    /// <summary>The profile id.</summary>
    public const string ProfileId = "godot";

    /// <summary>The id of the class template for a new Godot node script.</summary>
    public const string NodeScriptTemplateId = "godot.node-script";

    private const string GodotSdk = "Godot.NET.Sdk/4.7.2";

    /// <inheritdoc />
    public string Id => ProfileId;

    /// <inheritdoc />
    public string DisplayName => "Godot project";

    /// <inheritdoc />
    public string DefaultTargetFramework => "net10.0";

    /// <inheritdoc />
    public string ProjectTemplate =>
        $$"""
        <Project Sdk="{{GodotSdk}}">

          <PropertyGroup>
            <TargetFramework>{TargetFramework}</TargetFramework>
            <RootNamespace>{RootNamespace}</RootNamespace>
            <NetPrintsProfile>{ProfileId}</NetPrintsProfile>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="NetPrints.Sdk" Version="{NetPrintsSdkVersion}" PrivateAssets="all" />
          </ItemGroup>

        </Project>

        """;

    /// <inheritdoc />
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; } =
    [
        new TypeSpecifier("Godot.Node"),
        new TypeSpecifier("Godot.Node2D"),
        new TypeSpecifier("Godot.Node3D"),
        new TypeSpecifier("Godot.Control"),
        new TypeSpecifier("Godot.Resource"),
        TypeSpecifier.FromType<object>(),
    ];

    /// <inheritdoc />
    public IReadOnlyList<ClassTemplate> ClassTemplates { get; } = [new ClassTemplate(NodeScriptTemplateId, "Node script", CreateNodeScript)];

    /// <inheritdoc />
    public string? CatalogProfileId => null;

    private static ClassGraph CreateNodeScript(Project project, string className) => new()
    {
        Name = className,
        Namespace = project.DefaultNamespace,
        Visibility = MemberVisibility.Public,
        Modifiers = ClassModifiers.Partial,
        Project = project,
    };
}
