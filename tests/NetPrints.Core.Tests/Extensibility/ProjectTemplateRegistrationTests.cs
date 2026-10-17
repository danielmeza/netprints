using System.Linq;
using System.Threading.Tasks;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-025, NPXE0012: the project templates an extension adds through the builder reach the registry, with the id rules.</summary>
public class ProjectTemplateRegistrationTests
{
    private static ProjectTemplateDescriptor Template(string id) =>
        new(id, "Template", "A template", "profile", ProjectOutputType.Console, Files: [new TemplateFile("a.txt", "A")]);

    [Fact]
    public async Task ATemplateWithTheManifestPrefixIsInTheRegistryWithItsFiles()
    {
        var extension = InProcess("fx", builder => builder.AddProjectTemplate(Template("fx.template.one")));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        ProjectTemplateDescriptor template = Assert.Single(registry.ProjectTemplates);
        Assert.Equal("a.txt", Assert.Single(template.Files ?? []).Path);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task ATemplateIdWithoutTheManifestPrefixIsNpx013AndADuplicateIsNpx004()
    {
        var extension = InProcess("fx", builder => builder
            .AddProjectTemplate(Template("other.template"))
            .AddProjectTemplate(Template("fx.template.one"))
            .AddProjectTemplate(Template("fx.template.one")));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Equal("fx.template.one", Assert.Single(registry.ProjectTemplates).Id);
        Assert.Equal([ExtensionDiagnosticCodes.IdPrefix, ExtensionDiagnosticCodes.DuplicateId], registry.Issues.Select(issue => issue.Code));
    }
}
