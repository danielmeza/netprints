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

        private static readonly string[] NotAggregated = ["build-test", "e2e", "packages", "perf"];

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

            string[] actual = [.. MatrixLegs().Select(leg => Text(Get(leg, "project"))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

            Assert.NotEmpty(expected);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void TheUiShardsRunEveryTestNamespaceExactlyOnce()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string project = Path.Combine(root, "tests", "NetPrints.Editor.UITests");
            List<(string Include, string Exclude)> shards =
            [
                .. MatrixLegs()
                    .Where(leg => Text(Get(leg, "project")) == "tests/NetPrints.Editor.UITests")
                    .Select(leg => Text(Get(leg, "filter")))
                    .Select(filter => (NamespaceFilters(filter, "--filter-namespace"), NamespaceFilters(filter, "--filter-not-namespace"))),
            ];
            Assert.Equal(4, shards.Count);

            string[] namespaces =
            [
                .. Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                    .Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    .Select(File.ReadAllText)
                    .Where(text => TestAttribute().IsMatch(text))
                    .Select(text => NamespaceDeclaration().Match(text).Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];
            Assert.NotEmpty(namespaces);
            Assert.DoesNotContain(string.Empty, namespaces);

            foreach (string name in namespaces)
            {
                int running = shards.Count(shard => RunsNamespace(shard, name));
                Assert.True(running == 1, $"Test namespace {name} runs in {running} UI shards, expected exactly 1.");
            }

            foreach ((string include, _) in shards)
            {
                foreach (string name in include.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    Assert.Contains(name, namespaces);
                }
            }
        }

        private static bool RunsNamespace((string Include, string Exclude) shard, string name)
        {
            string[] include = shard.Include.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] exclude = shard.Exclude.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (include.Length == 0 || include.Contains(name, StringComparer.Ordinal)) && !exclude.Contains(name, StringComparer.Ordinal);
        }

        private static string NamespaceFilters(string filter, string option) =>
            string.Join(' ', Regex.Matches(filter, Regex.Escape(option) + @"\s+(\S+)").Select(match => match.Groups[1].Value));

        [GeneratedRegex(@"^\s*\[(Avalonia)?(Fact|Theory)\b", RegexOptions.Multiline)]
        private static partial Regex TestAttribute();

        [GeneratedRegex(@"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline)]
        private static partial Regex NamespaceDeclaration();

        [Fact]
        public void TestMatrixHasTheContractLegsWithUniqueNames()
        {
            List<object?> legs = MatrixLegs();
            string[] names = [.. legs.Select(leg => Text(Get(leg, "name")))];
            string[] slugs = [.. legs.Select(leg => Text(Get(leg, "leg")))];

            Assert.Equal(["Core", "Analyzers", "Catalog", "CLI", "Editor", "Editor UI (headless) 1/4", "Editor UI (headless) 2/4", "Editor UI (headless) 3/4", "Editor UI (headless) 4/4"], names);
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
        public void TheContactSheetIsUploadedFromTheShardThatRunsTheSnapshots()
        {
            string snapshots = "NetPrints.Editor.UITests.Snapshots";
            string[] legs =
            [
                .. MatrixLegs()
                    .Where(leg => Text(Get(leg, "project")) == "tests/NetPrints.Editor.UITests")
                    .Where(leg => RunsNamespace((NamespaceFilters(Text(Get(leg, "filter")), "--filter-namespace"), NamespaceFilters(Text(Get(leg, "filter")), "--filter-not-namespace")), snapshots))
                    .Select(leg => Text(Get(leg, "leg"))),
            ];
            string leg = Assert.Single(legs);

            object? upload = Assert.Single(Items(Get(Ci(), "jobs", "test", "steps")), step => Text(Get(step, "with", "name")) == "contact-sheet");

            Assert.StartsWith("actions/upload-artifact@", Text(Get(upload, "uses")), StringComparison.Ordinal);
            Assert.Equal("TestResults/ui/contact-sheet.png", Text(Get(upload, "with", "path")));
            Assert.Contains($"matrix.leg == '{leg}'", Text(Get(upload, "if")), StringComparison.Ordinal);
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
            Assert.DoesNotContain("||", script, StringComparison.Ordinal);
            Assert.Null(Get(step, "continue-on-error"));
            Assert.Null(Get(aggregate, "continue-on-error"));

            string[] checks = [.. script.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith('['))];
            string check = Assert.Single(checks);
            string[] conditions = [.. check.Split(" && ").Order(StringComparer.Ordinal)];
            string[] expectedConditions = [.. needs.Select(id => $"[ \"${{{{ needs.{id}.result }}}}\" = \"success\" ]").Order(StringComparer.Ordinal)];
            Assert.Equal(expectedConditions, conditions);
        }

        // Issue #18: the strict wall-clock budget is gated where a slow shared runner cannot block a merge.
        [Fact]
        public void ThePerformanceJobIsNonBlockingAndRunsTheStrictBudgets()
        {
            object? perf = Get(Jobs(), "perf");

            Assert.Equal("true", Text(Get(perf, "continue-on-error")));
            Assert.DoesNotContain("perf", Needs(Get(Jobs(), "build-test")));
            string script = string.Join('\n', Items(Get(perf, "steps")).Select(step => Text(Get(step, "run"))));
            Assert.Contains("Category=Performance", script, StringComparison.Ordinal);
            object? strictStep = Items(Get(perf, "steps")).Single(step => Text(Get(step, "name")).StartsWith("Performance tests", StringComparison.Ordinal));
            Assert.Equal("1", Text(Get(strictStep, "env", "NETPRINTS_PERF_STRICT")));
        }

        [Fact]
        public void NoBlockingJobRunsTheStrictBudgets()
        {
            foreach ((string id, object? job) in Jobs().Where(job => job.Key != "perf"))
            {
                Assert.True(string.IsNullOrEmpty(Text(Get(job, "env", "NETPRINTS_PERF_STRICT"))), $"Job {id} sets NETPRINTS_PERF_STRICT.");
                foreach (object? step in Items(Get(job, "steps")))
                {
                    Assert.True(string.IsNullOrEmpty(Text(Get(step, "env", "NETPRINTS_PERF_STRICT"))), $"A step of job {id} sets NETPRINTS_PERF_STRICT.");
                    Assert.DoesNotContain("NETPRINTS_PERF_STRICT", Text(Get(step, "run")), StringComparison.Ordinal);
                }
            }
        }

        // Hang guards with a fixed deadline, not performance budgets.
        private static readonly string[] HangGuardFiles = ["ProcessLauncherTests.cs", "CrashHandlerTests.cs", "E2EEditorExitDiagnosticsTests.cs"];

        [Fact]
        public void EveryWallClockBudgetTestIsTaggedRelaxedOutsideThePerfJobAndRunByIt()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string testsRoot = Path.Combine(root, "tests");
            string perfScript = string.Join('\n', Items(Get(Get(Jobs(), "perf"), "steps")).Select(step => Text(Get(step, "run"))));
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

            string[] budgetFiles = BudgetFiles(testsRoot);

            Assert.NotEmpty(budgetFiles);
            foreach (string path in budgetFiles)
            {
                string text = File.ReadAllText(path);
                string relative = Path.GetRelativePath(root, path);
                Assert.True(text.Contains("[Trait(\"Category\", \"Performance\")]", StringComparison.Ordinal), $"{relative} asserts a Stopwatch budget without the Performance trait.");
                Assert.True(text.Contains("NETPRINTS_PERF_STRICT", StringComparison.Ordinal), $"{relative} has no relaxed bound outside the strict perf job.");
                string project = Path.GetRelativePath(testsRoot, path).Split(separators)[0];
                Assert.True(perfScript.Contains(project, StringComparison.Ordinal), $"The perf job does not run {project}, which holds {relative}.");
            }
        }

        private static string[] BudgetFiles(string testsRoot)
        {
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
            return
            [
                .. Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !path.Split(separators).Any(part => part is "bin" or "obj"))
                    .Where(path => Path.GetFileName(path) != nameof(CiWorkflowTests) + ".cs")
                    .Where(path => !HangGuardFiles.Contains(Path.GetFileName(path)))
                    .Where(path => IsBudgetFile(File.ReadAllText(path))),
            ];
        }

        [Fact]
        public void TheBudgetGateSelectsEveryWallClockBudgetFile()
        {
            string testsRoot = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests");

            string[] names = [.. BudgetFiles(testsRoot).Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal)];

            Assert.Equal(
                ["CatalogPerformanceTests.cs", "OpenMethodPerformanceTests.cs", "ProjectOpenPerformanceTests.cs", "ScaleTests.cs", "SearchPerformanceTests.cs"],
                names);
        }

        private static bool IsBudgetFile(string text) =>
            text.Contains("Stopwatch", StringComparison.Ordinal) && BudgetAssertion().IsMatch(text);

        [GeneratedRegex(@"Assert\.True\([^;]*\s<\s[^;]*;")]
        private static partial Regex BudgetAssertion();

        [Fact]
        public void TheReaderRejectsAContinuationLineItDoesNotUnderstand()
        {
            string[] lines =
            [
                "jobs:",
                "  a:",
                "    steps:",
                "      - name: split",
                "          scalar",
                "  b:",
                "    name: lost",
            ];

            FormatException error = Assert.Throws<FormatException>(() => new YamlReader(lines).Parse());
            Assert.Contains("line 5", error.Message, StringComparison.Ordinal);
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
            Assert.True(File.Exists(workflow), $"{CliWindowsWorkflow} does not exist.");

            string[] closure = ProjectClosure(root, "tests/NetPrints.Cli.Tests/NetPrints.Cli.Tests.csproj");
            Assert.Contains("src/NetPrints.Cli", closure);

            string[] buildFiles =
            [
                "samples/**",
                "schemas/**",
                "eng/schemastore/**",
                "src/NetPrints.Sdk/**",
                ".gitattributes",
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
        /// literal and folded scalars, quoted scalars and comments. Anchors, tags, flow mappings and multi-line plain scalars are not supported:
        /// the reader throws a <see cref="FormatException"/> on any line it does not consume.
        /// </summary>
        private sealed partial class YamlReader(string[] lines)
        {
            private readonly string[] lines = lines;
            private int position;

            public object? Parse()
            {
                object? root = ParseBlock(0);
                if (Peek() is { } rest)
                {
                    throw new FormatException($"Unsupported YAML on line {position + 1}: {rest.Text}");
                }

                return root;
            }

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
