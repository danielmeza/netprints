using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Dock.Model.Services;

namespace NetPrints.Editor.Shell.Docking;

/// <summary>Dock's Mvvm root dock with the per-host overlay services its overlay controls bind. NetPrints shows no Dock overlay, so each service is an inert default.</summary>
internal sealed class ShellRootDock : RootDock, IHostOverlayServices
{
    /// <inheritdoc/>
    public IDockBusyService Busy { get; } = new InertBusy();

    /// <inheritdoc/>
    public IDockDialogService Dialogs { get; } = new InertDialogs();

    /// <inheritdoc/>
    public IDockConfirmationService Confirmations { get; } = new InertConfirmations();

    /// <inheritdoc/>
    public IDockGlobalBusyService GlobalBusyService { get; } = new InertGlobalOverlay();

    /// <inheritdoc/>
    public IDockGlobalDialogService GlobalDialogService { get; } = new InertGlobalOverlay();

    /// <inheritdoc/>
    public IDockGlobalConfirmationService GlobalConfirmationService { get; } = new InertGlobalOverlay();

    private sealed class Nothing : IDisposable
    {
        public static readonly Nothing Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class InertBusy : ObservableObject, IDockBusyService
    {
        public bool IsBusy => false;

        public string? Message => null;

        public bool IsReloadVisible { get; set; }

        public bool CanReload => false;

        public ICommand ReloadCommand { get; } = new RelayCommand(() => { }, () => false);

        public IDisposable Begin(string? message) => Nothing.Instance;

        public Task RunAsync(string message, Func<Task> work) => work();

        public void UpdateMessage(string? message)
        {
        }

        public void SetReloadHandler(Func<Task>? handler)
        {
        }
    }

    private sealed class InertConfirmations : ObservableObject, IDockConfirmationService
    {
        public ReadOnlyObservableCollection<ConfirmationRequest> Confirmations { get; } = new([]);

        public ConfirmationRequest? ActiveConfirmation => null;

        public bool HasConfirmations => false;

        public Task<bool> ConfirmAsync(string title, string message, string confirmText, string cancelText) => Task.FromResult(false);

        public void Close(ConfirmationRequest request, bool result)
        {
        }

        public void CancelAll()
        {
        }
    }

    private sealed class InertDialogs : ObservableObject, IDockDialogService
    {
        public ReadOnlyObservableCollection<DialogRequest> Dialogs { get; } = new([]);

        public DialogRequest? ActiveDialog => null;

        public bool HasDialogs => false;

        public Task<T?> ShowAsync<T>(object content, string? title = null) => Task.FromResult<T?>(default);

        public void Close(DialogRequest request, object? result)
        {
        }

        public void CancelAll()
        {
        }
    }

    private sealed class InertGlobalOverlay : ObservableObject, IDockGlobalBusyService, IDockGlobalDialogService, IDockGlobalConfirmationService
    {
        public bool IsBusy => false;

        public bool IsDialogOpen => false;

        public bool IsConfirmationOpen => false;

        public string? Message => null;

        public IDisposable Begin(string? message) => Nothing.Instance;
    }
}
