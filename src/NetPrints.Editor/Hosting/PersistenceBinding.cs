using Microsoft.Extensions.Logging;
using NetPrints.Extensibility.Loading;
using NetPrints.Serialization;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Keeps a <see cref="ProjectPersistence"/> on the node converters of the current extension registry: the mapper and the
/// JSON options are rebuilt whenever the registry changes (editor-services.md §4).
/// </summary>
public sealed class PersistenceBinding : IDisposable
{
    private readonly ProjectPersistence persistence;
    private readonly IExtensionHost extensions;
    private readonly ILoggerFactory loggerFactory;

    private PersistenceBinding(ProjectPersistence persistence, IExtensionHost extensions, ILoggerFactory loggerFactory)
    {
        this.persistence = persistence;
        this.extensions = extensions;
        this.loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Builds the document formats and the mapper for <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">The loaded extensions.</param>
    /// <param name="loggerFactory">Creates the migration (3001) and mapping (3002, 3003) loggers.</param>
    /// <returns>The formats and the mapper over the registry's node converters.</returns>
    public static (DocumentFormatRegistry Formats, IDocumentMapper Mapper) CreateSerializers(ExtensionRegistry registry, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        var jsonFormat = new JsonDocumentFormat(new NetPrintsJsonOptions(registry.NodeConverters),
            new DocumentMigrator([], loggerFactory.CreateLogger<DocumentMigrator>()));
        return (new DocumentFormatRegistry([jsonFormat]), new DocumentMapper(registry.NodeConverters, loggerFactory.CreateLogger<DocumentMapper>()));
    }

    /// <summary>
    /// Rebinds <paramref name="persistence"/> to the host's current registry now and on every
    /// <see cref="IExtensionHost.RegistryChanged"/>; dispose to stop.
    /// </summary>
    /// <param name="persistence">The persistence to keep current.</param>
    /// <param name="extensions">The extension host.</param>
    /// <param name="loggerFactory">Creates the migration (3001) and mapping (3002, 3003) loggers.</param>
    /// <returns>The binding.</returns>
    public static PersistenceBinding Bind(ProjectPersistence persistence, IExtensionHost extensions, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        var binding = new PersistenceBinding(persistence, extensions, loggerFactory);
        binding.Rebind(extensions.Current);
        extensions.RegistryChanged += binding.OnRegistryChanged;
        return binding;
    }

    private void OnRegistryChanged(object? sender, ExtensionRegistry registry) => Rebind(registry);

    private void Rebind(ExtensionRegistry registry)
    {
        (DocumentFormatRegistry formats, IDocumentMapper mapper) = CreateSerializers(registry, loggerFactory);
        persistence.Rebind(formats, mapper);
    }

    /// <summary>Stops following the registry.</summary>
    public void Dispose() => extensions.RegistryChanged -= OnRegistryChanged;
}
