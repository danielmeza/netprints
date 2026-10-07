using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Projects;

namespace NetPrints.Editor.StartPage;

/// <summary>Creates a project from a registered project template.</summary>
internal sealed class ProjectTemplateService(
    Func<IReadOnlyList<ProjectTemplateDescriptor>> templates,
    Func<string, IProjectProfile?> findProfile,
    IProjectSystem projects)
{
    private static readonly string[] ReservedDeviceNames =
        ["con", "prn", "aux", "nul", "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9", "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"];

    /// <summary>Gets the registered templates, in registration order.</summary>
    public IReadOnlyList<ProjectTemplateDescriptor> Templates => templates();

    /// <summary>Checks a project name and folder without writing anything.</summary>
    /// <param name="name">The project name, which is also its root namespace.</param>
    /// <param name="folder">The folder the project is created in.</param>
    /// <returns>The reason the input is rejected, or null when it is valid.</returns>
    public string? Validate(string name, string folder)
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

    /// <summary>Creates the project; a failure removes what was written.</summary>
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
        if (Validate(name, folder) is { } problem)
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
            switch (template.OutputType)
            {
                case ProjectOutputType.Library:
                    await projects.ApplyAsync(csproj, [new ProjectEdit.SetOutputType(BinaryType.SharedLibrary)], cancellationToken).ConfigureAwait(true);
                    break;
                case ProjectOutputType.Console:
                    await File.WriteAllTextAsync(Path.Combine(directory, ProgramGraphSeed.FileName), ProgramGraphSeed.Render(name), new UTF8Encoding(false), cancellationToken)
                        .ConfigureAwait(true);
                    break;
            }

            return csproj;
        }
        catch
        {
            RemoveWritten(directory, createdFolder);
            throw;
        }
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
