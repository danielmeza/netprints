using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;
using NetPrints.Testing.Extensions;
using NetPrints.Tests.Extensibility;
using NetPrints.Tests.Projects;
using NetPrints.Tests.Extensibility.MultiExtension;
using Xunit;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// MX-T02 and MX-T04 inside the real Generator process (SC-008): a temporary project loads the type provider, its consumer
    /// and the private-prefix fixture through <c>NetPrintsExtension</c> items, builds a graph using the consumer's and the
    /// prefix node's kinds, and the program prints what their translators emitted.
    /// </summary>
    [Collection(nameof(RealExtensionLoadCollection))]
    public class MultiExtensionBuildTests
    {
        [Fact]
        public async Task AProviderItsConsumerAndAPrivatePrefixExtensionBuildAndRunInTheGeneratorProcess()
        {
            string[] ids = [Extensibility.MultiExtension.FixtureExtensions.TypesProvider, Extensibility.MultiExtension.FixtureExtensions.TypesConsumer, Extensibility.MultiExtension.FixtureExtensions.PrefixedPrivate];
            string directory = Directory.CreateTempSubdirectory("netprints-mx-build-").FullName;
            try
            {
                await using (ExtensionHarness harness = await ExtensionHarness.CreateAsync([.. Array.ConvertAll(ids, Extensibility.MultiExtension.FixtureExtensions.Folder)], [], TestContext.Current.CancellationToken))
                {
                    ClassGraph cls = MultiExtensionGraphs.BuildClass(harness.Registry, "MxBuild", "Program", "fx.types-consumer/Use", "fx.private-prefix/Describe");
                    byte[] document = await MultiExtensionGraphs.WriteAsync(harness.Registry, cls);
                    await File.WriteAllBytesAsync(Path.Combine(directory, "MxBuild.Program.netpc.json"), document, TestContext.Current.CancellationToken);
                }

                string items = string.Concat(Array.ConvertAll(ids, id => $"    <NetPrintsExtension Include=\"{Extensibility.MultiExtension.FixtureExtensions.Folder(id)}\" />\n"));
                await File.WriteAllTextAsync(Path.Combine(directory, "MxBuild.csproj"), $"""
                    <Project Sdk="Microsoft.NET.Sdk">

                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>net10.0</TargetFramework>
                        <RootNamespace>MxBuild</RootNamespace>
                        <NetPrintsProfile>netprints.default</NetPrintsProfile>
                      </PropertyGroup>

                      <ItemGroup>
                    {items}    <PackageReference Include="NetPrints.Sdk" Version="1.0.0" Condition="'$(NetPrintsUseLocalSdk)' != 'true'" PrivateAssets="all" />
                      </ItemGroup>

                    </Project>

                    """, TestContext.Current.CancellationToken);
                LocalSdkLayout.Write(directory);

                (int buildExit, string buildOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "build", Path.Combine(directory, "MxBuild.csproj"), "-v:n", "-tl:off", "--nologo");
                Assert.True(buildExit == 0, buildOutput);
                string generated = await File.ReadAllTextAsync(Path.Combine(directory, "MxBuild.Program.netpc.g.cs"), TestContext.Current.CancellationToken);
                Assert.Contains("Description(\"fx.types-provider\")", generated, StringComparison.Ordinal);
                Assert.Contains("System.Console.WriteLine(\"netprints-fixture-runtime\");", generated, StringComparison.Ordinal);

                (int runExit, string runOutput) = await ExternalProcess.RunDotnetAsync(directory, environment: null,
                    "run", "--project", Path.Combine(directory, "MxBuild.csproj"), "--no-build");
                Assert.True(runExit == 0, runOutput);
                Assert.Contains("fx.types-provider", runOutput, StringComparison.Ordinal);
                Assert.Contains("netprints-fixture-runtime", runOutput, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
