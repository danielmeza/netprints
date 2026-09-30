using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Reflection;
using NetPrints.Translator;

namespace NetPrints.Extensibility;

/// <summary>
/// Collects an extension's contributions during <see cref="INetPrintsExtension.Register"/>
/// (extension-points.md §1). Every method returns the builder for chaining, throws
/// <see cref="ArgumentNullException"/> for a <see langword="null"/> argument, and throws
/// <see cref="InvalidOperationException"/> once <see cref="INetPrintsExtension.Register"/> has returned.
/// Contributed objects are singletons owned by the registry for the process lifetime, are disposed with it when
/// they implement <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>, and must be thread-safe for reads.
/// </summary>
public interface IExtensionBuilder
{
    /// <summary>
    /// The manifest of the extension being registered.
    /// </summary>
    ExtensionManifest Manifest { get; }

    /// <summary>
    /// Logger factory of the host.
    /// </summary>
    ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Adds a library of node kinds.
    /// </summary>
    /// <param name="library">The library.</param>
    /// <returns>This builder.</returns>
    IExtensionBuilder AddNodeLibrary(INodeLibrary library);

    /// <summary>
    /// Adds an emitter that runs once per translated type declaration.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <returns>This builder.</returns>
[Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
    IExtensionBuilder AddClassEmitter(IClassEmitter emitter);

    /// <summary>
    /// Adds an emitter that runs once per translated member.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <returns>This builder.</returns>
[Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
    IExtensionBuilder AddMemberEmitter(IMemberEmitter emitter);

    /// <summary>
    /// Adds a precomputed type catalog.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <returns>This builder.</returns>
    IExtensionBuilder AddTypeCatalog(ITypeCatalog catalog);

    /// <summary>
    /// Adds a project profile.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>This builder.</returns>
    IExtensionBuilder AddProjectProfile(IProjectProfile profile);

    /// <summary>
    /// Adds a resolver for the JSON shape of the extension's node documents.
    /// </summary>
    /// <param name="resolver">The resolver, usually a source-generated <c>JsonSerializerContext</c>.</param>
    /// <returns>This builder.</returns>
    IExtensionBuilder AddJsonTypeInfoResolver(IJsonTypeInfoResolver resolver);

    /// <summary>
    /// Asks the project system to capture an MSBuild property into <c>ProjectSnapshot.Properties</c>.
    /// </summary>
    /// <param name="msbuildPropertyName">The property name.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="msbuildPropertyName"/> is empty or white space.</exception>
    IExtensionBuilder AddProjectProperty(string msbuildPropertyName);

    /// <summary>
    /// Adds a factory for the host channel that <c>NETPRINTS_HOST_CHANNEL=&lt;factory id&gt;</c> selects.
    /// </summary>
    /// <param name="factory">The factory.</param>
    /// <returns>This builder.</returns>
[Experimental(ExperimentalApiIds.HostChannel, UrlFormat = ExperimentalApiIds.UrlFormat)]
    IExtensionBuilder AddHostChannel(IHostChannelFactory factory);

    /// <summary>
    /// Declares the extension's settings section. An extension declares at most one, and its
    /// <see cref="ExtensionSettingsDescriptor.ExtensionId"/> must be the extension's own id.
    /// </summary>
    /// <param name="descriptor">The section.</param>
    /// <returns>This builder.</returns>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
    IExtensionBuilder AddSettings(ExtensionSettingsDescriptor descriptor);
}
