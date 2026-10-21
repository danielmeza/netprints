using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Dialogs;

/// <summary>The New project dialog: a template, a name and a location; it creates the project in <c>location/name</c> and closes with the path of its <c>.csproj</c>.</summary>
public sealed partial class NewProjectDialogViewModel : DialogViewModel<string?>, IDisposable
{
    private const string BrowseTitle = "Choose the project folder";

    private readonly ProjectTemplateService service;
    private readonly IFilePickerService filePicker;
    private readonly ProjectLocations locations;
    private readonly TimeProvider time;
    private CancellationTokenSource? validation;
    private bool inputIsValid;
    private bool closeAfterCancel;

    internal NewProjectDialogViewModel(ProjectTemplateService service, IFilePickerService filePicker, ProjectLocations locations, TimeProvider time, string? profileId = null)
    {
        this.service = service;
        this.filePicker = filePicker;
        this.locations = locations;
        this.time = time;
        ProfileId = profileId;
        Location = locations.Last;
        Templates = [];
        ApplyFilter();
    }

    /// <summary>The pause in typing after which the name and folder are checked.</summary>
    internal static readonly TimeSpan ValidationDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Gets the profile the list is narrowed to (<c>--profile</c>), or null.</summary>
    public string? ProfileId { get; }

    /// <summary>Gets a value indicating whether the list is narrowed to <see cref="ProfileId"/> and the user can show all.</summary>
    public bool IsFiltered => ProfileId is not null;

    /// <summary>Gets the note that names the profile the list is narrowed to.</summary>
    public string ProfileNote => ProfileId is null ? "" : TextFormat.Of(Strings.NewProject_ProfileNote, ProfileId);

    /// <summary>Gets or sets a value indicating whether every template shows instead of the ones of <see cref="ProfileId"/>.</summary>
    [ObservableProperty]
    public partial bool ShowAll { get; set; }

    /// <summary>Gets the templates the user can choose from.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ProjectTemplateDescriptor> Templates { get; private set; }

    partial void OnShowAllChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        Templates = ProfileId is null || ShowAll ? service.Templates : [.. service.Templates.Where(t => string.Equals(t.ProfileId, ProfileId, StringComparison.Ordinal))];
        if (SelectedTemplate is null || !Templates.Contains(SelectedTemplate))
        {
            SelectedTemplate = Templates.Count > 0 ? Templates[0] : null;
        }
    }

    /// <summary>Gets or sets the chosen template.</summary>
    [ObservableProperty]
    public partial ProjectTemplateDescriptor? SelectedTemplate { get; set; }

    /// <summary>Gets or sets the project name, which is also its root namespace.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Folder))]
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets the parent folder the project folder is created in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Folder))]
    public partial string Location { get; set; } = "";

    /// <summary>Gets the project folder, <see cref="Location"/> and <see cref="Name"/> joined; empty until both are set. The dialog shows it as the preview.</summary>
    public string Folder => Location.Length > 0 && Name.Length > 0 ? Path.Combine(locations.Expand(Location), Name) : "";

    /// <summary>Gets a value indicating whether a check of the input is waiting for the typing to pause or running; Create stays disabled meanwhile.</summary>
    [ObservableProperty]
    public partial bool IsValidating { get; private set; }

    internal Task ValidationTask { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the reason the input is rejected, or the error of the last failed creation; null when there is none.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    partial void OnSelectedTemplateChanged(ProjectTemplateDescriptor? value) => Revalidate();

    partial void OnNameChanged(string value) => Revalidate();

    partial void OnLocationChanged(string value) => Revalidate();

    /// <summary>Cancels the pending check of the input.</summary>
    public void Dispose()
    {
        validation?.Cancel();
        validation?.Dispose();
        validation = null;
    }

    partial void OnIsValidatingChanged(bool value) => CreateCommand.NotifyCanExecuteChanged();

    private void Revalidate()
    {
        validation?.Cancel();
        validation?.Dispose();
        inputIsValid = false;
        Message = null;
        if (Name.Length == 0)
        {
            validation = null;
            IsValidating = false;
            CreateCommand.NotifyCanExecuteChanged();
            return;
        }

        validation = new CancellationTokenSource();
        IsValidating = true;
        ValidationTask = ValidateAfterPauseAsync(Name, Folder, validation.Token);
    }

    private async Task ValidateAfterPauseAsync(string name, string folder, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ValidationDelay, time, cancellationToken).ConfigureAwait(true);
            string? problem = await service.ValidateAsync(name, folder, cancellationToken).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            Message = problem;
            inputIsValid = problem is null;
            IsValidating = false;
        }
        catch (OperationCanceledException)
        {
            // Superseded by newer input, or the dialog is gone: the newer check reports.
        }
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await filePicker.OpenFolderAsync(BrowseTitle).ConfigureAwait(true) is { } folder)
        {
            Location = folder;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (SelectedTemplate is not { } template)
        {
            return;
        }

        try
        {
            string path = await service.CreateAsync(template, Name, Folder, cancellationToken).ConfigureAwait(true);
            locations.Remember(locations.Expand(Location));
            RequestClose(path);
        }
        catch (OperationCanceledException)
        {
            if (closeAfterCancel)
            {
                RequestClose(null);
            }
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
    }

    private bool CanCreate() => SelectedTemplate is not null && !IsValidating && inputIsValid;

    [RelayCommand]
    private void Cancel()
    {
        if (CreateCommand.IsRunning)
        {
            closeAfterCancel = true;
            CreateCommand.Cancel();
            return;
        }

        RequestClose(null);
    }

    /// <summary>Stops a creation still running and waits until what it wrote is removed; for a window closed without Cancel.</summary>
    /// <returns>A task that completes when no creation or check is running.</returns>
    internal async Task CancelCreationAsync()
    {
        if (validation is { } pending)
        {
            await pending.CancelAsync().ConfigureAwait(true);
        }

        if (CreateCommand.IsRunning)
        {
            CreateCommand.Cancel();
            if (CreateCommand.ExecutionTask is { } running)
            {
                try
                {
                    await running.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    // The creation removed what it wrote before it ended.
                }
            }
        }
    }
}
