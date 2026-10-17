using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>
    /// FR-019, research R12, ADR-0030: <c>NetPrints.Extensibility.Abstractions</c> holds the contracts an extension
    /// compiles against and nothing of the host. Extensions and the test fixtures reference it alone.
    /// </summary>
    public class AbstractionsReferenceTests
    {
        private const string AbstractionsName = "NetPrints.Extensibility.Abstractions";
        private const string HostName = "NetPrints.Extensibility";

        private static readonly string[] AllowedProjects =
        [
            "NetPrints.Core",
            "NetPrints.Serialization",
            "NetPrints.Reflection",
            "NetPrints.Catalog",
        ];

        private static readonly string[] AllowedPackages =
        [
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
        ];

        private static string Root => SampleProjectFactory.FindRepositoryRoot();

        [Fact]
        public void TheAbstractionsProjectReferencesOnlyTheAllowedProjectsAndPackages()
        {
            string project = Path.Combine(Root, "src", AbstractionsName, AbstractionsName + ".csproj");
            Assert.True(File.Exists(project), $"{project} does not exist");

            XElement csproj = XElement.Load(project);
            string[] projects = [.. References(csproj, "ProjectReference").Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))];
            string[] packages = [.. References(csproj, "PackageReference")];

            Assert.Empty(projects.Except(AllowedProjects, StringComparer.Ordinal));
            Assert.Empty(packages.Except(AllowedPackages, StringComparer.Ordinal));
            Assert.DoesNotContain(HostName, projects);
        }

        [Fact]
        public void TheAbstractionsAssemblyReferencesNoHostOnlyAssembly()
        {
            Assembly abstractions = typeof(INetPrintsExtension).Assembly;
            Assert.Equal(AbstractionsName, abstractions.GetName().Name);

            string[] referenced = [.. abstractions.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty)];

            Assert.DoesNotContain("System.Runtime.Loader", referenced);
            Assert.DoesNotContain("Microsoft.Extensions.Options", referenced);
            Assert.DoesNotContain(HostName, referenced);
        }

        [Fact]
        public void TheHostAssemblyReferencesTheAbstractions()
        {
            Assembly host = typeof(ExtensionHost).Assembly;

            Assert.Equal(HostName, host.GetName().Name);
            Assert.Contains(AbstractionsName, host.GetReferencedAssemblies().Select(name => name.Name));
        }

        [Fact]
        public void TheTestExtensionAndTheFixturesCompileAgainstTheAbstractionsOnly()
        {
            string[] projects =
            [
                Path.Combine(Root, "tests", "NetPrints.TestExtension", "NetPrints.TestExtension.csproj"),
                .. Directory.EnumerateFiles(Path.Combine(Root, "tests", "Fixtures", "Extensions"), "Fx.*.csproj", SearchOption.AllDirectories),
            ];
            Assert.NotEmpty(projects);

            List<string> offenders = [];
            foreach (string project in projects)
            {
                string[] referenced = [.. References(XElement.Load(project), "ProjectReference")
                    .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))];

                if (referenced.Contains(HostName, StringComparer.Ordinal))
                {
                    offenders.Add(Path.GetRelativePath(Root, project));
                }

                Assert.Contains(AbstractionsName, referenced);
            }

            Assert.Empty(offenders);
        }

        private static IEnumerable<string> References(XElement csproj, string element)
            => csproj.Descendants(element).Select(reference => (string?)reference.Attribute("Include")).OfType<string>();
    }
}
