using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Icons;

/// <summary>FR-084, ADR-0021 Amendment 1: icon ids, the registry that resolves them and the unknown-id fallback.</summary>
public partial class IconRegistryTests
{
    private static readonly string[] AllIds =
    [
        .. typeof(IconIds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (string)(field.GetRawConstantValue() ?? string.Empty)),
    ];

    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [GeneratedRegex(@"^[a-z0-9]+(\.[a-zA-Z0-9]+)+$")]
    private static partial Regex IdPattern();

    public static TheoryData<string> Ids() => [.. AllIds];

    [Fact]
    public void TheBuiltInIdsCoverTheNodeCategoriesKindGlyphsPinsAndEmptyStates()
    {
        Assert.True(AllIds.Count(id => id.StartsWith("netprints.icon.category.", StringComparison.Ordinal)) >= 16);
        Assert.True(AllIds.Count(id => id.StartsWith("netprints.icon.nodeKind.", StringComparison.Ordinal)) >= 13);
        Assert.True(AllIds.Count(id => id.StartsWith("netprints.icon.pin.", StringComparison.Ordinal)) >= 3);
        Assert.NotEmpty(AllIds.Where(id => id.StartsWith("netprints.icon.empty.", StringComparison.Ordinal)));
        Assert.NotEmpty(AllIds.Where(id => id.StartsWith("netprints.icon.dialog.", StringComparison.Ordinal)));
        Assert.Contains(IconIds.Overloads, AllIds);
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void EveryIdMatchesThePatternAndResolvesToAGlyph(string id)
    {
        var logger = new CollectingLogger<IconRegistry>();
        var registry = new IconRegistry(logger);

        Assert.Matches(IdPattern(), id);
        Assert.True(registry.IsKnown(id));
        Assert.Equal(id == IconIds.Unknown, registry.Resolve(id) == registry.Resolve(IconIds.Unknown));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AnUnknownIdResolvesToTheFallbackAndLogsOneWarning()
    {
        var logger = new CollectingLogger<IconRegistry>();
        var registry = new IconRegistry(logger);

        IconGlyph first = registry.Resolve("netprints.icon.doesNotExist");
        IconGlyph second = registry.Resolve("netprints.icon.doesNotExist");

        Assert.False(registry.IsKnown("netprints.icon.doesNotExist"));
        Assert.Equal(registry.Resolve(IconIds.Unknown), first);
        Assert.Equal(first, second);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("netprints.icon.doesNotExist", entry.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankIdResolvesToTheFallbackWithoutLogging(string? id)
    {
        var logger = new CollectingLogger<IconRegistry>();
        var registry = new IconRegistry(logger);

        Assert.Equal(registry.Resolve(IconIds.Unknown), registry.Resolve(id));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void AnIdWithAnOutlineAndFilledPairDrawsTheFilledGlyphWhenActive()
    {
        var registry = new IconRegistry(new CollectingLogger<IconRegistry>());

        Assert.NotEqual(registry.Resolve(IconIds.Project), registry.Resolve(IconIds.Project, active: true));
        Assert.Equal(registry.Resolve(IconIds.Save), registry.Resolve(IconIds.Save, active: true));
    }

    [Fact]
    public void ADescriptorWithAnUnknownIconIdStillRegistersWithoutAnIssue()
    {
        var logger = new CollectingLogger<ContributionRegistry>();
        var registry = new ContributionRegistry(logger);

        registry.AddCommand(new CommandDescriptor("ext.command.one", "One", new NoopHandler(), IconId: "ext.icon.nothing"));
        registry.AddPanel(new PanelDescriptor("ext.panel.one", "Panel", _ => new object(), PanelDock.Left, 0, IconId: "ext.icon.nothing"));
        registry.AddProjectTemplate(new ProjectTemplateDescriptor("ext.template.one", "Template", "Description", "profile", ProjectOutputType.Console, IconId: "ext.icon.nothing"));

        Assert.Empty(registry.Issues);
        Assert.Single(registry.Commands, command => command.Id == "ext.command.one");
        Assert.Single(registry.Panels);
        Assert.Single(registry.ProjectTemplates, template => template.Id == "ext.template.one");
    }
}
