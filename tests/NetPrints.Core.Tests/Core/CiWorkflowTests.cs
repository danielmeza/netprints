using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// The CI layout of <c>specs/005-editor-shell/contracts/ci.md</c> §1 and §2: every test project runs in the
    /// <c>test</c> matrix, <c>Build and test (Linux)</c> aggregates every test-running job, and the Windows CLI
    /// workflow triggers on everything the CLI tests build from. The workflow files are read with the small
    /// indentation-aware reader at the end of this class, so the tests add no package.
    /// </summary>
    public partial class CiWorkflowTests
    {
        private const string CiWorkflow = ".github/workflows/ci.yml";
        private const string CliWindowsWorkflow = ".github/workflows/cli-windows.yml";
        private const string AggregateJobName = "Build and test (Linux)";

        private static readonly string[] NotAggregated = ["build-test", "e2e", "packages"];

        [Fact]
        public void TestMatrixCoversEveryTestProjectExactlyOnce()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string[] expected =
            [
                .. Directory.EnumerateDirectories(Path.Combine(root, "tests"))
                    .Select(directory => Path.GetFileName(directory))
                    .Where(name => name.EndsWith(".Tests", StringComparison.Ordinal) || name.EndsWith(".UITests", StringComparison.Ordinal))
                    .Where(name => name != "NetPrints.Desktop.E2ETests")
                    .Where(name => File.Exists(Path.Combine(root, "tests", name, name + ".csproj")))
                    .Select(name => "tests/" + name)
                    .Order(StringComparer.Ordinal),
            ];

            string[] actual = [.. MatrixLegs().Select(leg => Text(Get(leg, "project"))).Order(StringComparer.Ordinal)];

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TestMatrixHasTheContractLegsWithUniqueNames()
        {
            List<object?> legs = MatrixLegs();
            string[] names = [.. legs.Select(leg => Text(Get(leg, "name")))];
            string[] slugs = [.. legs.Select(leg => Text(Get(leg, "leg")))];

            Assert.Equal(["Core", "Catalog", "CLI", "Editor", "Editor UI (headless)"], names);
            Assert.All(slugs, slug => Assert.NotEmpty(slug));
            Assert.Equal(slugs.Length, slugs.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void TestMatrixDoesNotFailFast()
        {
            Assert.Equal("false", Text(Get(Ci(), "jobs", "test", "strategy", "fail-fast")));
        }

        [Fact]
        public void EveryLegUploadsItsOwnResultsAndCoverage()
        {
            string[] uploaded =
            [
                .. Items(Get(Ci(), "jobs", "test", "steps"))
                    .Where(step => Text(Get(step, "uses")).StartsWith("actions/upload-artifact@", StringComparison.Ordinal))
                    .Select(step => Text(Get(step, "with", "name"))),
            ];

            Assert.Contains("test-results-${{ matrix.leg }}", uploaded);
            Assert.Contains("coverage-${{ matrix.leg }}", uploaded);
        }

        [Fact]
        public void TestMatrixLegsRunTheirOwnProjectWithTheAdr0008CoverageSettings()
        {
            string[] runs = [.. Items(Get(Ci(), "jobs", "test", "steps")).Select(step => Text(Get(step, "run")))];
            string testRun = Assert.Single(runs, run => run.Contains("dotnet test", StringComparison.Ordinal));

            Assert.Contains("${{ matrix.project }}", testRun, StringComparison.Ordinal);
            Assert.Contains("--coverage-settings", testRun, StringComparison.Ordinal);
            Assert.Contains("tests/CodeCoverage.config", testRun, StringComparison.Ordinal);
            Assert.DoesNotContain("--ignore-exit-code", testRun, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildAndTestAggregatesEveryTestRunningJob()
        {
            Dictionary<string, object?> jobs = Jobs();
            object? aggregate = Get(jobs, "build-test");

            Assert.Equal(AggregateJobName, Text(Get(aggregate, "name")));
            Assert.Contains("always()", Text(Get(aggregate, "if")), StringComparison.Ordinal);

            string[] expectedNeeds =
            [
                .. jobs.Where(job => !NotAggregated.Contains(job.Key, StringComparer.Ordinal))
                    .Where(job => Items(Get(job.Value, "steps")).Any(step => Text(Get(step, "run")).Contains("dotnet test", StringComparison.Ordinal)))
                    .Select(job => job.Key)
                    .Order(StringComparer.Ordinal),
            ];
            string[] needs = [.. Needs(aggregate).Order(StringComparer.Ordinal)];
            Assert.Contains("test", needs);
            Assert.Equal(expectedNeeds, needs);

            object? step = Assert.Single(Items(Get(aggregate, "steps")));
            string script = Text(Get(step, "run"));
            Assert.All(needs, id => Assert.Contains($"needs.{id}.result", script, StringComparison.Ordinal));
            Assert.Contains("success", script, StringComparison.Ordinal);
        }

        [Fact]
        public void RequiredCheckNamesAreUnchanged()
        {
            Dictionary<string, object?> jobs = Jobs();

            Assert.Equal(AggregateJobName, Text(Get(jobs, "build-test", "name")));
            Assert.Equal("Desktop E2E (Linux, Xvfb)", Text(Get(jobs, "e2e", "name")));
        }

        [Fact]
        public void CliWindowsPathFilterCoversTheCliTestsBuildInputs()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string workflow = Path.Combine(root, CliWindowsWorkflow);
            Assert.SkipUnless(File.Exists(workflow), $"{CliWindowsWorkflow} does not exist yet; T011 creates it and this check then runs.");

            string[] closure = ProjectClosure(root, "tests/NetPrints.Cli.Tests/NetPrints.Cli.Tests.csproj");
            Assert.Contains("src/NetPrints.Cli", closure);

            string[] buildFiles =
            [
                "tests/Fixtures/**",
                "Directory.*",
                "src/Directory.Build.props",
                "src/BannedSymbols*.txt",
                "tests/Directory.Build.props",
                "global.json",
                "NuGet.config",
                CliWindowsWorkflow,
            ];

            object? trigger = Get(Read(workflow), "on");
            foreach (string eventName in new[] { "pull_request", "push" })
            {
                string[] paths = [.. Items(Get(trigger, eventName, "paths")).Select(Text)];
                string[] uncovered =
                [
                    .. closure.Where(directory => !paths.Any(path => path.EndsWith("/**", StringComparison.Ordinal)
                        && (directory == path[..^3] || directory.StartsWith(path[..^2], StringComparison.Ordinal))))
                    .Concat(buildFiles.Where(file => !paths.Contains(file, StringComparer.Ordinal))),
                ];

                Assert.True(uncovered.Length == 0, $"{eventName} paths in {CliWindowsWorkflow} miss: {string.Join(", ", uncovered)}");
            }
        }

        private static string[] ProjectClosure(string root, string projectRelativePath)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(Path.Combine(root, projectRelativePath)));

            while (pending.Count > 0)
            {
                string project = pending.Pop();
                if (!seen.Add(project))
                {
                    continue;
                }

                string directory = Path.GetDirectoryName(project) ?? root;
                foreach (string? include in XDocument.Load(project).Descendants("ProjectReference").Select(reference => (string?)reference.Attribute("Include")))
                {
                    if (!string.IsNullOrEmpty(include))
                    {
                        pending.Push(Path.GetFullPath(Path.Combine(directory, include.Replace('\\', '/'))));
                    }
                }
            }

            return
            [
                .. seen.Select(project => Path.GetRelativePath(root, Path.GetDirectoryName(project) ?? root).Replace('\\', '/'))
                    .Order(StringComparer.Ordinal),
            ];
        }

        private static object? Ci() => Read(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), CiWorkflow));

        private static Dictionary<string, object?> Jobs() =>
            Get(Ci(), "jobs") as Dictionary<string, object?> ?? throw new InvalidOperationException("ci.yml has no jobs.");

        private static List<object?> MatrixLegs() => Items(Get(Ci(), "jobs", "test", "strategy", "matrix", "include"));

        private static string[] Needs(object? job) => Get(job, "needs") switch
        {
            List<object?> list => [.. list.Select(Text)],
            string single => [single],
            _ => [],
        };

        private static object? Read(string path) => new YamlReader(File.ReadAllLines(path)).Parse();

        private static object? Get(object? node, params string[] path)
        {
            foreach (string key in path)
            {
                node = node is Dictionary<string, object?> map && map.TryGetValue(key, out object? value) ? value : null;
            }

            return node;
        }

        private static List<object?> Items(object? node) => node as List<object?> ?? [];

        private static string Text(object? node) => node as string ?? string.Empty;

        /// <summary>
        /// The subset of YAML the workflows use: block mappings and sequences by indentation, flow sequences,
        /// literal and folded scalars, quoted scalars and comments. Anchors, tags and flow mappings are not supported.
        /// </summary>
        private sealed partial class YamlReader(string[] lines)
        {
            private readonly string[] lines = lines;
            private int position;

            public object? Parse() => ParseBlock(0);

            [GeneratedRegex(@"^([\w.\-]+):(?:\s+(.*))?$")]
            private static partial Regex KeyValue();

            private static bool IsItem(string text) => text == "-" || text.StartsWith("- ", StringComparison.Ordinal);

            private static string StripComment(string line)
            {
                bool single = false;
                bool dbl = false;
                for (int index = 0; index < line.Length; index++)
                {
                    char c = line[index];
                    if (c == '\'' && !dbl)
                    {
                        single = !single;
                    }
                    else if (c == '"' && !single)
                    {
                        dbl = !dbl;
                    }
                    else if (c == '#' && !single && !dbl && (index == 0 || char.IsWhiteSpace(line[index - 1])))
                    {
                        return line[..index];
                    }
                }

                return line;
            }

            private static string Unquote(string value) =>
                value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
                    ? value[1..^1]
                    : value;

            private static object Scalar(string value) =>
                value.StartsWith('[') && value.EndsWith(']')
                    ? new List<object?>(value[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(item => (object?)Unquote(item)))
                    : Unquote(value);

            private (int Indent, string Text)? Peek()
            {
                while (position < lines.Length)
                {
                    string stripped = StripComment(lines[position]).TrimEnd();
                    if (stripped.Trim().Length > 0)
                    {
                        string text = stripped.TrimStart();
                        return (stripped.Length - text.Length, text);
                    }

                    position++;
                }

                return null;
            }

            private object? ParseBlock(int minimumIndent)
            {
                if (Peek() is not { } line || line.Indent < minimumIndent)
                {
                    return null;
                }

                return IsItem(line.Text) ? ParseSequence(line.Indent) : ParseMapping(line.Indent);
            }

            private List<object?> ParseSequence(int indent)
            {
                var list = new List<object?>();
                while (Peek() is { } line && line.Indent == indent && IsItem(line.Text))
                {
                    string rest = line.Text.Length > 1 ? line.Text[2..].TrimStart() : string.Empty;
                    if (rest.Length == 0)
                    {
                        position++;
                        list.Add(ParseBlock(indent + 1));
                    }
                    else if (KeyValue().IsMatch(rest))
                    {
                        int restIndent = indent + (line.Text.Length - rest.Length);
                        lines[position] = new string(' ', restIndent) + rest;
                        list.Add(ParseMapping(restIndent));
                    }
                    else
                    {
                        position++;
                        list.Add(Scalar(rest));
                    }
                }

                return list;
            }

            private Dictionary<string, object?> ParseMapping(int indent)
            {
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                while (Peek() is { } line && line.Indent == indent && !IsItem(line.Text))
                {
                    Match match = KeyValue().Match(line.Text);
                    if (!match.Success)
                    {
                        throw new FormatException($"Unsupported YAML on line {position + 1}: {line.Text}");
                    }

                    string key = match.Groups[1].Value;
                    string value = match.Groups[2].Value.Trim();
                    position++;

                    if (value.Length == 0)
                    {
                        map[key] = Peek() is { } next && (next.Indent > indent || (next.Indent == indent && IsItem(next.Text)))
                            ? ParseBlock(next.Indent)
                            : null;
                    }
                    else if (value[0] is '|' or '>')
                    {
                        map[key] = ReadBlockScalar(indent);
                    }
                    else
                    {
                        map[key] = Scalar(value);
                    }
                }

                return map;
            }

            private string ReadBlockScalar(int parentIndent)
            {
                var text = new List<string>();
                while (position < lines.Length
                    && (lines[position].Trim().Length == 0 || lines[position].Length - lines[position].TrimStart().Length > parentIndent))
                {
                    text.Add(lines[position].Trim());
                    position++;
                }

                return string.Join('\n', text).Trim();
            }
        }
    }
}
