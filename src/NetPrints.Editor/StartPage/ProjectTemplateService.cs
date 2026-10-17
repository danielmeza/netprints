using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Editor.StartPage;

/// <summary>Creates a project from a registered project template.</summary>
[SessionScoped<ProjectTemplateService>(Factory = "Create")]
internal sealed class ProjectTemplateService(
    Func<IReadOnlyList<ProjectTemplateDescriptor>> templates,
    Func<string, IProjectProfile?> findProfile,
    IProjectSystem projects)
{
    private static readonly string[] ReservedDeviceNames =
        ["con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"];

    /// <summary>Creates the service over the registry's templates and the loaded extensions' profiles.</summary>
    /// <param name="registry">Holds the registered templates.</param>
    /// <param name="extensions">Provides the project profiles.</param>
    /// <param name="projects">Creates the project files.</param>
    /// <returns>The service.</returns>
    internal static ProjectTemplateService Create(IContributionRegistry registry, IExtensionHost extensions, IProjectSystem projects) =>
        new(() => registry.ProjectTemplates, profileId => FindProfile(extensions, profileId), projects);

    /// <summary>Finds a project profile among the loaded extensions' profiles and the default one.</summary>
    /// <param name="extensions">Provides the project profiles.</param>
    /// <param name="profileId">The profile id.</param>
    /// <returns>The profile, or <see langword="null"/> when none has that id.</returns>
    internal static IProjectProfile? FindProfile(IExtensionHost extensions, string profileId) =>
        extensions.Current.FindProfile(profileId) ?? (profileId == DefaultProjectProfile.ProfileId ? DefaultProjectProfile.Instance : null);

    /// <summary>Gets the registered templates, in registration order.</summary>
    public IReadOnlyList<ProjectTemplateDescriptor> Templates => templates();

    /// <summary>Checks a project name and folder without writing anything; the file system is read off the calling thread.</summary>
    /// <param name="name">The project name, which is also its root namespace.</param>
    /// <param name="folder">The full path of the folder the project is created in; a relative path is rejected, never resolved against the working folder.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The reason the input is rejected, or null when it is valid.</returns>
    public async Task<string?> ValidateAsync(string name, string folder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a project name.";
        }

        string[] segments = name.Split('.');
        if (segments.Any(segment => !IsIdentifier(segment)))
        {
            return $"'{name}' is not a valid C# namespace: use letters, digits and underscores, not starting with a digit, and no keywords.";
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || ReservedDeviceNames.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
        {
            return $"'{name}' is not a valid file name.";
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return "Choose a folder.";
        }

        if (!Path.IsPathRooted(folder))
        {
            return $"'{folder}' is not a full path. Enter the whole path of the folder, or start it with ~ for your home folder.";
        }

        return await Task.Run(() => CheckFolder(folder), cancellationToken).ConfigureAwait(true);
    }

    private static string? CheckFolder(string folder)
    {
        string full = Path.GetFullPath(folder);
        if (File.Exists(full))
        {
            return $"'{full}' is a file, not a folder.";
        }

        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
        {
            return $"'{full}' is not empty. Choose an empty or new folder.";
        }

        return null;
    }

    /// <summary>Creates the project; a failure or a cancellation removes what was written, and the folder too when this call created it.</summary>
    /// <param name="template">The template to create it from.</param>
    /// <param name="name">The project name.</param>
    /// <param name="folder">The empty or new folder to create it in.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The path of the new <c>.csproj</c>.</returns>
    /// <exception cref="ArgumentException">The name or the folder is rejected; nothing was written.</exception>
    /// <exception cref="InvalidOperationException">The template's profile is not provided; nothing was written.</exception>
    public async Task<string> CreateAsync(ProjectTemplateDescriptor template, string name, string folder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (await ValidateAsync(name, folder, cancellationToken).ConfigureAwait(true) is { } problem)
        {
            throw new ArgumentException(problem);
        }

        IProjectProfile profile = findProfile(template.ProfileId)
            ?? throw new InvalidOperationException($"The template '{template.DisplayName}' needs the project profile '{template.ProfileId}', which no loaded extension provides.");

        string directory = Path.GetFullPath(folder);
        bool createdFolder = !Directory.Exists(directory);
        Directory.CreateDirectory(directory);
        try
        {
            string csproj = await projects.CreateAsync(directory, name, profile, name, cancellationToken).ConfigureAwait(true);
            if (template.OutputType == ProjectOutputType.Library)
            {
                await projects.ApplyAsync(csproj, [new ProjectEdit.SetOutputType(BinaryType.SharedLibrary)], cancellationToken).ConfigureAwait(true);
            }

            foreach (TemplateFile file in template.Files ?? [])
            {
                await WriteFileAsync(directory, file, name, template.ProfileId, cancellationToken).ConfigureAwait(true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return csproj;
        }
        catch
        {
            RemoveWritten(directory, createdFolder);
            throw;
        }
    }

    private static string Replace(string text, string name, string profileId) =>
        text.Replace(TemplateFile.RootNamespaceToken, name, StringComparison.Ordinal)
            .Replace(TemplateFile.ProfileIdToken, profileId, StringComparison.Ordinal)
            .Replace(TemplateFile.SourceName, name, StringComparison.Ordinal);

    private static async Task WriteFileAsync(string directory, TemplateFile file, string name, string profileId, CancellationToken cancellationToken)
    {
        string relative = Replace(file.Path, name, profileId);
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        string rootedDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        if (Path.IsPathRooted(relative) || !path.StartsWith(rootedDirectory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The template file '{file.Path}' is outside the project folder.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? directory);
        await File.WriteAllTextAsync(path, Replace(file.Content, name, profileId), new UTF8Encoding(false), cancellationToken).ConfigureAwait(true);
    }

    private static bool IsIdentifier(string segment) =>
        SyntaxFacts.IsValidIdentifier(segment) && SyntaxFacts.GetKeywordKind(segment) == SyntaxKind.None;

    private static void RemoveWritten(string directory, bool createdFolder)
    {
        try
        {
            if (createdFolder)
            {
                Directory.Delete(directory, recursive: true);
                return;
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                File.Delete(file);
            }

            foreach (string child in Directory.GetDirectories(directory))
            {
                Directory.Delete(child, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left as it is: the caller reports the original failure.
        }
    }
}
