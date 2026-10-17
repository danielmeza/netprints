using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.Contributions;

/// <summary>
/// The one boundary every call into an extension's code goes through (research R27): it catches the exception, logs it with the extension's name and converts it.
/// A handler failure becomes an <see cref="ExtensionFailureException"/> the command invoker shows in the error dialog; a provider, factory or predicate failure
/// drops that call's result and is logged once per contribution and session (NPX011).
/// </summary>
/// <param name="logger">Receives the failures.</param>
internal sealed class ContributionGuard(ILogger logger)
{
    private readonly HashSet<string> reported = new(StringComparer.Ordinal);

    private ILogger Logger { get; } = logger;

    /// <summary>Wraps a command handler.</summary>
    /// <param name="handler">The extension's handler.</param>
    /// <param name="commandId">The command's id.</param>
    /// <param name="extensionName">Gives the display name of the extension.</param>
    /// <returns>The guarded handler.</returns>
    public ICommandHandler Command(ICommandHandler handler, string commandId, Func<string> extensionName) => new GuardedHandler(this, handler, commandId, extensionName);

    /// <summary>Wraps a tooltip provider.</summary>
    /// <param name="provider">The extension's provider.</param>
    /// <param name="extensionName">Gives the display name of the extension.</param>
    /// <returns>The guarded provider.</returns>
    public ITooltipProvider Tooltips(ITooltipProvider provider, Func<string> extensionName) => new GuardedTooltips(this, provider, extensionName);

    /// <summary>Wraps a go-to provider.</summary>
    /// <param name="provider">The extension's provider.</param>
    /// <param name="extensionName">Gives the display name of the extension.</param>
    /// <returns>The guarded provider.</returns>
    public IGoToProvider GoTo(IGoToProvider provider, Func<string> extensionName) => new GuardedGoTo(this, provider, extensionName);

    /// <summary>Wraps a view model factory: when it throws, the placeholder <see cref="ContributionErrorViewModel"/> takes its place.</summary>
    /// <typeparam name="TContext">The context the factory is given.</typeparam>
    /// <param name="factory">The extension's factory.</param>
    /// <param name="contribution">What the factory builds, for the message.</param>
    /// <param name="extensionName">Gives the display name of the extension.</param>
    /// <returns>The guarded factory.</returns>
    public Func<TContext, object> Factory<TContext>(Func<TContext, object> factory, string contribution, Func<string> extensionName) =>
        context =>
        {
            try
            {
                return factory(context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Report(ex, extensionName(), contribution);
                return new ContributionErrorViewModel(extensionName(), contribution, ex.Message);
            }
        };

    /// <summary>Wraps a predicate of an extension: when it throws, it answers <see langword="false"/>.</summary>
    /// <typeparam name="TContext">The context the predicate is given.</typeparam>
    /// <param name="predicate">The extension's predicate, or <see langword="null"/>.</param>
    /// <param name="contribution">What the predicate belongs to, for the message.</param>
    /// <param name="extensionName">Gives the display name of the extension.</param>
    /// <returns>The guarded predicate, or <see langword="null"/> when there was none.</returns>
    public Func<TContext, bool>? Predicate<TContext>(Func<TContext, bool>? predicate, string contribution, Func<string> extensionName) =>
        predicate is null
            ? null
            : context =>
            {
                try
                {
                    return predicate(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Report(ex, extensionName(), contribution);
                    return false;
                }
            };

    private void Report(Exception exception, string extension, string contribution)
    {
        lock (reported)
        {
            if (!reported.Add(extension + "|" + contribution))
            {
                return;
            }
        }

        Log.ContributionFailed(Logger, exception, extension, contribution);
    }

    private sealed class GuardedHandler(ContributionGuard guard, ICommandHandler inner, string commandId, Func<string> extensionName) : ICommandHandler
    {
        public bool CanExecute(ICommandContext context)
        {
            try
            {
                return inner.CanExecute(context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                guard.Report(ex, extensionName(), "the state of command " + commandId);
                return false;
            }
        }

        public async Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
        {
            try
            {
                await inner.ExecuteAsync(context, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string name = extensionName();
                Log.HandlerFailed(guard.Logger, ex, name, commandId);
                throw new ExtensionFailureException(name, commandId, ex);
            }
        }

        public string? DynamicLabel(ICommandContext context)
        {
            try
            {
                return inner.DynamicLabel(context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                guard.Report(ex, extensionName(), "the label of command " + commandId);
                return null;
            }
        }

        public bool? IsChecked(ICommandContext context)
        {
            try
            {
                return inner.IsChecked(context);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                guard.Report(ex, extensionName(), "the checked state of command " + commandId);
                return null;
            }
        }
    }

    private sealed class GuardedTooltips(ContributionGuard guard, ITooltipProvider inner, Func<string> extensionName) : ITooltipProvider
    {
        public int Order => inner.Order;

        public TooltipContent? TryProvide(TooltipTarget target)
        {
            try
            {
                return inner.TryProvide(target);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                guard.Report(ex, extensionName(), "tooltip provider " + inner.GetType().Name);
                return null;
            }
        }
    }

    private sealed class GuardedGoTo(ContributionGuard guard, IGoToProvider inner, Func<string> extensionName) : IGoToProvider
    {
        public string Kind => inner.Kind;

        public async IAsyncEnumerable<GoToItem> SearchAsync(string text, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string contribution = "go-to provider " + inner.GetType().Name;
            IAsyncEnumerator<GoToItem> enumerator;
            try
            {
                enumerator = inner.SearchAsync(text, cancellationToken).GetAsyncEnumerator(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                guard.Report(ex, extensionName(), contribution);
                yield break;
            }

            await using (enumerator.ConfigureAwait(true))
            {
                while (true)
                {
                    GoToItem item;
                    try
                    {
                        if (!await enumerator.MoveNextAsync().ConfigureAwait(true))
                        {
                            break;
                        }

                        item = enumerator.Current;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        guard.Report(ex, extensionName(), contribution);
                        break;
                    }

                    yield return item;
                }
            }
        }
    }
}
