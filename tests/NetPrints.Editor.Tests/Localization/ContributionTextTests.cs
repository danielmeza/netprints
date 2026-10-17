using System.Globalization;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Editor.Tests.Localization;

/// <summary>FR-027, contracts/extension-api.md section 4: the text of dialogs, tiles, templates and node suggestions is a <see cref="LocalizedText"/>, and the built-in ones have resx entries.</summary>
public class ContributionTextTests
{
    private static readonly CultureInfo Neutral = CultureInfo.InvariantCulture;

    public static TheoryData<Type, string> TextProperties => new()
    {
        { typeof(DialogDescriptor), nameof(DialogDescriptor.Title) },
        { typeof(DashboardTileDescriptor), nameof(DashboardTileDescriptor.Title) },
        { typeof(ProjectTemplateDescriptor), nameof(ProjectTemplateDescriptor.DisplayName) },
        { typeof(ProjectTemplateDescriptor), nameof(ProjectTemplateDescriptor.Description) },
        { typeof(NodeSuggestion), nameof(NodeSuggestion.DisplayName) },
    };

    public static TheoryData<string> BuiltInKeys => new()
    {
        "Tile.netprints.tile.recent.Title",
        "Tile.netprints.tile.open.Title",
        "Tile.netprints.tile.new.Title",
        "Tile.netprints.tile.samples.Title",
        "Tile.netprints.tile.learn.Title",
        "Tile.netprints.tile.whatsNew.Title",
        "Template.netprints.template.console.Name",
        "Template.netprints.template.console.Description",
        "Template.netprints.template.library.Name",
        "Template.netprints.template.library.Description",
    };

    [Theory]
    [MemberData(nameof(TextProperties))]
    public void ALabelIsALocalizedText(Type descriptor, string property)
    {
        Assert.Equal(typeof(LocalizedText), descriptor.GetProperty(property)?.PropertyType);
    }

    [Fact]
    public void ALiteralDialogTileOrTemplateTextTakesTheConventionKeyAndShowsItself()
    {
        var dialog = new DialogDescriptor("fx.dialog.x", "Hello", _ => new object());
        var tile = new DashboardTileDescriptor("fx.tile.x", "Tile", 0, _ => new object());
        var template = new ProjectTemplateDescriptor("fx.template.x", "Name", "About", "profile", ProjectOutputType.Library);

        Assert.Equal(new LocalizedText("Dialog.fx.dialog.x.Title", "Hello"), dialog.Title);
        Assert.Equal(new LocalizedText("Tile.fx.tile.x.Title", "Tile"), tile.Title);
        Assert.Equal(new LocalizedText("Template.fx.template.x.Name", "Name"), template.DisplayName);
        Assert.Equal(new LocalizedText("Template.fx.template.x.Description", "About"), template.Description);
        Assert.Equal("Hello", dialog.DisplayTitle);
    }

    [Fact]
    public void AnExplicitKeyOnADialogTitleIsKept()
    {
        var dialog = new DialogDescriptor("fx.dialog.x", new LocalizedText("Custom.Dialog", "Hello"), _ => new object());

        Assert.Equal("Custom.Dialog", dialog.Title.Key);
    }

    [Theory]
    [MemberData(nameof(BuiltInKeys))]
    public void ABuiltInTileOrTemplateTextIsInTheNeutralResx(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, Neutral)), key);
    }
}
