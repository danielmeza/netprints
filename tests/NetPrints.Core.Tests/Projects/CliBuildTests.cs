using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrintsCLI;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// R1-07: <c>NetPrintsCLI.Program.BuildAsync</c>'s exit codes against a fake <see cref="IProjectSystem"/>
    /// and <see cref="IProcessRunner"/>, so no real MSBuild build or child process is needed.
    /// </summary>
    public class CliBuildTests
    {
        private sealed class FakeProjectSystem : IProjectSystem
        {
            public BuildResult? Result { get; set; }
            public ProcessStartRequest RunCommand { get; set; } = new("dotnet", ["run"], "/tmp");

            public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) => Task.FromResult(Result ?? new BuildResult(true, [], null, ""));
            public ProcessStartRequest GetRunCommand(string projectFilePath) => RunCommand;
        }

        private sealed class FakeProcessRunner : IProcessRunner
        {
            public ProcessResult Result { get; set; } = new(0, "", "");
            public Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken) => Task.FromResult(Result);
        }

        [Fact]
        public async Task FailedBuildReturnsExitCode1()
        {
            var projects = new FakeProjectSystem
            {
                Result = new BuildResult(false,
                    [new ProjectMessage(ProjectMessageSeverity.Error, "CS1002", "; expected", "Program.cs", 1, 1)], null, ""),
            };

            int exitCode = await Program.BuildAsync("Program.csproj", run: false, projects, new FakeProcessRunner());

            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task SuccessfulBuildWithoutRunReturnsExitCode0()
        {
            var projects = new FakeProjectSystem();

            int exitCode = await Program.BuildAsync("Program.csproj", run: false, projects, new FakeProcessRunner());

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task SuccessfulRunPropagatesTheChildsNonZeroExitCode()
        {
            var projects = new FakeProjectSystem();
            var processes = new FakeProcessRunner { Result = new ProcessResult(5, "", "boom") };

            int exitCode = await Program.BuildAsync("Program.csproj", run: true, projects, processes);

            Assert.Equal(5, exitCode);
        }

        [Fact]
        public async Task SuccessfulRunWithZeroChildExitCodeReturnsExitCode0()
        {
            var projects = new FakeProjectSystem();
            var processes = new FakeProcessRunner { Result = new ProcessResult(0, "Hello, World!", "") };

            int exitCode = await Program.BuildAsync("Program.csproj", run: true, projects, processes);

            Assert.Equal(0, exitCode);
        }
    }
}
