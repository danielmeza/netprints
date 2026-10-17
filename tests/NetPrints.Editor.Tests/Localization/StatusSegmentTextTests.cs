using System.Globalization;
using NetPrints.Compilation;
using NetPrints.Editor.Contributions.BuiltIn;

namespace NetPrints.Editor.Tests.Localization;

/// <summary>FR-027, CD-14: the text of the built-in status bar segments comes from the resx, with one text per plural form.</summary>
public class StatusSegmentTextTests
{
    public static TheoryData<string> Keys => new()
    {
        "Status.ErrorsOne",
        "Status.ErrorsOther",
        "Status.WarningsOne",
        "Status.WarningsOther",
        "Status.Selected",
        "Status.Zoom",
    };

    [Fact]
    public void ACountUsesTheSingularFormForOneAndThePluralFormOtherwise()
    {
        Assert.Equal("1 error", StatusBarContributions.CountText(1, CodeDiagnosticSeverity.Error));
        Assert.Equal("3 errors", StatusBarContributions.CountText(3, CodeDiagnosticSeverity.Error));
        Assert.Equal("1 warning", StatusBarContributions.CountText(1, CodeDiagnosticSeverity.Warning));
        Assert.Equal("2 warnings", StatusBarContributions.CountText(2, CodeDiagnosticSeverity.Warning));
    }

    [Fact]
    public void TheSelectionAndZoomTextsFollowTheResx()
    {
        Assert.Equal("2 selected", StatusBarContributions.SelectedText(2));
        Assert.Equal("150%", StatusBarContributions.ZoomText(1.5));
        Assert.Equal("33%", StatusBarContributions.ZoomText(1.0 / 3));
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void ASegmentTextIsInTheNeutralResx(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(Strings.ResourceManager.GetString(key, CultureInfo.InvariantCulture)), key);
    }
}
