using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using NetPrints.Core;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK015: the editor contributions have prefixed ids, resolvable labels, gestures that parse and follow the scope rules, and icon ids of the extension.</summary>
internal sealed partial class EditorContributionsCheck : ConformanceCheck
{
    private const string IdRule = "contribution ids start with the extension id";
    private const string LabelRule = "a label resolves through its key or has default text";
    private const string GestureRule = "a gesture parses and follows the scope rules";
    private const string IconRule = "an icon id is the extension id, a dot and a name that resolves";
    private const string IconKeyPrefix = "Icon.";

    public override string Code => NpkDiagnosticCodes.EditorContributions;

    public override string Name => "Editor contributions";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.SubjectId is not { } id)
        {
            return Task.FromResult(CheckOutcome.Skip("no extension besides the built-in one was loaded"));
        }

        RecordedContribution[] own = [.. context.Harness.EditorContributions.Where(item => item.ExtensionId == id)];
        if (own.Length == 0)
        {
            return Task.FromResult(CheckOutcome.Passed("no editor contribution"));
        }

        HashSet<string>? iconKeys = IconKeysIn(context.Options.SourceDirectory);
        string[] commandIds = [.. own.Select(item => item.Descriptor).OfType<CommandDescriptor>().Select(command => command.Id)];
        var findings = new List<ConformanceFinding>();
        foreach (object descriptor in own.Select(item => item.Descriptor))
        {
            switch (descriptor)
            {
                case CommandDescriptor command:
                    string commandItem = $"command {command.Id}";
                    CheckId(id, command.Id, commandItem, findings);
                    CheckLabel(context, command.Label, commandItem, findings);
                    CheckIcon(id, command.IconId, commandItem, iconKeys, findings);
                    CheckGestures(command, commandItem, findings);
                    break;
                case PanelDescriptor panel:
                    CheckId(id, panel.Id, $"panel {panel.Id}", findings);
                    CheckLabel(context, panel.Title, $"panel {panel.Id}", findings);
                    CheckIcon(id, panel.IconId, $"panel {panel.Id}", iconKeys, findings);
                    break;
                case DashboardTileDescriptor tile:
                    CheckId(id, tile.Id, $"dashboard tile {tile.Id}", findings);
                    CheckLabel(context, tile.Title, $"dashboard tile {tile.Id}", findings);
                    break;
                case DialogDescriptor dialog:
                    CheckId(id, dialog.Id, $"dialog {dialog.Id}", findings);
                    CheckLabel(context, dialog.Title, $"dialog {dialog.Id}", findings);
                    break;
                case SettingsPageDescriptor page:
                    CheckId(id, page.Id, $"settings page {page.Id}", findings);
                    CheckLabel(context, page.Title, $"settings page {page.Id}", findings);
                    break;
                case StatusBarItemDescriptor status:
                    CheckId(id, status.Id, $"status bar item {status.Id}", findings);
                    break;
                case ContextMenuItemDescriptor menu:
                    CheckId(id, menu.Id, $"context menu item {menu.Id}", findings);
                    if (HasPrefix(menu.CommandId, id) && !commandIds.Contains(menu.CommandId, StringComparer.Ordinal))
                    {
                        findings.Add(Finding(IdRule, $"context menu item {menu.Id} names the command {menu.CommandId}, which the extension does not contribute"));
                    }

                    break;
            }
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }

    private static bool HasPrefix(string value, string extensionId) =>
        !string.IsNullOrEmpty(value) && value.Length > extensionId.Length + 1 && value.StartsWith(extensionId, StringComparison.Ordinal) && value[extensionId.Length] is '.' or '/';

    private static HashSet<string>? IconKeysIn(string? directory)
    {
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        string[] files = [.. ViewSources.Find(directory)];
        if (files.Length == 0)
        {
            return null;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            foreach (Match match in KeyPattern().Matches(File.ReadAllText(file)))
            {
                keys.Add(match.Groups["key"].Value);
            }
        }

        return keys;
    }

    private void CheckId(string extensionId, string id, string item, List<ConformanceFinding> findings)
    {
        if (!HasPrefix(id, extensionId))
        {
            findings.Add(Finding(IdRule, $"{item}: the id must start with {extensionId}."));
        }
    }

    private void CheckLabel(CheckContext context, LocalizedText text, string item, List<ConformanceFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(text.Key))
        {
            findings.Add(Finding(LabelRule, $"{item}: the label has no key"));
        }
        else if (string.IsNullOrWhiteSpace(text.DefaultText) && !context.Harness.Registry.StringResources.Any(resources => Resolves(resources, text.Key)))
        {
            findings.Add(Finding(LabelRule, $"{item}: the key {text.Key} has no default text and no string resource holds it"));
        }
    }

    private static bool Resolves(ResourceManager resources, string key)
    {
        try
        {
            return resources.GetString(key, CultureInfo.InvariantCulture) is not null;
        }
        catch (MissingManifestResourceException)
        {
            return false;
        }
    }

    private void CheckIcon(string extensionId, string? iconId, string item, HashSet<string>? iconKeys, List<ConformanceFinding> findings)
    {
        if (iconId is null)
        {
            return;
        }

        if (!HasPrefix(iconId, extensionId))
        {
            findings.Add(Finding(IconRule, $"{item}: the icon id {iconId} must start with {extensionId}."));
        }
        else if (iconKeys is not null && !iconKeys.Contains(IconKeyPrefix + iconId))
        {
            findings.Add(Finding(IconRule, $"{item}: no resource {IconKeyPrefix}{iconId} in the view sources"));
        }
    }

    private void CheckGestures(CommandDescriptor command, string item, List<ConformanceFinding> findings)
    {
        foreach (CommandGesture gesture in command.DefaultGestures ?? [])
        {
            string text = gesture.ToString();
            if (!CommandGesture.TryParse(text, out CommandGesture? parsed) || parsed != gesture)
            {
                findings.Add(Finding(GestureRule, $"{item}: the gesture {text} does not parse back to itself"));
            }

            if (command.Scope == CommandScope.Global && gesture.IsPlain && !gesture.IsFunctionKey)
            {
                findings.Add(Finding(GestureRule, $"{item}: the gesture {text} has no modifier and the command is global; use a function key, a modifier or a narrower scope"));
            }
        }
    }

    [GeneratedRegex("""x:Key\s*=\s*"(?<key>[^"]+)""")]
    private static partial Regex KeyPattern();
}
