using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>FR-024, NPXE0008: the services of the extension that owns a command reach its context and its project session.</summary>
public sealed class ExtensionServicesWiringTests : IAsyncDisposable
{
    private const string ExtensionId = "testext";

    private const string CommandId = "testext.command.hello";

    private readonly TestEditor editor;
    private readonly string projectPath = TestPaths.CopyHelloWorldSample();

    public ExtensionServicesWiringTests()
    {
        var extension = new Registration(builder =>
        {
            builder.Services.AddSingleton<SingletonProbe>();
            builder.Services.AddScoped<SessionProbe>();
        });
        var manifest = new ExtensionManifest(ExtensionId, ExtensionId, "1.0.0", ExtensionId + ".dll", "1.0", []);
        var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly with { InProcess = [.. ExtensionLoaderOptions.BuiltInOnly.InProcess, (manifest, extension)] }, NullLoggerFactory.Instance);
        editor = TestEditor.Create(TestEditor.CreateReflectionHost, host);
    }

    public async ValueTask DisposeAsync()
    {
        await editor.DisposeAsync();
        TestPaths.TryDelete(projectPath);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ASessionGivesAnExtensionItsScopedServicesUntilTheSessionIsDisposed()
    {
        using ProjectSessionViewModel session = await OpenAsync();

        SessionProbe first = session.ExtensionServices(ExtensionId).GetRequiredService<SessionProbe>();

        Assert.Same(first, session.ExtensionServices(ExtensionId).GetRequiredService<SessionProbe>());
        Assert.Null(session.ExtensionServices("other.ext").GetService<SessionProbe>());

        session.Dispose();

        Assert.True(first.Disposed);
    }

    [Fact]
    public async Task ACommandContextCarriesTheServicesOfTheExtensionThatOwnsTheCommand()
    {
        using ProjectSessionViewModel session = await OpenAsync();
        var handler = new RecordingHandler();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.For(ExtensionId).AddCommand(new CommandDescriptor(CommandId, "Hello", handler));
        registry.Freeze();
        var invoker = new CommandInvoker(registry, new FixedContexts(() => new CommandContext(new FakeShell(), session, null, null, CommandSelection.None, Host: editor.Context)), _ => { });

        Assert.True(invoker.TryRun(registry.Commands.Single()));
        await Task.Yield();

        ICommandContext received = Assert.IsAssignableFrom<ICommandContext>(handler.Received);
        Assert.NotNull(received.Services.GetService<SingletonProbe>());
        Assert.NotNull(received.Session?.Services.GetService<SessionProbe>());
        Assert.Throws<InvalidOperationException>(() => received.Services.GetService<SessionProbe>());
    }

    [Fact]
    public async Task ADialogRegisteredWithAddDialogIsShownByIdWithItsDialogContext()
    {
        using ProjectSessionViewModel session = await OpenAsync();
        var shown = new List<object>();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        IEditorContributions facet = registry.For(ExtensionId);
        facet.AddDialog(new DialogDescriptor("testext.dialog.hello", "Hello", context => new HelloViewModel(context)));
        var handler = new ShowDialogHandler(shown);
        facet.AddCommand(new CommandDescriptor(CommandId, "Hello", handler));
        registry.Freeze();
        var invoker = new CommandInvoker(registry, new FixedContexts(() => new CommandContext(new FakeShell(), session, null, null, CommandSelection.None, Host: editor.Context)), _ => { });

        Assert.True(invoker.TryRun(registry.Commands.Single()));
        await handler.Done.Task.WaitAsync(TestContext.Current.CancellationToken);

        (string title, object viewModel) = Assert.Single(editor.Dialogs.ViewModelDialogs);
        Assert.Equal("Hello", title);
        HelloViewModel hello = Assert.IsType<HelloViewModel>(viewModel);
        Assert.Same(hello, Assert.Single(shown));
        Assert.NotNull(hello.Context.Shell);
        Assert.NotNull(hello.Context.Dialogs);
        Assert.NotNull(hello.Context.Services.GetService<SingletonProbe>());
    }

    [Fact]
    public async Task ShowingAnUnknownDialogIdFails()
    {
        using ProjectSessionViewModel session = await OpenAsync();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.Freeze();
        ICommandContext context = new CommandContext(new FakeShell(), session, null, null, CommandSelection.None, Host: editor.Context, Contributions: registry);

        await Assert.ThrowsAsync<ArgumentException>(() => context.Dialogs.ShowDialogAsync("testext.dialog.missing", TestContext.Current.CancellationToken));
    }

    private async Task<ProjectSessionViewModel> OpenAsync()
    {
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(projectPath, TestContext.Current.CancellationToken);
        return new ProjectSessionViewModel(loaded.Project, editor.Context);
    }

    private sealed class SingletonProbe;

    private sealed class HelloViewModel(IDialogContext context)
    {
        public IDialogContext Context { get; } = context;
    }

    private sealed class ShowDialogHandler(List<object> shown) : ICommandHandler
    {
        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CanExecute(ICommandContext context) => true;

        public async Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
        {
            shown.Add(await context.Dialogs.ShowDialogAsync("testext.dialog.hello", cancellationToken));
            Done.SetResult();
        }
    }

    private sealed class SessionProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class Registration(Action<IExtensionBuilder> register) : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => register(builder);
    }

    private sealed class RecordingHandler : ICommandHandler
    {
        public ICommandContext? Received { get; private set; }

        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
        {
            Received = context;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedContexts(Func<CommandContext> create) : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => create();
    }
}
