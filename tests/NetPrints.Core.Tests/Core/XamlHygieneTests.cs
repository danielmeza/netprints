using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>
    /// ADR-0007 / the `avalonia-xaml` skill (<c>.claude/skills/avalonia-xaml/SKILL.md</c>): enforces the
    /// Enforced-tier checks E1-E6 against every <c>src/**/*.axaml</c> file. Each rule has an explicit
    /// allowlist keyed by <c>file:line</c> with a reason, seeded with the violations found when the
    /// rule was introduced (batch X1). Batch X2 fixes them and empties the allowlists. A file is
    /// re-parsed per rule (rather than shared state) to keep each <c>[Fact]</c> independently readable,
    /// the same trade-off <see cref="SourceHygieneTests"/> makes.
    /// </summary>
    public partial class XamlHygieneTests
    {
        private static readonly Regex NamedColorPattern = NamedColorRegex();
        private static readonly Regex HexColorPattern = HexColorRegex();
        private static readonly Regex StaticResourceKeyPattern = StaticResourceKeyRegex();

        private static readonly string[] ColorPropertySuffixes =
        [
            "Background", "Foreground", "Fill", "Stroke", "BorderBrush", "Color", "BoxShadow", "CaretBrush",
            "SelectionBrush", "OpacityMask",
        ];

        private static readonly HashSet<string> ForbiddenEventAttributes = new(StringComparer.Ordinal)
        {
            "Click", "Tapped", "DoubleTapped", "RightTapped", "Holding", "KeyDown", "KeyUp", "TextInput",
            "SelectionChanged", "TextChanged", "GotFocus", "LostFocus", "Loaded", "Unloaded", "Opened", "Closed",
            "Closing", "IsCheckedChanged", "ValueChanged", "ContextRequested",
        };

        private static readonly HashSet<string> ResourceRoots = new(StringComparer.Ordinal) { "Application", "Styles", "ResourceDictionary" };
        private static readonly HashSet<string> IconTags = new(StringComparer.Ordinal) { "MaterialIcon", "PathIcon", "Image", "Path", "Viewbox", "Svg" };
        private static readonly HashSet<string> ButtonTags = new(StringComparer.Ordinal)
        {
            "Button", "ToggleButton", "RepeatButton", "SplitButton", "DropDownButton", "HyperlinkButton",
        };

        /// <summary>One parsed <c>.axaml</c> file, keyed by its path relative to the repository root (forward slashes).</summary>
        private sealed record AxamlFile(string RelativePath, XDocument Document);

        private static AxamlFile[] LoadAxamlFiles()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string src = Path.Combine(root, "src");
            string[] paths = [.. Directory.EnumerateFiles(src, "*.axaml", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)];

            Assert.True(paths.Length > 0, "Expected to scan at least one src/**/*.axaml file.");

            return [.. paths.Select(path => new AxamlFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                XDocument.Load(path, LoadOptions.SetLineInfo)))];
        }

        private static int LineOf(XObject node) => ((IXmlLineInfo)node).LineNumber;

        /// <summary>True when <paramref name="element"/> or an ancestor is a <c>*.ThemeDictionaries</c> element (E2, E6).</summary>
        private static bool IsUnderThemeDictionaries(XElement element) =>
            element.AncestorsAndSelf().Any(e => e.Name.LocalName.EndsWith("ThemeDictionaries", StringComparison.Ordinal));

        /// <summary>Asserts that every allowlist key was seen by the scan: an entry that no longer violates
        /// means the allowlist is stale and must shrink, not just accumulate.</summary>
        private static void AssertAllowlistHasNoStaleEntries(IEnumerable<string> allowlistKeys, HashSet<string> seenKeys)
        {
            string[] stale = [.. allowlistKeys.Where(key => !seenKeys.Contains(key)).OrderBy(k => k, StringComparer.Ordinal)];
            Assert.True(stale.Length == 0, $"Allowlisted but no longer violates (remove from the allowlist): {string.Join(", ", stale)}");
        }

        /// <summary>E1: <c>x:CompileBindings="False"</c> and <c>{ReflectionBinding</c> are allowlist-only.</summary>
        private static readonly Dictionary<string, string> E1Allowlist = new(StringComparer.Ordinal);

        [Fact]
        public void E1_NoCompiledBindingOptOuts()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                foreach (XAttribute attribute in file.Document.Descendants().SelectMany(e => e.Attributes()))
                {
                    bool isCompileBindingsFalse = attribute.Name.LocalName == "CompileBindings"
                        && string.Equals(attribute.Value.Trim(), "False", StringComparison.OrdinalIgnoreCase);
                    bool isReflectionBinding = attribute.Value.Contains("{ReflectionBinding", StringComparison.Ordinal);
                    if (!isCompileBindingsFalse && !isReflectionBinding)
                    {
                        continue;
                    }

                    string key = $"{file.RelativePath}:{LineOf(attribute)}";
                    seen.Add(key);
                    if (!E1Allowlist.ContainsKey(key))
                    {
                        offenders.Add($"{key}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
                    }
                }
            }

            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E1Allowlist.Keys, seen);
        }

        /// <summary>E2: hex or named color literals on brush/color properties, outside theme dictionaries.</summary>
        private static readonly Dictionary<string, string> E2Allowlist = new(StringComparer.Ordinal);

        [Fact]
        public void E2_NoColorLiteralsInViews()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                XElement? root = file.Document.Root;
                if (root is null || ResourceRoots.Contains(root.Name.LocalName))
                {
                    continue; // A resource file (Application/Styles/ResourceDictionary), not a view.
                }

                foreach (XElement element in file.Document.Descendants())
                {
                    if (IsUnderThemeDictionaries(element))
                    {
                        continue;
                    }

                    foreach (XAttribute attribute in element.Attributes())
                    {
                        string value = attribute.Value.Trim();
                        if (!ColorPropertySuffixes.Any(suffix => attribute.Name.LocalName.EndsWith(suffix, StringComparison.Ordinal))
                            || value.StartsWith('{')
                            || string.Equals(value, "Transparent", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!HexColorPattern.IsMatch(value) && !NamedColorPattern.IsMatch(value))
                        {
                            continue;
                        }

                        string key = $"{file.RelativePath}:{LineOf(attribute)}";
                        seen.Add(key);
                        if (!E2Allowlist.ContainsKey(key))
                        {
                            offenders.Add($"{key}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
                        }
                    }
                }
            }

            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E2Allowlist.Keys, seen);
        }

        /// <summary>E3: command-shaped events wired to a code-behind handler in XAML. Pointer and
        /// <c>DragDrop.*</c> events are gesture mechanics (owner rule), so they are not scanned at all.</summary>
        private static readonly Dictionary<string, string> E3Allowlist = new(StringComparer.Ordinal)
        {
            ["src/NetPrints.Editor/ClassEditor/ClassEditorWindow.axaml:173"] =
                "Event graph double-click opens the graph; fits ExecuteCommandOnDoubleTappedBehavior (batch X2).",
            ["src/NetPrints.Editor/ClassEditor/ClassEditorWindow.axaml:200"] =
                "Diagnostic row double-click navigates to the node; fits ExecuteCommandOnDoubleTappedBehavior (batch X2).",
            ["src/NetPrints.Editor/Dialogs/ErrorDialog.axaml:13"] = "OK closes the dialog with no result; fits ButtonClickEventTriggerBehavior + CloseWindowAction (batch X2).",
            ["src/NetPrints.Editor/Dialogs/IssuesDialog.axaml:20"] = "OK closes the dialog with no result; fits ButtonClickEventTriggerBehavior + CloseWindowAction (batch X2).",
            ["src/NetPrints.Editor/Dialogs/SelectMethodDialog.axaml:21"] =
                "Select closes the dialog with a result; needs a dialog VM plus a result-carrying close action, which does not exist yet (research.md §5; batch X2 or later).",
            ["src/NetPrints.Editor/Dialogs/SelectTypeDialog.axaml:14"] =
                "Select closes the dialog with a result; same gap as SelectMethodDialog, plus ResolveSelection (D16) should move to that VM.",
            ["src/NetPrints.Editor/Dialogs/TrustDialog.axaml:21"] = "Don't load closes the dialog with a result; same result-carrying-close gap as SelectMethodDialog.",
            ["src/NetPrints.Editor/Dialogs/TrustDialog.axaml:22"] = "Trust closes the dialog with a result; same result-carrying-close gap as SelectMethodDialog.",
            ["src/NetPrints.Editor/References/ReferencesDialog.axaml:44"] = "Close closes the dialog with no result; fits ButtonClickEventTriggerBehavior + CloseWindowAction (batch X2).",
            ["src/NetPrints.Editor/Search/NodeSearchView.axaml:11"] = "Enter picks the first non-header result; fits ExecuteCommandOnKeyDownBehavior plus a VM SelectFirstCommand (batch X2).",
            ["src/NetPrints.Editor/Search/NodeSearchView.axaml:14"] = "Enter on the result list selects the item; fits ExecuteCommandOnKeyDownBehavior (batch X2).",
            ["src/NetPrints.Editor/Search/NodeSearchView.axaml:23"] = "Tap selects a result, skipping headers; fits ExecuteCommandOnTappedBehavior plus a VM guard (batch X2).",
            ["src/NetPrints.Editor/Variables/MemberVariableView.axaml:16"] = "Tap selects the variable; fits ExecuteCommandOnTappedBehavior (batch X2).",
            ["src/NetPrints.Editor/Variables/MemberVariableView.axaml:26"] = "Double-tap opens the getter; fits ExecuteCommandOnDoubleTappedBehavior (batch X2).",
            ["src/NetPrints.Editor/Variables/MemberVariableView.axaml:37"] = "Double-tap opens the setter; fits ExecuteCommandOnDoubleTappedBehavior (batch X2).",
        };

        [Fact]
        public void E3_NoCommandShapedEventHandlersInXaml()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var isHandlerName = IsBareIdentifierRegex();

            foreach (AxamlFile file in files)
            {
                foreach (XAttribute attribute in file.Document.Descendants().SelectMany(e => e.Attributes()))
                {
                    if (!ForbiddenEventAttributes.Contains(attribute.Name.LocalName) || !isHandlerName.IsMatch(attribute.Value))
                    {
                        continue;
                    }

                    string key = $"{file.RelativePath}:{LineOf(attribute)}";
                    seen.Add(key);
                    if (!E3Allowlist.ContainsKey(key))
                    {
                        offenders.Add($"{key}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
                    }
                }
            }

            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E3Allowlist.Keys, seen);
        }

        /// <summary>E4: every <c>AutomationProperties.AutomationId</c> comes from the <c>AutomationIds</c>
        /// constants (<c>{x:Static ...}</c>), never a string literal. A regression guard: no allowlist.</summary>
        [Fact]
        public void E4_AutomationIdsComeFromConstants()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            int checkedCount = 0;

            foreach (AxamlFile file in files)
            {
                foreach (XAttribute attribute in file.Document.Descendants().SelectMany(e => e.Attributes())
                    .Where(a => a.Name.LocalName == "AutomationProperties.AutomationId"))
                {
                    checkedCount++;
                    if (!attribute.Value.StartsWith("{x:Static", StringComparison.Ordinal))
                    {
                        offenders.Add($"{file.RelativePath}:{LineOf(attribute)}: AutomationId=\"{attribute.Value}\"");
                    }
                }
            }

            Assert.True(checkedCount > 0, "Expected to check at least one AutomationProperties.AutomationId.");
            Assert.Empty(offenders);
        }

        /// <summary>E5: an icon-only button (its only content is an icon element) needs an accessible name.</summary>
        private static readonly Dictionary<string, string> E5Allowlist = new(StringComparer.Ordinal);

        [Fact]
        public void E5_IconOnlyButtonsHaveAnAccessibleName()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                foreach (XElement element in file.Document.Descendants().Where(e => ButtonTags.Contains(e.Name.LocalName)))
                {
                    if (element.Attributes().Any(a => a.Name.LocalName == "Content"))
                    {
                        continue;
                    }

                    // Non-property-element children only (skip "Button.Styles" etc., which carry a dot).
                    XElement[] children = [.. element.Elements().Where(c => !c.Name.LocalName.Contains('.', StringComparison.Ordinal))];
                    if (children.Length == 0 || !children.All(c => IconTags.Contains(c.Name.LocalName)))
                    {
                        continue;
                    }

                    bool hasAccessibleName = element.Attributes().Any(a =>
                        a.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy");
                    if (hasAccessibleName)
                    {
                        continue;
                    }

                    string key = $"{file.RelativePath}:{LineOf(element)}";
                    seen.Add(key);
                    if (!E5Allowlist.ContainsKey(key))
                    {
                        offenders.Add($"{key}: <{element.Name.LocalName}> with only {string.Join('/', children.Select(c => c.Name.LocalName))}");
                    }
                }
            }

            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E5Allowlist.Keys, seen);
        }

        /// <summary>E6: a key declared under <c>ThemeDictionaries</c> must be looked up with
        /// <c>DynamicResource</c>, never <c>StaticResource</c> (which cannot see theme dictionaries). A
        /// regression guard: no allowlist.</summary>
        [Fact]
        public void E6_ThemeTokensAreNeverLookedUpWithStaticResource()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var themeKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                foreach (XElement element in file.Document.Descendants())
                {
                    XAttribute? key = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key");
                    bool isThemeDictionaryOrWrapper = element.Name.LocalName.EndsWith("ThemeDictionaries", StringComparison.Ordinal)
                        || element.Name.LocalName == "ResourceDictionary";
                    if (key is not null && !isThemeDictionaryOrWrapper && IsUnderThemeDictionaries(element))
                    {
                        themeKeys.Add(key.Value);
                    }
                }
            }

            var offenders = new List<string>();
            foreach (AxamlFile file in files)
            {
                foreach (XAttribute attribute in file.Document.Descendants().SelectMany(e => e.Attributes()))
                {
                    Match match = StaticResourceKeyPattern.Match(attribute.Value);
                    if (match.Success && themeKeys.Contains(match.Groups["key"].Value))
                    {
                        offenders.Add($"{file.RelativePath}:{LineOf(attribute)}: StaticResource {match.Groups["key"].Value} is a ThemeDictionaries key");
                    }
                }
            }

            Assert.True(themeKeys.Count > 0, "Expected at least one ThemeDictionaries key (EditorStyles.axaml).");
            Assert.Empty(offenders);
        }

        [GeneratedRegex(@"^(AliceBlue|Aqua|Beige|Black|Blue|Brown|Crimson|Cyan|DarkGray|DarkGreen|DarkOrange|DarkRed|DimGray|DodgerBlue|Gold|Gray|Green|Grey|HotPink|Indigo|LightBlue|LightGray|LightGreen|Lime|Magenta|Maroon|Navy|Olive|Orange|OrangeRed|Pink|Purple|Red|Salmon|Silver|SkyBlue|Teal|Tomato|Violet|White|WhiteSmoke|Yellow|YellowGreen)$")]
        private static partial Regex NamedColorRegex();

        [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
        private static partial Regex HexColorRegex();

        [GeneratedRegex(@"^[A-Za-z_]\w*$")]
        private static partial Regex IsBareIdentifierRegex();

        [GeneratedRegex(@"\{StaticResource\s+(?<key>[\w.]+)")]
        private static partial Regex StaticResourceKeyRegex();
    }
}
