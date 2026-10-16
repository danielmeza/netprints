using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>The gate of research R4 steps 3 and 4: every registration resolves in the root and in a project session scope.</summary>
public sealed class CompositionResolveAllTests : IAsyncDisposable
{
    private readonly ExtensionHost extensions = TestExtensions.CreateBuiltIn();

    public ValueTask DisposeAsync() => extensions.DisposeAsync();

    [Fact]
    public void EveryRootRegistrationResolvesAndTheSessionScopedOnesAreNotAtTheRoot()
    {
        ServiceCollection services = [];
        services.AddEditorHost(Host());
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        ServiceDescriptor[] editor = [.. services.Where(IsEditorRegistration)];
        Assert.NotEmpty(editor);
        foreach (ServiceDescriptor descriptor in editor.Where(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton && descriptor.ServiceType != typeof(ShellHost)))
        {
            Assert.NotNull(provider.GetRequiredService(descriptor.ServiceType));
        }

        Assert.All(editor.Where(descriptor => descriptor.Lifetime == ServiceLifetime.Scoped), descriptor =>
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService(descriptor.ServiceType)));
    }

    [Fact]
    public void EverySessionScopedRegistrationResolvesInAProjectSessionScope()
    {
        ServiceCollection root = [];
        root.AddEditorHost(Host());
        using ServiceProvider provider = root.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        EditorContext context = provider.GetRequiredService<EditorContext>() with { Backups = new BackupOptions(new EditorDataPaths("state-root"), new RealEditorFileSystem(), TimeProvider.System, TimeSpan.FromSeconds(1)) };
        IContributionRegistry registry = provider.GetRequiredService<IContributionRegistry>();

        ServiceCollection session = [];
        session.AddSessionHost(context, registry);
        ServiceDescriptor[] scoped = [.. session.Where(descriptor => IsEditorRegistration(descriptor) && descriptor.Lifetime == ServiceLifetime.Scoped)];
        Assert.Contains(scoped, descriptor => descriptor.ServiceType == typeof(BackupService));
        Assert.Contains(scoped, descriptor => descriptor.ServiceType == typeof(BackupStore));
        Assert.Contains(scoped, descriptor => descriptor.ServiceType == typeof(RecoveryService));
        Assert.Contains(scoped, descriptor => descriptor.ServiceType == typeof(ProjectTemplateService));

        using ProjectSessionScope scope = ProjectSessionScope.Create(context, registry, new SessionData("/project/Project.csproj", _ => { }));

        foreach (ServiceDescriptor descriptor in scoped)
        {
            Assert.NotNull(scope.Services.GetRequiredService(descriptor.ServiceType));
        }
    }

    [Fact]
    public void ASessionScopeWithoutBackupOptionsRecoversNothingAndStillOffersTemplates()
    {
        ServiceCollection root = [];
        root.AddEditorHost(Host());
        using ServiceProvider provider = root.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        using ProjectSessionScope scope = ProjectSessionScope.Create(provider.GetRequiredService<EditorContext>(), provider.GetRequiredService<IContributionRegistry>(), new SessionData(null, _ => { }));

        Assert.Null(scope.Services.GetService<BackupOptions>());
        Assert.NotNull(scope.Templates);
        Assert.Throws<InvalidOperationException>(() => scope.Services.GetRequiredService<BackupStore>());
    }

    private static bool IsEditorRegistration(ServiceDescriptor descriptor) =>
        descriptor.ServiceType.Namespace?.StartsWith("NetPrints.Editor", StringComparison.Ordinal) == true;

    private EditorHostServices Host() =>
        new(NullLoggerFactory.Instance, extensions, new FakeSettingsStore(), NullHostChannel.Instance, HostChannelError: null,
            MsBuildAvailable: false, DisposeOwnedResources: () => ValueTask.CompletedTask);
}
