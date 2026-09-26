using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// Runs an external process (always <c>dotnet</c> in this suite) and captures both output streams
    /// concurrently, then waits for exit — not "read stdout, then wait for exit", which deadlocks once
    /// one stream's output exceeds its OS pipe buffer while the other still needs draining (flagged in
    /// sub-phase D/E's notes). Shared by every test that shells out to <c>dotnet</c> directly, as
    /// opposed to going through the production <see cref="NetPrints.Projects.IProcessRunner"/> — the
    /// third occurrence of this exact pattern (<c>SdkTargetsTests</c>, <c>SdkPackageTests</c>, and
    /// <c>MsBuildProjectSystemTests</c>), so factored out here as the sub-phase D notes anticipated.
    /// </summary>
    internal static class ExternalProcess
    {
        /// <summary>
        /// Starts <c>dotnet</c> with <paramref name="args"/> in <paramref name="workingDirectory"/> and
        /// waits for it to exit.
        /// </summary>
        /// <param name="workingDirectory">Working directory for the process.</param>
        /// <param name="environment">Extra environment variables to set, or <see langword="null"/> to
        /// inherit the test process's environment unchanged.</param>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>The exit code and the combined stdout/stderr output.</returns>
        public static async Task<(int ExitCode, string Output)> RunDotnetAsync(string workingDirectory,
            IReadOnlyDictionary<string, string>? environment, params string[] args)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory,
            };
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            if (environment is not null)
            {
                foreach ((string key, string value) in environment)
                {
                    startInfo.Environment[key] = value;
                }
            }

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await Task.WhenAll(stdOutTask, stdErrTask, process.WaitForExitAsync(TestContext.Current.CancellationToken));
            return (process.ExitCode, stdOutTask.Result + stdErrTask.Result);
        }
    }
}
