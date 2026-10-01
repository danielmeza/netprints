using NetPrints.Core;
using NetPrints.Reflection;

namespace Fx.Catalog;

/// <summary>The <c>fx.catalog.profile</c> project profile: the default template, with the <c>fixture-flags</c> catalog profile as the default for <c>netprints catalog</c>.</summary>
public sealed class FxCatalogProjectProfile : IProjectProfile
{
    /// <summary>The profile id.</summary>
    public const string ProfileId = "fx.catalog.profile";

    /// <inheritdoc />
    public string Id => ProfileId;

    /// <inheritdoc />
    public string DisplayName => "NetPrints catalog fixture project";

    /// <inheritdoc />
    public string DefaultTargetFramework => "net10.0";

    /// <inheritdoc />
    public string ProjectTemplate => DefaultProjectProfile.Instance.ProjectTemplate;

    /// <inheritdoc />
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; } = [TypeSpecifier.FromType<object>()];

    /// <inheritdoc />
    public IReadOnlyList<ClassTemplate> ClassTemplates => DefaultProjectProfile.Instance.ClassTemplates;

    /// <inheritdoc />
    public string? CatalogProfileId => "fixture-flags";
}
