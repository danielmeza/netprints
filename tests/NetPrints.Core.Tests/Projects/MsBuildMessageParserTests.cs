using System.Collections.Generic;
using NetPrints.Projects;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// PS-T10 (project-system.md §7): <see cref="MsBuildMessageParser.Parse"/> against a representative
    /// slice of real build output — a csc error with path/line/col/code, a csc warning with the trailing
    /// "[project path]" suffix real invocations add, MSB/NU warnings with no line/column, a NetPrints
    /// generator error keeping its "(graph …, node …)" suffix inside the message, and lines that aren't
    /// in canonical format at all (ignored).
    /// </summary>
    public class MsBuildMessageParserTests
    {
        [Fact]
        public void ParsesEachCanonicalLineAndIgnoresEverythingElse()
        {
            string output = string.Join('\n',
                "Restore complete.",
                "/repo/App/Program.cs(12,34): error CS1002: ; expected",
                "/repo/App/Program.cs(20,3): warning CS0168: The variable 'x' is declared but never used [/repo/App/App.csproj]",
                "/repo/App/App.csproj : warning MSB3245: Could not resolve this reference.",
                "/repo/App/App.csproj : warning NU1701: Package 'Old' was restored using '.NETFramework' instead of the project target framework.",
                "/repo/App/Program.netpc.json(3,5): error NPD002: connection dropped (graph class, node n0000000000001)",
                "Build FAILED.",
                "1 Warning(s)",
                "1 Error(s)");

            IReadOnlyList<ProjectMessage> messages = MsBuildMessageParser.Parse(output);

            Assert.Equal(5, messages.Count);

            Assert.Equal(new ProjectMessage(ProjectMessageSeverity.Error, "CS1002", "; expected",
                "/repo/App/Program.cs", 12, 34), messages[0]);
            Assert.Equal(new ProjectMessage(ProjectMessageSeverity.Warning, "CS0168",
                "The variable 'x' is declared but never used", "/repo/App/Program.cs", 20, 3), messages[1]);
            Assert.Equal(new ProjectMessage(ProjectMessageSeverity.Warning, "MSB3245", "Could not resolve this reference.",
                "/repo/App/App.csproj", null, null), messages[2]);
            Assert.Equal(new ProjectMessage(ProjectMessageSeverity.Warning, "NU1701",
                "Package 'Old' was restored using '.NETFramework' instead of the project target framework.",
                "/repo/App/App.csproj", null, null), messages[3]);
            Assert.Equal(new ProjectMessage(ProjectMessageSeverity.Error, "NPD002",
                "connection dropped (graph class, node n0000000000001)",
                "/repo/App/Program.netpc.json", 3, 5), messages[4]);
        }

        [Fact]
        public void LineWithoutColumnParsesWithNullColumn()
        {
            IReadOnlyList<ProjectMessage> messages = MsBuildMessageParser.Parse(
                "/repo/App/Program.cs(7): info NPD004: layout entry ignored");

            ProjectMessage message = Assert.Single(messages);
            Assert.Equal(ProjectMessageSeverity.Info, message.Severity);
            Assert.Equal(7, message.Line);
            Assert.Null(message.Column);
        }
    }
}
