using System.Reactive.Concurrency;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Contributions;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Stores;
using NetPrints.Workspace;

namespace NetPrints.Editor.Hosting;

/// <summary>The registrations written by hand next to the generated <c>AddEditorServices()</c>: the instances a host provides.</summary>
internal static class EditorServiceRegistrations
{
    /// <summary>Registers the process-wide services of <paramref name="host"/> and what is built from them, then the generated registrations.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="host">Process-wide services created once by the host.</param>
    /// <returns>The collection.</returns>
    public static IServiceCollection AddEditorHost(this IServiceCollection services, EditorHostServices host)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(host);
        services.AddSingleton(host);
        services.AddSingleton(host.LoggerFactory);
        services.AddSingleton(host.Extensions);
        services.AddSingleton(host.Settings);
        services.AddSingleton(host.HostChannel);
        services.AddSingleton<IScheduler>(DefaultScheduler.Instance);
        services.AddSingleton<Func<IMessenger>>(static () => new WeakReferenceMessenger());
        services.AddSingleton<IProjectSystem>(static provider => CreateProjectSystem(provider.GetRequiredService<EditorHostServices>(), provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton(static provider => CreatePersistence(provider.GetRequiredService<IProjectSystem>(), provider.GetRequiredService<IExtensionHost>(), provider.GetRequiredService<ILoggerFactory>()));
        return services.AddEditorServices();
    }

    /// <summary>Registers what a project session needs from the host: the services of <paramref name="context"/> and the generated registrations.</summary>
    /// <param name="services">The collection to add to.</param>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="registry">The frozen registry whose project templates the session offers.</param>
    /// <returns>The collection.</returns>
    public static IServiceCollection AddSessionHost(this IServiceCollection services, EditorContext context, IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        services.AddEditorServices();
        services.AddSingleton(context);
        services.AddSingleton(registry);
        services.AddSingleton(context.LoggerFactory);
        services.AddSingleton(context.Persistence);
        services.AddSingleton(context.Extensions);
        services.AddSingleton(context.Projects);
        if (context.Backups is { } backups)
        {
            services.AddSingleton(backups);
        }

        return services;
    }

    private static IProjectSystem CreateProjectSystem(EditorHostServices host, ILoggerFactory loggerFactory) =>
        host.MsBuildAvailable
            ? new MsBuildProjectSystem(EditorServices.CreateProjectSystemOptions(host.Extensions), new ProcessRunner(), loggerFactory.CreateLogger<MsBuildProjectSystem>())
            : new NoSdkProjectSystem();

    private static ProjectPersistence CreatePersistence(IProjectSystem projects, IExtensionHost extensions, ILoggerFactory loggerFactory)
    {
        (DocumentFormatRegistry formats, IDocumentMapper mapper) = PersistenceBinding.CreateSerializers(extensions.Current, loggerFactory);
        return new ProjectPersistence(projects, formats, mapper,
            (directory, watch) => new FileSystemDocumentStore(directory, DefaultScheduler.Instance, loggerFactory.CreateLogger<FileSystemDocumentStore>(), watch),
            loggerFactory.CreateLogger<ProjectPersistence>());
    }
}
