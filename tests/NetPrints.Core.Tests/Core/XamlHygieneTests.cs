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

        /// <summary>E2: hex or named color literals on brush/color properties, outside theme dictionaries.
        /// R2-07: also checks a <c>Setter</c>'s <c>Value</c> when its <c>Property</c> ends with a color
        /// suffix (the attribute is named <c>Value</c>, not e.g. <c>Stroke</c>, so the plain
        /// attribute-name check below never saw it), and no longer exempts an entire
        /// <c>Styles</c>/<c>ResourceDictionary</c>-rooted file: only its <c>ThemeDictionaries</c> are a
        /// legitimate home for a raw color, and that is already excluded per-element below.</summary>
        private static readonly Dictionary<string, string> E2Allowlist = new(StringComparer.Ordinal)
        {
            ["src/NetPrints.Editor/EditorApp.axaml:64"] = "FluentTheme's own Dark palette definition (ColorPaletteResources): the literal is the base system color, one level below any token.",
        };

        /// <summary>True when <paramref name="value"/> is a color literal E2 forbids: not a binding/resource
        /// markup extension and not the always-allowed <c>Transparent</c>.</summary>
        private static bool IsColorLiteral(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.StartsWith('{') || string.Equals(trimmed, "Transparent", StringComparison.Ordinal))
            {
                return false;
            }

            return HexColorPattern.IsMatch(trimmed) || NamedColorPattern.IsMatch(trimmed);
        }

        [Fact]
        public void E2_NoColorLiteralsInViews()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                foreach (XElement element in file.Document.Descendants())
                {
                    if (IsUnderThemeDictionaries(element))
                    {
                        continue;
                    }

                    if (element.Name.LocalName == "Setter")
                    {
                        XAttribute? property = element.Attribute("Property");
                        XAttribute? value = element.Attribute("Value");
                        if (property is null || value is null
                            || !ColorPropertySuffixes.Any(suffix => property.Value.EndsWith(suffix, StringComparison.Ordinal))
                            || !IsColorLiteral(value.Value))
                        {
                            continue;
                        }

                        string setterKey = $"{file.RelativePath}:{LineOf(element)}";
                        seen.Add(setterKey);
                        if (!E2Allowlist.ContainsKey(setterKey))
                        {
                            offenders.Add($"{setterKey}: Setter Property=\"{property.Value}\" Value=\"{value.Value}\"");
                        }

                        continue;
                    }

                    foreach (XAttribute attribute in element.Attributes())
                    {
                        if (!ColorPropertySuffixes.Any(suffix => attribute.Name.LocalName.EndsWith(suffix, StringComparison.Ordinal))
                            || !IsColorLiteral(attribute.Value))
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
        /// <c>DragDrop.*</c> events are gesture mechanics (owner rule), so they are not scanned at all.
        /// Empty since batch X2b: every seeded violation had a prebuilt-behavior or dialog-VM fit, so
        /// none needed to stay (ADR-0007).</summary>
        private static readonly Dictionary<string, string> E3Allowlist = new(StringComparer.Ordinal);

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
