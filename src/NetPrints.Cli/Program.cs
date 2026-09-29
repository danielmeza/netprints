using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrintsCLI
{
    sealed class Program
    {
        /// <summary>Exit code: the project built (and, with <c>--run</c>, ran) successfully.</summary>
        private const int ExitSuccess = 0;

        /// <summary>Exit code: the project was not found, or the build failed.</summary>
        private const int ExitBuildFailed = 1;

        /// <summary>Exit code: bad command-line arguments (matches the generator's <c>ExitBadRequest</c>).</summary>
        private const int BadArgumentsExitCode = 2;

        /// <summary>Exit code: no compatible .NET SDK is registered (matches <c>ProjectCheck.ExitNoSdk</c>).</summary>
        private const int ExitNoSdk = 3;

        public sealed class CompileOptions
        {
            [Option('p', "project-path", Required = false, HelpText = "Path to the project file (.csproj).")]
            public string? ProjectPath { get; set; }

            [Option('r', "run", Required = false, HelpText = "Whether to run the executable on success.")]
            public bool Run { get; set; }
        }

        private static async Task<int> CompileAsync(CompileOptions options)
        {
            string? path = options.ProjectPath;
            if (path is null || !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Only .csproj projects are supported: '{0}' was not built.", path);
                return BadArgumentsExitCode;
            }

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                Console.WriteLine("Could not find project '{0}'.", path);
                return ExitBuildFailed;
            }

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));

            // Must run before any Microsoft.Build type is loaded, hence the separate method below.
            if (!MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration))))
            {
                Console.WriteLine("No .NET SDK could be found; nothing was built.");
                return ExitNoSdk;
            }

            return await BuildWithMsBuildAsync(path, options.Run, loggerFactory).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Task<int> BuildWithMsBuildAsync(string path, bool run, ILoggerFactory loggerFactory)
        {
            var processes = new ProcessRunner();
            var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0-dev"), processes,
                loggerFactory.CreateLogger<MsBuildProjectSystem>());

            return BuildAsync(path, run, projects, processes);
        }

        /// <summary>
        /// Core of the build: takes <paramref name="projects"/> and <paramref name="processes"/> as
        /// interfaces so a fake <see cref="IProjectSystem"/> can exercise it without MSBuild.
        /// </summary>
        internal static async Task<int> BuildAsync(string path, bool run, IProjectSystem projects, IProcessRunner processes)
        {
            Console.WriteLine("Compiling {0}", path);

            BuildResult result = await projects.BuildAsync(path, CancellationToken.None).ConfigureAwait(false);

            if (!result.Success)
            {
                var errors = result.Messages.Where(message => message.Severity == ProjectMessageSeverity.Error).ToList();
                Console.WriteLine($"Compilation failed with {errors.Count} errors:");
                foreach (ProjectMessage error in errors)
                {
                    string location = error.File is null ? "" : error.Line is null ? $"{error.File}: " : $"{error.File}({error.Line},{error.Column}): ";
                    Console.WriteLine($"{location}{error.Code}: {error.Message}");
                }

                return ExitBuildFailed;
            }

            Console.WriteLine("Compilation succeeded.");

            if (!run)
            {
                return ExitSuccess;
            }

            Console.WriteLine("Running...");
            ProcessResult output = await processes.RunAsync(projects.GetRunCommand(path), CancellationToken.None).ConfigureAwait(false);
            await Console.Out.WriteAsync(output.StandardOutput).ConfigureAwait(false);
            await Console.Error.WriteAsync(output.StandardError).ConfigureAwait(false);
            return output.ExitCode;
        }

        private static async Task<int> Main(string[] args) =>
            await Parser.Default.ParseArguments<CompileOptions>(args)
                .MapResult(CompileAsync, _ => Task.FromResult(BadArgumentsExitCode))
                .ConfigureAwait(false);
    }
}
