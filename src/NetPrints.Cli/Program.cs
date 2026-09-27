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
        private const int BadArgumentsExitCode = 1;

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
                return 0;
            }

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning));

            // Must run before any Microsoft.Build type is loaded, hence the separate method below.
            if (!MsBuildRegistration.EnsureRegistered(loggerFactory.CreateLogger(nameof(MsBuildRegistration))))
            {
                Console.WriteLine("No .NET SDK could be found; nothing was built.");
                return 0;
            }

            return await BuildAsync(path, options.Run, loggerFactory).ConfigureAwait(false);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<int> BuildAsync(string path, bool run, ILoggerFactory loggerFactory)
        {
            Console.WriteLine("Compiling {0}", path);

            var processes = new ProcessRunner();
            var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0-dev"), processes,
                loggerFactory.CreateLogger<MsBuildProjectSystem>());

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

                return 0;
            }

            Console.WriteLine("Compilation succeeded.");

            if (run)
            {
                Console.WriteLine("Running...");
                ProcessResult output = await processes.RunAsync(projects.GetRunCommand(path), CancellationToken.None).ConfigureAwait(false);
                await Console.Out.WriteAsync(output.StandardOutput).ConfigureAwait(false);
                await Console.Error.WriteAsync(output.StandardError).ConfigureAwait(false);
            }

            return 1;
        }

        private static async Task<int> Main(string[] args) =>
            await Parser.Default.ParseArguments<CompileOptions>(args)
                .MapResult(CompileAsync, _ => Task.FromResult(BadArgumentsExitCode))
                .ConfigureAwait(false);
    }
}
