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

        private static readonly HashSet<string> IconTags = new(StringComparer.Ordinal) { "MaterialIcon", "PathIcon", "Image", "Path", "Viewbox", "Svg", "IconPresenter" };
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

            return trimmed.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Any(token => HexColorPattern.IsMatch(token) || NamedColorPattern.IsMatch(token));
        }

        private static readonly HashSet<string> ColorTextElements = new(StringComparer.Ordinal) { "BoxShadows", "Color", "SolidColorBrush" };

        /// <summary>Scans for E2 offenders. A key names the element, the attribute and the literal, never a line,
        /// so editing above an allowlisted literal neither breaks nor widens the entry.</summary>
        private static (List<string> Offenders, HashSet<string> Seen) ScanColorLiterals(AxamlFile[] files, IReadOnlyDictionary<string, string> allowlist)
        {
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

                    if (ColorTextElements.Contains(element.Name.LocalName) && !element.HasElements && IsColorLiteral(element.Value))
                    {
                        string textKey = $"{file.RelativePath}:{element.Name.LocalName}.#text={element.Value.Trim()}";
                        seen.Add(textKey);
                        if (!allowlist.ContainsKey(textKey))
                        {
                            offenders.Add($"{file.RelativePath}:{LineOf(element)}: {textKey}");
                        }
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

                        string setterKey = $"{file.RelativePath}:Setter.{property.Value}={value.Value.Trim()}";
                        seen.Add(setterKey);
                        if (!allowlist.ContainsKey(setterKey))
                        {
                            offenders.Add($"{file.RelativePath}:{LineOf(element)}: {setterKey}: Setter Property=\"{property.Value}\" Value=\"{value.Value}\"");
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

                        string key = $"{file.RelativePath}:{element.Name.LocalName}.{attribute.Name.LocalName}={attribute.Value.Trim()}";
                        seen.Add(key);
                        if (!allowlist.ContainsKey(key))
                        {
                            offenders.Add($"{file.RelativePath}:{LineOf(attribute)}: {key}: {attribute.Name.LocalName}=\"{attribute.Value}\"");
                        }
                    }
                }
            }

            return (offenders, seen);
        }

        [Fact]
        public void E2_NoColorLiteralsInViews()
        {
            (List<string> offenders, HashSet<string> seen) = ScanColorLiterals(LoadAxamlFiles(), E2Allowlist);
            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E2Allowlist.Keys, seen);
        }

        private static AxamlFile Synthetic(string path, string xml) => new(path, XDocument.Parse(xml, LoadOptions.SetLineInfo));

        [Fact]
        public void E2_AnAllowlistedLiteralStaysAllowedWhenLinesAreAddedAboveIt()
        {
            const string palette = "<ColorPaletteResources RegionColor=\"#FF252525\" />";
            AxamlFile moved = Synthetic("src/NetPrints.Editor/EditorApp.axaml", "<Application>\n\n\n\n" + palette + "</Application>");

            var allowlist = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["src/NetPrints.Editor/EditorApp.axaml:ColorPaletteResources.RegionColor=#FF252525"] = "synthetic entry for this test",
            };

            (List<string> offenders, HashSet<string> seen) = ScanColorLiterals([moved], allowlist);

            Assert.Empty(offenders);
            Assert.Equal(allowlist.Keys.Order(StringComparer.Ordinal), seen.Order(StringComparer.Ordinal));
        }

        [Fact]
        public void E2_ARawColorOnAnotherElementIsNotCoveredByTheAllowlist()
        {
            AxamlFile file = Synthetic("src/NetPrints.Editor/EditorApp.axaml", "<Application>\n<Border Background=\"#FF252525\" />\n<ColorPaletteResources RegionColor=\"#FF000000\" /></Application>");

            (List<string> offenders, _) = ScanColorLiterals([file], new Dictionary<string, string>(StringComparer.Ordinal));

            Assert.Equal(2, offenders.Count);
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

        /// <summary>E8: no literal <c>FontSize</c> or <c>FontFamily</c> outside <c>EditorStyles.axaml</c> and
        /// <c>EditorApp.axaml</c>. Rule E8 drives all such literals to zero by replacing them with class-based
        /// styling or <c>Font.*</c> tokens. Allowlist shrinks to empty as T092/T092a fix each file.</summary>
        private static readonly Dictionary<string, string> E8Allowlist = new(StringComparer.Ordinal)
        {
        };

        [Fact]
        public void E8_NoFontSizeOrFontFamilyLiteralsOutsideEditorStyles()
        {
            AxamlFile[] files = LoadAxamlFiles();
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (AxamlFile file in files)
            {
                if (file.RelativePath is "src/NetPrints.Editor/EditorStyles.axaml" or "src/NetPrints.Editor/EditorApp.axaml"
                    || IsStyleOrResourceFile(file))
                {
                    continue;
                }

                bool hasFontSizeLiteral = false;
                bool hasFontFamilyLiteral = false;

                foreach (XElement element in file.Document.Descendants())
                {
                    // Check FontSize direct attributes and Setters
                    XAttribute? fontSizeAttr = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "FontSize" && !a.Value.TrimStart().StartsWith('{'));
                    if (fontSizeAttr is not null)
                    {
                        hasFontSizeLiteral = true;
                    }

                    if (element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "FontSize")
                    {
                        if (element.Attribute("Value") is { } value && !value.Value.TrimStart().StartsWith('{'))
                        {
                            hasFontSizeLiteral = true;
                        }
                    }

                    // Check FontFamily direct attributes and Setters
                    XAttribute? fontFamilyAttr = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "FontFamily" && !a.Value.TrimStart().StartsWith('{'));
                    if (fontFamilyAttr is not null)
                    {
                        hasFontFamilyLiteral = true;
                    }

                    if (element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "FontFamily")
                    {
                        if (element.Attribute("Value") is { } value && !value.Value.TrimStart().StartsWith('{'))
                        {
                            hasFontFamilyLiteral = true;
                        }
                    }
                }

                if (hasFontSizeLiteral)
                {
                    string key = $"{file.RelativePath}:FontSize";
                    seen.Add(key);
                    if (!E8Allowlist.ContainsKey(key))
                    {
                        offenders.Add(key);
                    }
                }

                if (hasFontFamilyLiteral)
                {
                    string key = $"{file.RelativePath}:FontFamily";
                    seen.Add(key);
                    if (!E8Allowlist.ContainsKey(key))
                    {
                        offenders.Add(key);
                    }
                }
            }

            Assert.True(offenders.Count == 0, "FontSize and FontFamily literals must be replaced with Font.* tokens or style classes (see E8): " + string.Join(", ", offenders));
            AssertAllowlistHasNoStaleEntries(E8Allowlist.Keys, seen);
        }

        private static AxamlFile LoadAxaml(string relativePath) =>
            LoadAxamlFiles().Single(file => file.RelativePath == relativePath);

        private static XElement[] StyleSetters(AxamlFile file, string selector) =>
            [.. file.Document.Descendants().Where(e => e.Name.LocalName == "Style" && e.Attribute("Selector")?.Value == selector).Elements()];

        [Fact]
        public void TheTabularStyleSetsMonoAndTnum()
        {
            AxamlFile styles = LoadAxaml("src/NetPrints.Editor/EditorStyles.axaml");

            foreach (string selector in new[] { "TextBlock.tabular", "TextBox.tabular" })
            {
                XElement[] setters = StyleSetters(styles, selector);
                Assert.Contains(setters, s => s.Attribute("Property")?.Value == "FontFeatures" && s.Attribute("Value")?.Value == "tnum");
                Assert.Contains(setters, s => s.Attribute("Property")?.Value == "FontFamily" && s.Attribute("Value")?.Value == "{StaticResource Font.Mono}");
            }
        }

        [Fact]
        public void TheCodeBlockClassUsesFontMono()
        {
            Assert.Contains(
                StyleSetters(LoadAxaml("src/NetPrints.Editor/EditorStyles.axaml"), "TextBox.codeBlock"),
                s => s.Attribute("Property")?.Value == "FontFamily" && s.Attribute("Value")?.Value == "{StaticResource Font.Mono}");
            Assert.Contains(
                LoadAxaml("src/NetPrints.Editor/Dialogs/ErrorDialog.axaml").Document.Descendants(),
                e => e.Name.LocalName == "TextBox" && e.Attribute("Classes")?.Value.Split(' ').Contains("codeBlock") == true);
        }

        [Theory]
        [InlineData("src/NetPrints.Editor/ErrorList/ErrorListView.axaml", "{Binding Header}")]
        [InlineData("src/NetPrints.Editor/Shell/ShellWindow.axaml", "{Binding BadgeText}")]
        [InlineData("src/NetPrints.Editor/Shell/ShellWindow.axaml", "{Binding StatusBar.BuildStateText}")]
        public void CountsTakeTheTabularClass(string path, string textBinding)
        {
            XElement block = LoadAxaml(path).Document.Descendants()
                .First(e => e.Name.LocalName == "TextBlock" && e.Attribute("Text")?.Value == textBinding);

            Assert.Contains("tabular", block.Attribute("Classes")?.Value.Split(' ') ?? []);
        }

        /// <summary>E9: icons come from <c>IconPresenter</c>. A <c>MaterialIcon</c>, <c>SymbolIcon</c> or <c>FluentIcon</c>
        /// element and a bitmap <c>Image</c> source may appear only under <c>src/NetPrints.Editor/Icons/</c>.</summary>
        private static readonly Dictionary<string, string> E9Allowlist = new(StringComparer.Ordinal)
        {
        };

        private static readonly HashSet<string> IconLibraryTags = new(StringComparer.Ordinal) { "MaterialIcon", "SymbolIcon", "FluentIcon" };

        private static bool IsBitmapSource(string? source) =>
            source is not null && !source.StartsWith("{DynamicResource", StringComparison.Ordinal) && !source.StartsWith("{StaticResource", StringComparison.Ordinal);

        private static (List<string> Offenders, HashSet<string> Seen) ScanIconElements(IEnumerable<AxamlFile> files, IReadOnlyDictionary<string, string> allowlist)
        {
            var offenders = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (AxamlFile file in files.Where(f => !f.RelativePath.StartsWith("src/NetPrints.Editor/Icons/", StringComparison.Ordinal)))
            {
                foreach (XElement element in file.Document.Descendants())
                {
                    string name = element.Name.LocalName;
                    bool offends = IconLibraryTags.Contains(name) || (name == "Image" && IsBitmapSource(element.Attribute("Source")?.Value));
                    if (!offends)
                    {
                        continue;
                    }

                    string key = IconLibraryTags.Contains(name) ? $"{file.RelativePath}:{LineOf(element)}" : $"{file.RelativePath}:Image";
                    seen.Add(key);
                    if (!allowlist.ContainsKey(key))
                    {
                        offenders.Add($"{key}: <{name}>");
                    }
                }
            }

            return (offenders, seen);
        }

        [Fact]
        public void E9_IconsComeFromIconPresenter()
        {
            (List<string> offenders, HashSet<string> seen) = ScanIconElements(LoadAxamlFiles(), E9Allowlist);

            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(E9Allowlist.Keys, seen);
        }

        [Fact]
        public void E9_FlagsAnIconElementOutsideTheIconsFolderOnly()
        {
            AxamlFile outside = Synthetic("src/NetPrints.Editor/X/V.axaml", "<UserControl xmlns:mi=\"clr-namespace:M\">\n<mi:MaterialIcon Kind=\"Plus\" />\n<Image Source=\"a.png\" />\n<Image Source=\"{Binding B}\" />\n<Image Source=\"{DynamicResource App.Mark}\" />\n</UserControl>");
            AxamlFile inside = Synthetic("src/NetPrints.Editor/Icons/IconPresenter.axaml", "<ControlTheme xmlns:mi=\"clr-namespace:M\">\n<mi:MaterialIcon Kind=\"Plus\" />\n</ControlTheme>");

            (List<string> offenders, _) = ScanIconElements([outside, inside], new Dictionary<string, string>());

            Assert.Equal(3, offenders.Count);
            Assert.All(offenders, offender => Assert.StartsWith("src/NetPrints.Editor/X/V.axaml", offender, StringComparison.Ordinal));
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

        [Fact]
        public void E2_StyleFilesAndBoxShadowsAreScannedForColorLiterals()
        {
            string[] styleFiles = [.. LoadAxamlFiles().Select(f => f.RelativePath).Where(p => p.Contains("/Styles/", StringComparison.Ordinal) || p.EndsWith("Styles.axaml", StringComparison.Ordinal))];
            Assert.NotEmpty(styleFiles);

            var none = new Dictionary<string, string>(StringComparer.Ordinal);
            (List<string> inStyles, _) = ScanColorLiterals(
                [Synthetic("src/NetPrints.Editor/Shell/Docking/DockStyles.axaml", "<Styles><Style Selector=\"Border\"><Setter Property=\"Background\" Value=\"#FF112233\" /><Setter Property=\"BoxShadow\" Value=\"0 4 12 0 Black\" /></Style></Styles>")], none);
            Assert.Equal(2, inStyles.Count);

            (List<string> shadow, _) = ScanColorLiterals([Synthetic("src/X/ViewStyles.axaml", "<Styles><Border BoxShadow=\"0 4 12 0 #40000000\" /></Styles>")], none);
            Assert.Single(shadow);

            (List<string> text, _) = ScanColorLiterals([Synthetic("src/X/ViewStyles.axaml", "<ResourceDictionary><BoxShadows x:Key=\"A\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">0 4 12 0 #40000000</BoxShadows></ResourceDictionary>")], none);
            Assert.Single(text);

            (List<string> themed, _) = ScanColorLiterals([Synthetic("src/X/ViewStyles.axaml", "<ResourceDictionary><ResourceDictionary.ThemeDictionaries><ResourceDictionary x:Key=\"Dark\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><BoxShadows x:Key=\"A\">0 4 12 0 #40000000</BoxShadows></ResourceDictionary></ResourceDictionary.ThemeDictionaries></ResourceDictionary>")], none);
            Assert.Empty(themed);
        }

        [Fact]
        public void NoNumericAncestorLevelSelectors()
        {
            var offenders = new List<string>();
            foreach (AxamlFile file in LoadAxamlFiles())
            {
                offenders.AddRange(FindNumericAncestorLevels(file));
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void NumericAncestorLevelsAreFlaggedInPlantedBindings()
        {
            AxamlFile planted = Synthetic("src/X/V.axaml", "<UserControl><Button Command=\"{Binding $parent[Border;2].DataContext.Go}\" /><Button Tag=\"{Binding RelativeSource={RelativeSource AncestorType=Grid, AncestorLevel=2}}\" /><Button Command=\"{Binding $parent[ListBox].DataContext.Go}\" /></UserControl>");
            Assert.Equal(2, FindNumericAncestorLevels(planted).Count());
        }

        private static IEnumerable<string> FindNumericAncestorLevels(AxamlFile file) =>
            file.Document.Descendants().SelectMany(e => e.Attributes())
                .Where(a => NumericAncestorLevelPattern.IsMatch(a.Value))
                .Select(a => $"{file.RelativePath}:{LineOf(a)}: {a.Value}");

        /// <summary>Dialog windows (<c>*Dialog.axaml</c> with a <c>Window</c> root) size to their content, per the
        /// "Layout and look" dialog anatomy in the avalonia-styling skill. Keyed by file path; the reason is why the
        /// fixed size stays. Entries are legacy fixed sizes: remove one when its dialog moves to SizeToContent.</summary>
        private static readonly Dictionary<string, string> DialogSizingAllowlist = new(StringComparer.Ordinal);

        private static bool SizesToContent(AxamlFile file) =>
            file.Document.Root?.Attribute("SizeToContent") is { } size && !string.Equals(size.Value, "Manual", StringComparison.Ordinal);

        private static bool IsDialogWindow(AxamlFile file) =>
            file.RelativePath.EndsWith("Dialog.axaml", StringComparison.Ordinal) && file.Document.Root?.Name.LocalName == "Window";

        [Fact]
        public void DialogWindowsSizeToContentOrAreAllowlisted()
        {
            AxamlFile[] dialogs = [.. LoadAxamlFiles().Where(IsDialogWindow)];
            Assert.NotEmpty(dialogs);

            string[] offenders = [.. dialogs.Where(d => !SizesToContent(d) && !DialogSizingAllowlist.ContainsKey(d.RelativePath)).Select(d => d.RelativePath)];
            Assert.Empty(offenders);
            AssertAllowlistHasNoStaleEntries(DialogSizingAllowlist.Keys, [.. dialogs.Where(d => !SizesToContent(d)).Select(d => d.RelativePath)]);
        }

        [Fact]
        public void ADialogWithAFixedSizeAndNoSizeToContentIsFlagged()
        {
            AxamlFile fixedSize = Synthetic("src/X/NewDialog.axaml", "<Window Width=\"400\" Height=\"300\" />");
            AxamlFile sized = Synthetic("src/X/OkDialog.axaml", "<Window SizeToContent=\"WidthAndHeight\" MaxWidth=\"600\" />");
            Assert.True(IsDialogWindow(fixedSize) && !SizesToContent(fixedSize));
            Assert.True(IsDialogWindow(sized) && SizesToContent(sized));
        }

        private const string RatchetBaselinePath = "tests/NetPrints.Core.Tests/Core/xaml-literal-ratchet.txt";

        private static bool IsStyleOrResourceFile(AxamlFile file) =>
            file.Document.Root?.Name.LocalName is "Application" or "Styles" or "ResourceDictionary";

        /// <summary>Counts literal (non-binding) <c>Margin</c> and <c>FontSize</c> values per view, including
        /// <c>Setter</c> values, as <c>"Margin path" -> count</c> and <c>"FontSize path" -> count</c>.</summary>
        private static SortedDictionary<string, int> CountLiterals(IEnumerable<AxamlFile> files)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (AxamlFile file in files.Where(f => !IsStyleOrResourceFile(f)))
            {
                foreach (XElement element in file.Document.Descendants())
                {
                    var hits = new List<string>();
                    if (element.Name.LocalName == "Setter")
                    {
                        if (element.Attribute("Property")?.Value is ("Margin" or "FontSize") and var property
                            && element.Attribute("Value") is { } value && !value.Value.TrimStart().StartsWith('{'))
                        {
                            hits.Add(property);
                        }
                    }
                    else
                    {
                        hits.AddRange(element.Attributes()
                            .Where(a => a.Name.LocalName is "Margin" or "FontSize" && !a.Value.TrimStart().StartsWith('{'))
                            .Select(a => a.Name.LocalName));
                    }

                    foreach (string hit in hits)
                    {
                        string key = $"{hit} {file.RelativePath}";
                        counts[key] = counts.GetValueOrDefault(key) + 1;
                    }
                }
            }

            return counts;
        }

        private static Dictionary<string, int> ReadRatchetBaseline()
        {
            var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(Path.Combine(SampleProjectFactory.FindRepositoryRoot(), RatchetBaselinePath)))
            {
                int split = line.LastIndexOf(' ');
                if (line.Length > 0 && !line.StartsWith('#') && split > 0)
                {
                    baseline[line[..split]] = int.Parse(line[(split + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return baseline;
        }

        /// <summary>Ratchet, not a cleanup: literal Margin and FontSize counts per view may not rise above the
        /// checked-in baseline. Rule E8 later drives FontSize to zero.</summary>
        [Fact]
        public void MarginAndFontSizeLiteralsNeverExceedTheBaseline()
        {
            SortedDictionary<string, int> current = CountLiterals(LoadAxamlFiles());
            if (Environment.GetEnvironmentVariable("NETPRINTS_UPDATE_RATCHET") == "1")
            {
                File.WriteAllLines(
                    Path.Combine(SampleProjectFactory.FindRepositoryRoot(), RatchetBaselinePath),
                    ["# Literal Margin/FontSize counts per view (XamlHygieneTests ratchet). Regenerate with NETPRINTS_UPDATE_RATCHET=1 only when counts went down.", .. current.Select(c => $"{c.Key} {c.Value}")]);
            }

            Dictionary<string, int> baseline = ReadRatchetBaseline();

            string[] rose = [.. current.Where(c => c.Value > baseline.GetValueOrDefault(c.Key)).Select(c => $"{c.Key}: {baseline.GetValueOrDefault(c.Key)} -> {c.Value}")];
            Assert.True(rose.Length == 0, "Literal Margin/FontSize count went up (use a style class from EditorStyles.axaml instead): " + string.Join("; ", rose));

            string[] lower = [.. baseline.Where(b => current.GetValueOrDefault(b.Key) < b.Value).Select(b => $"{b.Key} {current.GetValueOrDefault(b.Key)}")];
            if (lower.Length > 0)
            {
                TestContext.Current.SendDiagnosticMessage($"Ratchet: {lower.Length} baseline entries can be lowered. Lower {RatchetBaselinePath} (run this test once with NETPRINTS_UPDATE_RATCHET=1) to: {string.Join("; ", lower)}");
            }
        }

        [Fact]
        public void TheRatchetCountsLiteralsAndIgnoresBindingsAndStyleFiles()
        {
            AxamlFile view = Synthetic("src/X/V.axaml", "<UserControl><Border Margin=\"4\" FontSize=\"12\"><TextBlock Margin=\"{Binding M}\" /><Style><Setter Property=\"FontSize\" Value=\"10\" /></Style></Border></UserControl>");
            AxamlFile styles = Synthetic("src/X/S.axaml", "<Styles><Setter Property=\"Margin\" Value=\"1\" /></Styles>");

            SortedDictionary<string, int> counts = CountLiterals([view, styles]);

            Assert.Equal(1, counts["Margin src/X/V.axaml"]);
            Assert.Equal(2, counts["FontSize src/X/V.axaml"]);
            Assert.Equal(2, counts.Count);
        }

        private static readonly Regex NumericAncestorLevelPattern = NumericAncestorLevelRegex();

        [GeneratedRegex(@"\$parent\[[^\]]*;\s*\d+\s*\]|AncestorLevel\s*=\s*\d+")]
        private static partial Regex NumericAncestorLevelRegex();

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
