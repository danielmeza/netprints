#nullable enable
using System.Collections.Generic;

namespace NetPrints.Core;

/// <summary>
/// The one built-in project profile (extension-points.md §5, data-model.md §5): a plain SDK-style
/// project with no extension-specific customization. Every new project defaults to this profile
/// (<see cref="ProfileId"/>); unlike an extension-contributed profile, it can never be replaced
/// (extension-points.md §5).
/// </summary>
public sealed class DefaultProjectProfile : IProjectProfile
{
    /// <summary>Reverse-DNS id of this profile.</summary>
    public const string ProfileId = "netprints.default";

    /// <summary>Id of the class template <see cref="ClassTemplates"/> offers for a new, empty class.</summary>
    public const string EmptyClassTemplateId = "netprints.empty-class";

    /// <summary>The shared instance; this profile has no per-instance state.</summary>
    public static DefaultProjectProfile Instance { get; } = new();

    private DefaultProjectProfile()
    {
        BaseTypes = [TypeSpecifier.FromType<object>()];
        ClassTemplates = [new ClassTemplate(EmptyClassTemplateId, "Empty class", CreateEmptyClass)];
    }

    /// <inheritdoc/>
    public string Id => ProfileId;

    /// <inheritdoc/>
    public string DisplayName => "NetPrints project";

    /// <inheritdoc/>
    public string DefaultTargetFramework => "net10.0";

    /// <inheritdoc/>
    public string ProjectTemplate =>
        """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>{TargetFramework}</TargetFramework>
            <RootNamespace>{RootNamespace}</RootNamespace>
            <NetPrintsProfile>{ProfileId}</NetPrintsProfile>
          </PropertyGroup>

          <ItemGroup>
            <PackageReference Include="NetPrints.Sdk" Version="{NetPrintsSdkVersion}" PrivateAssets="all" />
          </ItemGroup>

        </Project>

        """;

    /// <inheritdoc/>
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; }

    /// <inheritdoc/>
    public IReadOnlyList<ClassTemplate> ClassTemplates { get; }

    /// <inheritdoc/>
    public string? CatalogProfileId => null;

    private static ClassGraph CreateEmptyClass(Project project, string className) => new()
    {
        Name = className,
        Namespace = project.DefaultNamespace,
        Visibility = MemberVisibility.Public,
        Project = project,
    };
}
