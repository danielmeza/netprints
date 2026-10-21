using System.Globalization;
using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Localization;

/// <summary>FR-027, research R13: every key a built-in declaration implies exists in the neutral resx, and an id-keyed text resolves.</summary>
public class ConventionKeyTests
{
    private static readonly CultureInfo Neutral = CultureInfo.InvariantCulture;

    private static ContributionRegistry Registered()
    {
        var registry = new ContributionRegistry(new CollectingLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        return registry;
    }

    [Fact]
    public void EveryCommandLabelKeyIsInTheNeutralResx()
    {
        string[] missing = [.. Registered().Commands.Where(command => Strings.ResourceManager.GetString(command.Label.Key, Neutral) is null).Select(command => $"{command.Label.Key}={command.Label.Text}")];

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryMenuKeyIsInTheNeutralResx()
    {
        string[] missing = [.. Registered().Commands.Select(command => command.Menu?.Path).OfType<string>().Concat(MenuBarViewModel.StandardMenus).Distinct(StringComparer.Ordinal).Select(menu => new MenuPlacement(menu, "", 0).Label).Where(label => Strings.ResourceManager.GetString(label.Key, Neutral) is null).Select(label => $"{label.Key}={label.Text}")];

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryPanelTitleKeyIsInTheNeutralResx()
    {
        string[] missing = [.. Registered().Panels.Where(panel => Strings.ResourceManager.GetString(panel.Title.Key, Neutral) is null).Select(panel => $"{panel.Title.Key}={panel.Title.Text}")];

        Assert.NotEmpty(Registered().Panels);
        Assert.Empty(missing);
    }

    [Fact]
    public void TheBuiltInNodeCategoryIsTheSingleNetPrintsOne()
    {
        Assert.Equal("NetPrints", Strings.ResourceManager.GetString("Category.netprints", Neutral));
    }

    [Fact]
    public void ALiteralLabelTakesTheConventionKeyAndShowsItsText()
    {
        var command = new CommandDescriptor("netprints.command.demo", "Demo", new NewProjectCommandHandler());

        Assert.Equal("Command.netprints.command.demo.Label", command.Label.Key);
        Assert.Equal("Demo", command.DisplayLabel);
    }

    [Fact]
    public void AnExplicitKeyIsKept()
    {
        var command = new CommandDescriptor("netprints.command.demo", new LocalizedText("Custom.Key", "Demo"), new NewProjectCommandHandler());

        Assert.Equal("Custom.Key", command.Label.Key);
    }
}
