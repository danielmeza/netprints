using Avalonia.Headless.XUnit;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.UITests.Shell;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>FR-027, research R13: a pin has its own automation id built from direction, kind and key, so a test finds it without its label or position.</summary>
public class PinAutomationIdTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task APinIsFoundByItsPerPinId()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var entry = session.Graph.Node("MethodEntryNode");

        var exec = entry.PinById(isInput: false, PinKind.Exec, "Exec");

        Assert.Equal("Graph.Pin.Out.Exec.Exec", AutomationIds.ForPin(isInput: false, PinKind.Exec, "Exec"));
        Assert.NotNull(await exec.GetAsync(Token));
    }

    [Fact]
    public void EveryDirectionAndKindGetsADistinctId()
    {
        string[] ids =
        [
            AutomationIds.ForPin(isInput: true, PinKind.Data, "Value"),
            AutomationIds.ForPin(isInput: false, PinKind.Data, "Value"),
            AutomationIds.ForPin(isInput: true, PinKind.Type, "Value"),
            AutomationIds.ForPin(isInput: true, PinKind.Exec, "Value"),
        ];

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }
}
