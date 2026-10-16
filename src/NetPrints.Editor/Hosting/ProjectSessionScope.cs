using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The container of one project session: the session-scoped services (backups, recovery, project templates) live in it and
/// go with the session. It is built from the host services of an <see cref="EditorContext"/>.
/// </summary>
internal sealed class ProjectSessionScope : IDisposable
{
    private readonly ServiceProvider container;
    private readonly IServiceScope scope;
    private SessionBackups? backups;
    private bool disposed;

    private ProjectSessionScope(ServiceProvider container, IServiceScope scope)
    {
        this.container = container;
        this.scope = scope;
    }

    /// <summary>Gets the provider of the session's services.</summary>
    public IServiceProvider Services => scope.ServiceProvider;

    /// <summary>Gets the backups of the session, or <see langword="null"/> before <see cref="CreateBackups"/> or when the host configured none.</summary>
    public SessionBackups? Backups => backups;

    /// <summary>Gets the project template service of the session.</summary>
    public ProjectTemplateService Templates => Services.GetRequiredService<ProjectTemplateService>();

    /// <summary>Builds the container of a session.</summary>
    /// <param name="context">Host services shared across the editor; the backup options come from it.</param>
    /// <param name="registry">The frozen registry whose project templates the session offers.</param>
    /// <param name="data">What identifies the session.</param>
    /// <returns>The session container; dispose it with the session.</returns>
    public static ProjectSessionScope Create(EditorContext context, IContributionRegistry registry, SessionData data)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(data);
        ServiceProvider container = new ServiceCollection().AddSessionHost(context, registry).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IServiceScope scope = container.CreateScope();
        scope.ServiceProvider.GetRequiredService<SessionIdentity>().Initialize(data);
        return new ProjectSessionScope(container, scope);
    }

    /// <summary>Offers the backups of the project that is about to open.</summary>
    /// <param name="project">The project whose classes the backups may replace.</param>
    /// <param name="dialogs">Asks which backups to restore.</param>
    /// <param name="cancellationToken">Cancels the recovery.</param>
    /// <returns>What was restored and the issues met; nothing when the host configured no backups.</returns>
    public Task<RecoveryResult> RecoverAsync(Project project, IEditorDialogs dialogs, CancellationToken cancellationToken) =>
        Services.GetService<BackupOptions>() is null
            ? Task.FromResult(RecoveryResult.None)
            : Services.GetRequiredService<RecoveryService>().RecoverAsync(project, dialogs, cancellationToken);

    /// <summary>Starts following the session's classes for backups, when the host configured any.</summary>
    /// <param name="session">The session whose classes are backed up.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public void CreateBackups(ProjectSessionViewModel session, EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(context);
        if (backups is null && Services.GetService<BackupOptions>() is not null)
        {
            backups = new SessionBackups(session, Services.GetRequiredService<BackupService>(), context.Persistence.RenderClassAsync, context.Dispatcher);
        }
    }

    /// <summary>Stops following the session and disposes its container.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        backups?.Dispose();
        scope.Dispose();
        container.Dispose();
    }
}
