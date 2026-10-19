using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK016: the <c>.axaml</c> views of the extension hold no colour literal outside a theme dictionary and no font size or family literal, as rules E2 and E8 of the editor's XAML hygiene tests.</summary>
internal sealed partial class ContributedViewsCheck : ConformanceCheck
{
    private const string Rule = "no colour or font literal a theme cannot reach";
    private const string Transparent = "Transparent";
    private const string Setter = "Setter";
    private const string FontSize = "FontSize";
    private const string FontFamily = "FontFamily";

    private static readonly string[] ColorSuffixes =
    [
        "Background", "Foreground", "Fill", "Stroke", "BorderBrush", "Color", "BoxShadow", "CaretBrush", "SelectionBrush", "OpacityMask",
    ];

    private static readonly HashSet<string> ColorTextElements = new(StringComparer.Ordinal) { "BoxShadows", "Color", "SolidColorBrush" };

    private static readonly HashSet<string> NonViewRoots = new(StringComparer.Ordinal) { "Application", "Styles", "ResourceDictionary" };

    public override string Code => NpkDiagnosticCodes.ContributedViews;

    public override string Name => "Contributed views";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        string? directory = context.Options.SourceDirectory ?? context.Options.ExtensionFolder;
        if (directory is null || !Directory.Exists(directory))
        {
            return Task.FromResult(CheckOutcome.Skip("no SourceDirectory or ExtensionFolder"));
        }

        string[] files = [.. ViewSources.Find(directory)];
        if (files.Length == 0)
        {
            return Task.FromResult(CheckOutcome.Passed("no .axaml source"));
        }

        var findings = new List<ConformanceFinding>();
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string name = Path.GetRelativePath(directory, file).Replace('\\', '/');
            XDocument document;
            try
            {
                document = XDocument.Load(file, LoadOptions.SetLineInfo);
            }
            catch (XmlException ex)
            {
                findings.Add(Finding(Rule, $"{name}: the file is not well formed XML: {ex.Message}"));
                continue;
            }

            if (document.Root is not { } root || NonViewRoots.Contains(root.Name.LocalName))
            {
                continue;
            }

            Scan(document, name, findings);
        }

        return Task.FromResult(CheckOutcome.FromFindings(findings));
    }

    private static bool IsColorLiteral(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.StartsWith('{') || string.Equals(trimmed, Transparent, StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => HexColor().IsMatch(token) || NamedColor().IsMatch(token));
    }

    private static bool IsLiteral(string value) => !value.TrimStart().StartsWith('{');

    private static int LineOf(XObject node) => node is IXmlLineInfo info ? info.LineNumber : 0;

    private void Scan(XDocument document, string name, List<ConformanceFinding> findings)
    {
        foreach (XElement element in document.Descendants())
        {
            bool themed = element.AncestorsAndSelf().Any(ancestor => ancestor.Name.LocalName.EndsWith("ThemeDictionaries", StringComparison.Ordinal));
            string tag = element.Name.LocalName;
            if (!themed && ColorTextElements.Contains(tag) && !element.HasElements && IsColorLiteral(element.Value))
            {
                findings.Add(Finding(Rule, $"{name}:{LineOf(element)}: {tag} holds the colour {element.Value.Trim()}"));
            }

            if (tag == Setter)
            {
                string property = element.Attribute("Property")?.Value ?? string.Empty;
                string value = element.Attribute("Value")?.Value ?? string.Empty;
                if (!themed && ColorSuffixes.Any(suffix => property.EndsWith(suffix, StringComparison.Ordinal)) && IsColorLiteral(value))
                {
                    findings.Add(Finding(Rule, $"{name}:{LineOf(element)}: Setter {property}={value.Trim()} is a colour literal"));
                }

                if (property is FontSize or FontFamily && IsLiteral(value))
                {
                    findings.Add(Finding(Rule, $"{name}:{LineOf(element)}: Setter {property}={value.Trim()} is a font literal; use a type ramp token or a style class"));
                }

                continue;
            }

            foreach (XAttribute attribute in element.Attributes())
            {
                string attributeName = attribute.Name.LocalName;
                if (!themed && ColorSuffixes.Any(suffix => attributeName.EndsWith(suffix, StringComparison.Ordinal)) && IsColorLiteral(attribute.Value))
                {
                    findings.Add(Finding(Rule, $"{name}:{LineOf(attribute)}: {tag}.{attributeName}={attribute.Value.Trim()} is a colour literal"));
                }

                if (attributeName is FontSize or FontFamily && IsLiteral(attribute.Value))
                {
                    findings.Add(Finding(Rule, $"{name}:{LineOf(attribute)}: {tag}.{attributeName}={attribute.Value.Trim()} is a font literal; use a type ramp token or a style class"));
                }
            }
        }
    }

    [GeneratedRegex("^(AliceBlue|Aqua|Beige|Black|Blue|Brown|Crimson|Cyan|DarkGray|DarkGreen|DarkOrange|DarkRed|DimGray|DodgerBlue|Gold|Gray|Green|Grey|HotPink|Indigo|LightBlue|LightGray|LightGreen|Lime|Magenta|Maroon|Navy|Olive|Orange|OrangeRed|Pink|Purple|Red|Salmon|Silver|SkyBlue|Teal|Tomato|Violet|White|WhiteSmoke|Yellow|YellowGreen)$")]
    private static partial Regex NamedColor();

    [GeneratedRegex(@"#[0-9A-Fa-f]{3,8}\b")]
    private static partial Regex HexColor();
}
