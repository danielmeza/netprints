using System.Text.Json;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Workspace;
using NetPrints.Workspace.Run;

namespace NetPrints.Run.Testing;

/// <summary>The run scenarios of contracts/run-profiles.md §2 and §7.3a: one per rule, one per problem code. Names are unique.</summary>
public static class RunProfileCases
{
    private const string Tool = "{root}/bin/tool";
    private const string ProjectFile = "{root}/App/App.csproj";
    private const string AppFolder = "{root}/App";
    private const string LaunchProfileVariable = "DOTNET_LAUNCH_PROFILE";
    private const string ToolProfile = "Tool";
    private const string ToolBinary = "bin/tool";
    private const string MsBuildExePath = "MSBUILD_EXE_PATH";
    private const string DotnetHostPath = "DOTNET_HOST_PATH";
    private const string QuotedLine = "\"a b\" c\\\\d \"e\\\"f\" \"\"";

    private static readonly Lazy<IReadOnlyList<RunProfileCase>> Cases = new(Build);

    /// <summary>Gets every case.</summary>
    public static IReadOnlyList<RunProfileCase> All => Cases.Value;

    /// <summary>Gets the names of every case.</summary>
    public static IReadOnlyList<string> Names => [.. All.Select(c => c.Name)];

    /// <summary>Gets the names of the cases the editor can also express.</summary>
    public static IReadOnlyList<string> EditorNames => [.. All.Where(c => !c.CliOnly).Select(c => c.Name)];

    /// <summary>Finds a case by name.</summary>
    /// <param name="name">The case's name.</param>
    /// <returns>The case.</returns>
    public static RunProfileCase Named(string name) =>
        All.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal)) ?? throw new ArgumentException($"No run case is named '{name}'.", nameof(name));

    private static IReadOnlyList<RunProfileCase> Build() =>
    [
        new("ExpandEnvironment")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: "--home %HOME_DIR% --x", env: [("A", "%HOME_DIR%")])),
            Environment = Vars(("HOME_DIR", "alpha")),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, ["--home", "alpha", "--x"], AppFolder, ("A", "alpha")),
        },
        new("ExpandProperty")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: "--out $(OutDir) --name $(AssemblyName)")),
            Properties = Vars(("OutDir", "bin/Debug/"), ("AssemblyName", "App")),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, ["--out", "bin/Debug/", "--name", "App"], AppFolder),
        },
        new("SplitQuotesAndBackslashes")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: QuotedLine)),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, ["a b", "c\\\\d", "e\"f", string.Empty], AppFolder),
        },
        new("WorkingDirectoryRelative")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, workingDirectory: "work/dir")),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, [], "{root}/App/work/dir"),
        },
        new("WorkingDirectoryAbsent")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, [], AppFolder),
        },
        new("ExecutableWithSeparator")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "../bin/tool", workingDirectory: "work")),
            Files = [Empty("App/bin/tool")],
            ExpectedRequest = Run("{root}/App/bin/tool", [], "{root}/App/work"),
        },
        new("ExecutableOnPathLinux")
        {
            Platform = RunPlatform.Linux,
            NeedsColonFreeRoot = true,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "mytool")),
            Environment = Vars(("PATH", "{root}/other:{root}/bin")),
            Files = [Empty("bin/mytool")],
            ExpectedRequest = Run("{root}/bin/mytool", [], AppFolder),
        },
        new("ExecutableOnPathWindowsExe")
        {
            Platform = RunPlatform.Windows,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "mytool")),
            Environment = Vars(("Path", "{root}/other;{root}/bin")),
            Files = [Empty("bin/mytool.exe")],
            ExpectedRequest = Run("{root}/bin/mytool.exe", [], AppFolder),
        },
        new("ExecutableOnPathWindowsCom")
        {
            Platform = RunPlatform.Windows,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "mytool")),
            Environment = Vars(("PATH", "{root}/other;{root}/bin")),
            Files = [Empty("bin/mytool.com")],
            ExpectedRequest = Run("{root}/bin/mytool.com", [], AppFolder),
        },
        new("ExecutableOnPathRelativeEntry")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "tool")),
            Environment = Vars(("PATH", "bin")),
            Files = [Empty("App/bin/tool")],
            ExpectedProblems = [NprDiagnosticCodes.ExecutableNotFound],
        },
        new("MacAppBundle")
        {
            Platform = RunPlatform.MacOS,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "{root}/Tool.app")),
            Files =
            [
                new("Tool.app/Contents/Info.plist", Plist("ToolBin")),
                Empty("Tool.app/Contents/MacOS/ToolBin"),
            ],
            ExpectedRequest = Run("{root}/Tool.app/Contents/MacOS/ToolBin", [], AppFolder),
        },
        new("ProjectKind")
        {
            Platform = RunPlatform.Linux,
            Configuration = RunConfigurations.Release,
            LaunchSettingsJson = Settings(Kind("Run", "Project", args: "--flag x", env: [("K", "v")])),
            ExpectedRequest = Dotnet("Run", RunConfigurations.Release, [("K", "v")], ["--flag", "x"]),
        },
        new("ExecutableOnLibrary")
        {
            Platform = RunPlatform.Linux,
            OutputType = BinaryType.SharedLibrary,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, [], AppFolder),
        },
        new("ScrubExecutable")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, env: [("KEEP", "1")])),
            Files = [Empty(ToolBinary)],
            MsBuildVariables = [new(MsBuildExePath, null), new(DotnetHostPath, "{root}/user/dotnet")],
            ExpectedRequest = Run(Tool, [], AppFolder, [MsBuildExePath], ("KEEP", "1"), (DotnetHostPath, "{root}/user/dotnet")),
        },
        new("ScrubProject")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Kind("Run", "Project", env: [("KEEP", "1")])),
            MsBuildVariables = [new(MsBuildExePath, null), new(DotnetHostPath, "{root}/user/dotnet")],
            ExpectedRequest = Dotnet("Run", RunConfigurations.Debug, [("KEEP", "1")], [], [MsBuildExePath], (DotnetHostPath, "{root}/user/dotnet")),
        },
        new("ProfileVariableWinsOverScrub")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, env: [(MsBuildExePath, "mine"), (DotnetHostPath, "mine2")])),
            Files = [Empty(ToolBinary)],
            MsBuildVariables = [new(MsBuildExePath, null), new(DotnetHostPath, "original"), new(LaunchProfileVariable, null)],
            ExpectedRequest = Run(Tool, [], AppFolder, (MsBuildExePath, "mine"), (DotnetHostPath, "mine2")),
        },
        new("NoFile")
        {
            Platform = RunPlatform.Linux,
            ExpectedRequest = Dotnet(null, RunConfigurations.Debug, [], []),
        },
        new("NoLaunchProfileOption")
        {
            Platform = RunPlatform.Linux,
            CliOnly = true,
            NoLaunchProfile = true,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Dotnet(null, RunConfigurations.Debug, [], []),
        },
        new("ArgumentOverride")
        {
            Platform = RunPlatform.Linux,
            CliOnly = true,
            ArgumentOverride = ["--new", "x y"],
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: "--old", workingDirectory: "work", env: [("V", "1")])),
            Files = [Empty(ToolBinary)],
            ExpectedRequest = Run(Tool, ["--new", "x y"], "{root}/App/work", ("V", "1")),
        },
        new("Npr001InvalidJson")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = "{ not json",
            ExpectedProblems = [NprDiagnosticCodes.LaunchSettingsInvalid],
        },
        new("Npr002NamesDifferByCase")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe("A", Tool), Exe("a", Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.ProfileNamesDifferByCase],
        },
        new("Npr003Kind")
        {
            Platform = RunPlatform.Linux,
            ProfileName = "Web",
            LaunchSettingsJson = Settings(Kind("Web", "IISExpress"), Exe(ToolProfile, Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.KindNotRunnable],
        },
        new("Npr004UnknownProfile")
        {
            Platform = RunPlatform.Linux,
            CliOnly = true,
            ProfileName = "Missing",
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool)),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.UnknownProfile],
        },
        new("Npr005PathVariableUnset")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "%GODOT_UNSET%")),
            ExpectedProblems = [NprDiagnosticCodes.PathVariableUnset],
        },
        new("Npr006PathProperty")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, workingDirectory: "$(Nope)")),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.PropertyUndefined],
        },
        new("Npr006ArgumentPropertyWarns")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: "--a $(Nope)")),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.PropertyUndefined],
            ExpectedRequest = Run(Tool, ["--a", "$(Nope)"], AppFolder),
        },
        new("Npr007ArgumentVariableWarns")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, Tool, args: "--a %NOPE%", env: [("V", "%NOPE2%")])),
            Files = [Empty(ToolBinary)],
            ExpectedProblems = [NprDiagnosticCodes.ArgumentVariableUnset, NprDiagnosticCodes.ArgumentVariableUnset],
            ExpectedRequest = Run(Tool, ["--a", "%NOPE%"], AppFolder, ("V", "%NOPE2%")),
        },
        new("Npr008NotFound")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "{root}/bin/missing")),
            ExpectedProblems = [NprDiagnosticCodes.ExecutableNotFound],
        },
        new("Npr008Empty")
        {
            Platform = RunPlatform.Linux,
            LaunchSettingsJson = Settings(Exe(ToolProfile, string.Empty)),
            ExpectedProblems = [NprDiagnosticCodes.ExecutableNotFound],
        },
        new("Npr009Cmd")
        {
            Platform = RunPlatform.Windows,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "{root}/bin/run.cmd")),
            Files = [Empty("bin/run.cmd")],
            ExpectedProblems = [NprDiagnosticCodes.ScriptTargetRefused],
        },
        new("Npr010NoBundleExecutable")
        {
            Platform = RunPlatform.MacOS,
            LaunchSettingsJson = Settings(Exe(ToolProfile, "{root}/Bad.app")),
            Files = [new("Bad.app/Contents/Info.plist", "bplist00")],
            ExpectedProblems = [NprDiagnosticCodes.AppBundleUnresolved],
        },
        new("Npr011ProjectOnLibrary")
        {
            Platform = RunPlatform.Linux,
            OutputType = BinaryType.SharedLibrary,
            ProfileName = "Run",
            LaunchSettingsJson = Settings(Kind("Run", "Project")),
            ExpectedProblems = [NprDiagnosticCodes.ProjectKindNeedsExecutable],
        },
    ];

    private static KeyValuePair<string, string> Empty(string path) => new(path, string.Empty);

    private static Dictionary<string, string> Vars(params (string Name, string Value)[] entries) =>
        entries.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);

    private static ProcessStartRequest Run(string program, string[] arguments, string workingDirectory, params (string Name, string Value)[] variables) =>
        Run(program, arguments, workingDirectory, [], variables);

    private static ProcessStartRequest Run(string program, string[] arguments, string workingDirectory, string[] removals, params (string Name, string Value)[] variables) =>
        new(program, arguments, workingDirectory, Vars([.. variables, (LaunchProfileVariable, ToolProfile)])) { EnvironmentRemovals = removals };

    private static ProcessStartRequest Dotnet(
        string? profile,
        string configuration,
        (string Name, string Value)[] profileVariables,
        string[] tail,
        string[]? removals = null,
        params (string Name, string Value)[] setBack)
    {
        var arguments = new List<string> { "run", "--project", ProjectFile, "--no-build", "--no-launch-profile", "-c", configuration };
        foreach ((string name, string value) in profileVariables)
        {
            arguments.AddRange(["-e", $"{name}={value}"]);
        }

        if (profile is not null)
        {
            arguments.AddRange(["-e", $"{LaunchProfileVariable}={profile}"]);
        }

        if (tail.Length > 0)
        {
            arguments.Add("--");
            arguments.AddRange(tail);
        }

        return new ProcessStartRequest("dotnet", arguments, AppFolder, Vars(setBack)) { EnvironmentRemovals = removals ?? [] };
    }

    private static string Settings(params string[] profiles) => "{ \"profiles\": { " + string.Join(", ", profiles) + " } }";

    private static string Exe(string name, string path, string? args = null, string? workingDirectory = null, (string Name, string Value)[]? env = null) =>
        Kind(name, "Executable", path, args, workingDirectory, env);

    private static string Kind(string name, string kind, string? path = null, string? args = null, string? workingDirectory = null, (string Name, string Value)[]? env = null)
    {
        var members = new List<string> { Member("commandName", kind) };
        if (path is not null)
        {
            members.Add(Member("executablePath", path));
        }

        if (args is not null)
        {
            members.Add(Member("commandLineArgs", args));
        }

        if (workingDirectory is not null)
        {
            members.Add(Member("workingDirectory", workingDirectory));
        }

        if (env is { Length: > 0 })
        {
            members.Add($"\"environmentVariables\": {{ {string.Join(", ", env.Select(entry => Member(entry.Name, entry.Value)))} }}");
        }

        return $"{Quoted(name)}: {{ {string.Join(", ", members)} }}";
    }

    private static string Member(string name, string value) => $"{Quoted(name)}: {Quoted(value)}";

    private static string Quoted(string value) => $"\"{JsonEncodedText.Encode(value)}\"";

    private static string Plist(string executable) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
        + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
        + $"<plist version=\"1.0\"><dict><key>CFBundleName</key><string>Tool</string><key>CFBundleExecutable</key><string>{executable}</string></dict></plist>\n";
}
