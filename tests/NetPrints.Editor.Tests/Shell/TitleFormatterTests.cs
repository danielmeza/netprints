using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>contracts/shell.md section 4: the window title.</summary>
public class TitleFormatterTests
{
    [Fact]
    public void WithNoProjectTheTitleIsJustTheProductName()
    {
        Assert.Equal("NetPrints", TitleFormatter.Format(null, null, hasUnsavedFiles: false));
        Assert.Equal("NetPrints", TitleFormatter.Format(null, "Start", hasUnsavedFiles: true));
    }

    [Fact]
    public void WithAProjectTheTitleNamesTheActiveDocumentThenTheProject()
    {
        Assert.Equal("Main – Calc – NetPrints", TitleFormatter.Format("Calc", "Main", hasUnsavedFiles: false));
    }

    [Fact]
    public void AnUnsavedFileMarksTheProjectName()
    {
        Assert.Equal("Main* – Calc* – NetPrints", TitleFormatter.Format("Calc", "Main*", hasUnsavedFiles: true));
    }

    [Fact]
    public void WithoutAnActiveDocumentTheTitleStartsWithTheProject()
    {
        Assert.Equal("Calc – NetPrints", TitleFormatter.Format("Calc", null, hasUnsavedFiles: false));
        Assert.Equal("Calc* – NetPrints", TitleFormatter.Format("Calc", "", hasUnsavedFiles: true));
    }
}
