using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace NetPrints.Workspace.Run;

/// <summary>Reads <c>Properties/launchSettings.json</c> into <see cref="LaunchSettings"/>.</summary>
public static class LaunchSettingsReader
{
    private const string ProfilesMember = "profiles";
    private const string CommandNameMember = "commandName";
    private const string ExecutablePathMember = "executablePath";
    private const string CommandLineArgsMember = "commandLineArgs";
    private const string WorkingDirectoryMember = "workingDirectory";
    private const string EnvironmentVariablesMember = "environmentVariables";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads the file of a project folder.</summary>
    /// <param name="projectDirectory">The project folder.</param>
    /// <returns>The settings; <see cref="LaunchSettings.None"/> when the file is absent.</returns>
    public static LaunchSettings Read(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);

        string path = Path.GetFullPath(Path.Combine(projectDirectory, "Properties", "launchSettings.json"));
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return LaunchSettings.None;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Invalid(path, exception.Message, hasComments: false);
        }

        return Parse(text, path);
    }

    /// <summary>Parses the text of a launch settings file.</summary>
    /// <param name="json">The file text.</param>
    /// <param name="filePath">The full path, kept in the result and named in messages.</param>
    /// <returns>The settings, with NPR001 or NPR002 when the file has those problems.</returns>
    public static LaunchSettings Parse(string json, string filePath)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        bool hasComments = ContainsComment(json);
        try
        {
            using JsonDocument document = JsonDocument.Parse(json, DocumentOptions);
            return Build(document.RootElement, filePath, hasComments);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return Invalid(filePath, exception.Message, hasComments);
        }
    }

    private static bool ContainsComment(string json)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(json), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow, AllowTrailingCommas = true });
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.Comment)
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static LaunchSettings Build(JsonElement root, string filePath, bool hasComments)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid(filePath, "the root is not an object", hasComments);
        }

        var profiles = new List<LaunchProfile>();
        if (root.TryGetProperty(ProfilesMember, out JsonElement profilesElement))
        {
            if (profilesElement.ValueKind != JsonValueKind.Object)
            {
                return Invalid(filePath, "'profiles' is not an object", hasComments);
            }

            using JsonElement.ObjectEnumerator entries = profilesElement.EnumerateObject();
            foreach (JsonProperty entry in entries)
            {
                if (!TryReadProfile(entry.Name, entry.Value, out LaunchProfile? profile, out string? error))
                {
                    return Invalid(filePath, error, hasComments);
                }

                profiles.Add(profile);
            }
        }

        return new LaunchSettings
        {
            FilePath = filePath,
            Profiles = profiles,
            Problems = NameCollisions(profiles, filePath),
            HasComments = hasComments,
        };
    }

    private static bool TryReadProfile(string name, JsonElement element, [NotNullWhen(true)] out LaunchProfile? profile, [NotNullWhen(false)] out string? error)
    {
        profile = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            error = $"profile '{name}' is not an object";
            return false;
        }

        if (!element.TryGetProperty(CommandNameMember, out JsonElement commandName) || commandName.ValueKind != JsonValueKind.String)
        {
            error = $"profile '{name}' has no string commandName";
            return false;
        }

        if (!TryReadOptionalString(element, ExecutablePathMember, name, out string? executablePath, out error)
            || !TryReadOptionalString(element, CommandLineArgsMember, name, out string? commandLineArgs, out error)
            || !TryReadOptionalString(element, WorkingDirectoryMember, name, out string? workingDirectory, out error)
            || !TryReadEnvironment(element, name, out List<KeyValuePair<string, string>>? environment, out error))
        {
            return false;
        }

        profile = new LaunchProfile
        {
            Name = name,
            CommandName = commandName.GetString() ?? string.Empty,
            ExecutablePath = executablePath,
            CommandLineArgs = commandLineArgs,
            WorkingDirectory = workingDirectory,
            EnvironmentVariables = environment,
        };
        error = null;
        return true;
    }

    private static bool TryReadOptionalString(JsonElement element, string member, string profileName, out string? value, [NotNullWhen(false)] out string? error)
    {
        value = null;
        error = null;
        if (!element.TryGetProperty(member, out JsonElement property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            error = $"'{member}' of profile '{profileName}' is not a string";
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryReadEnvironment(JsonElement element, string profileName, [NotNullWhen(true)] out List<KeyValuePair<string, string>>? environment, [NotNullWhen(false)] out string? error)
    {
        environment = [];
        error = null;
        if (!element.TryGetProperty(EnvironmentVariablesMember, out JsonElement property))
        {
            return true;
        }

        bool valid = property.ValueKind == JsonValueKind.Object;
        if (valid)
        {
            using JsonElement.ObjectEnumerator variables = property.EnumerateObject();
            foreach (JsonProperty variable in variables)
            {
                if (variable.Value.ValueKind != JsonValueKind.String)
                {
                    valid = false;
                    break;
                }

                environment.Add(new KeyValuePair<string, string>(variable.Name, variable.Value.GetString() ?? string.Empty));
            }
        }

        if (valid)
        {
            return true;
        }

        environment = null;
        error = $"'{EnvironmentVariablesMember}' of profile '{profileName}' is not an object of strings";
        return false;
    }

    private static List<RunProblem> NameCollisions(List<LaunchProfile> profiles, string filePath)
    {
        var problems = new List<RunProblem>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LaunchProfile profile in profiles)
        {
            if (!seen.TryAdd(profile.Name, profile.Name)
                && !string.Equals(seen[profile.Name], profile.Name, StringComparison.Ordinal)
                && reported.Add(profile.Name))
            {
                problems.Add(new RunProblem(
                    NprDiagnosticCodes.ProfileNamesDifferByCase,
                    RunProblemSeverity.Error,
                    string.Create(CultureInfo.InvariantCulture, $"The profile names '{seen[profile.Name]}' and '{profile.Name}' in '{filePath}' differ only by case.")));
            }
        }

        return problems;
    }

    private static LaunchSettings Invalid(string filePath, string detail, bool hasComments) =>
        new()
        {
            FilePath = filePath,
            Problems =
            [
                new RunProblem(
                    NprDiagnosticCodes.LaunchSettingsInvalid,
                    RunProblemSeverity.Error,
                    string.Create(CultureInfo.InvariantCulture, $"'{filePath}' is not valid launch settings: {detail.TrimEnd('.')}.")),
            ],
            HasComments = hasComments,
        };
}
