using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>ADR-0017: the opt-in target turns only <c>NPXE</c> ids into <c>NoWarn</c>; any other item fails the build.</summary>
    public class ExperimentalOptInTargetTests
    {
        private static async Task<(int ExitCode, string Output)> RunAsync(string item)
        {
            string folder = Path.Combine(Path.GetTempPath(), "netprints-optin-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string targets = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "Directory.Build.targets");
                string project = Path.Combine(folder, "Probe.proj");
                await File.WriteAllTextAsync(project, $"""
                    <Project>
                      <ItemGroup>
                        <NetPrintsExperimentalOptIn Include="{item}" />
                      </ItemGroup>
                      <Target Name="CoreCompile" />
                      <Import Project="{targets}" />
                    </Project>
                    """, TestContext.Current.CancellationToken);

                var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add("msbuild");
                start.ArgumentList.Add(project);
                start.ArgumentList.Add("-t:CoreCompile");
                start.ArgumentList.Add("-nologo");
                start.ArgumentList.Add("-v:q");
                using Process process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start");
                Task<string> output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                Task<string> error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                await process.WaitForExitAsync(TestContext.Current.CancellationToken);
                return (process.ExitCode, await output + await error);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public async Task AnOptInItemThatIsNotAnExperimentalIdFailsTheBuild()
        {
            (int exitCode, string output) = await RunAsync("CS8602");

            Assert.NotEqual(0, exitCode);
            Assert.Contains("NetPrintsExperimentalOptIn accepts only NPXE ids", output, StringComparison.Ordinal);
            Assert.Contains("CS8602", output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task AnExperimentalIdIsAccepted()
        {
            (int exitCode, string output) = await RunAsync("NPXE0003");

            Assert.True(exitCode == 0, output);
        }
    }
}
