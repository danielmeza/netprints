using NetPrints.Extensibility.Editor;

namespace NetPrints.Godot;

/// <summary>The Godot project template: the Godot project file, a <c>.gitignore</c> and the launch profiles for the game and the Godot editor.</summary>
public static class GodotTemplate
{
    /// <summary>The id of the template.</summary>
    public const string TemplateId = GodotExtension.Id + ".project";

    private const string StaleAssemblyNote = "Godot loads .godot/mono/temp/bin/Debug, so a Release build leaves a stale assembly.";

    /// <summary>Creates the template descriptor.</summary>
    /// <returns>The descriptor.</returns>
    public static ProjectTemplateDescriptor Create() => new(
        TemplateId,
        "Godot project",
        "A Godot 4 C# project with launch profiles for the game and the Godot editor.",
        GodotProfile.ProfileId,
        ProjectOutputType.Library,
        Files:
        [
            new TemplateFile("project.godot", ProjectFile),
            new TemplateFile(".gitignore", GitIgnore),
            new TemplateFile("Properties/launchSettings.json", LaunchSettings),
        ]);

    private static string ProjectFile =>
        $"""
        config_version=5

        [application]

        config/name="{TemplateFile.SourceName}"
        config/features=PackedStringArray("4.7", "C#")

        [dotnet]

        project/assembly_name="{TemplateFile.SourceName}"

        """;

    private static string GitIgnore =>
        """
        .godot/
        bin/
        obj/

        """;

    private static string LaunchSettings =>
        $$"""
        {
          "$schema": "https://json.schemastore.org/launchsettings.json",
          "profiles": {
            "Game (Debug build)": {
              "commandName": "Executable",
              "executablePath": "%{{GodotStatus.Variable}}%",
              "commandLineArgs": "--path .",
              "netprintsNote": "{{StaleAssemblyNote}}"
            },
            "Godot editor (Debug build)": {
              "commandName": "Executable",
              "executablePath": "%{{GodotStatus.Variable}}%",
              "commandLineArgs": "--editor --path .",
              "netprintsNote": "{{StaleAssemblyNote}}"
            }
          }
        }

        """;
}
