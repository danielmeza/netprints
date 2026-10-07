using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Dialogs;

/// <summary>The New project dialog: a template, a name and a location; it creates the project in <c>location/name</c> and closes with the path of its <c>.csproj</c>.</summary>
public sealed partial class NewProjectDialogViewModel : DialogViewModel<string?>
{
    private const string BrowseTitle = "Choose the project folder";

    private readonly ProjectTemplateService service;
    private readonly IFilePickerService filePicker;
    private readonly ProjectLocations locations;

    internal NewProjectDialogViewModel(ProjectTemplateService service, IFilePickerService filePicker, ProjectLocations locations)
    {
        this.service = service;
        this.filePicker = filePicker;
        this.locations = locations;
        Location = locations.Last;
        Templates = service.Templates;
        SelectedTemplate = Templates.Count > 0 ? Templates[0] : null;
    }

    /// <summary>Gets the templates the user can choose from.</summary>
    public IReadOnlyList<ProjectTemplateDescriptor> Templates { get; }

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
    public string Folder => Location.Length > 0 && Name.Length > 0 ? Path.Combine(Location, Name) : "";

    /// <summary>Gets the reason the input is rejected, or the error of the last failed creation; null when there is none.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    partial void OnSelectedTemplateChanged(ProjectTemplateDescriptor? value) => Revalidate();

    partial void OnNameChanged(string value) => Revalidate();

    partial void OnLocationChanged(string value) => Revalidate();

    private void Revalidate()
    {
        Message = Name.Length > 0 ? service.Validate(Name, Folder) : null;
        CreateCommand.NotifyCanExecuteChanged();
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
            locations.Remember(Location);
            RequestClose(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Message = ex.Message;
        }
    }

    private bool CanCreate() => SelectedTemplate is not null && service.Validate(Name, Folder) is null;

    [RelayCommand]
    private void Cancel() => RequestClose(null);
}
