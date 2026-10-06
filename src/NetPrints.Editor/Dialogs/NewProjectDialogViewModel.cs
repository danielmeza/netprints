using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;

namespace NetPrints.Editor.Dialogs;

/// <summary>The New project dialog: a template, a name and a folder; it creates the project and closes with the path of its <c>.csproj</c>.</summary>
public sealed partial class NewProjectDialogViewModel : DialogViewModel<string?>
{
    private const string BrowseTitle = "Choose the project folder";

    private readonly ProjectTemplateService service;
    private readonly IFilePickerService filePicker;

    internal NewProjectDialogViewModel(ProjectTemplateService service, IFilePickerService filePicker)
    {
        this.service = service;
        this.filePicker = filePicker;
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
    public partial string Name { get; set; } = "";

    /// <summary>Gets or sets the empty or new folder the project is created in.</summary>
    [ObservableProperty]
    public partial string Folder { get; set; } = "";

    /// <summary>Gets the reason the input is rejected, or the error of the last failed creation; null when there is none.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    partial void OnSelectedTemplateChanged(ProjectTemplateDescriptor? value) => Revalidate();

    partial void OnNameChanged(string value) => Revalidate();

    partial void OnFolderChanged(string value) => Revalidate();

    private void Revalidate()
    {
        Message = Name.Length > 0 || Folder.Length > 0 ? service.Validate(Name, Folder) : null;
        CreateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await filePicker.OpenFolderAsync(BrowseTitle).ConfigureAwait(true) is { } folder)
        {
            Folder = folder;
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
            RequestClose(await service.CreateAsync(template, Name, Folder, cancellationToken).ConfigureAwait(true));
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
