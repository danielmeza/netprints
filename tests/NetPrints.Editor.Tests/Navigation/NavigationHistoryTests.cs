using NetPrints.Editor.Graph;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Navigation;

public class NavigationHistoryTests
{
    private static readonly DocumentId Home = DocumentId.Graph("A.netpc.json", DocumentId.ClassGraphKey);

    private static DocumentId Doc(int index) => DocumentId.Graph("A.netpc.json", DocumentId.MethodKeyPrefix + index);

    private static NavigationEntry Entry(DocumentId document, double x = 0, double zoom = 1, params string[] selected) =>
        new(document, new GraphPoint(x, 0), zoom, selected);

    private static bool Always(DocumentId id) => true;

    [Fact]
    public void BackReturnsTheLastRecordedEntryAndForwardTheOneItLeft()
    {
        var history = new NavigationHistory();
        history.Record(Entry(Doc(1), x: 10));

        NavigationEntry? back = history.Back(Entry(Doc(2), x: 20), Always);

        Assert.Equal(Doc(1), back?.Document);
        Assert.Equal(10, back?.Location.X);
        NavigationEntry? forward = history.Forward(Entry(Doc(1), x: 10), Always);
        Assert.Equal(Doc(2), forward?.Document);
        Assert.Equal(20, forward?.Location.X);
    }

    [Fact]
    public void ANewNavigationClearsForward()
    {
        var history = new NavigationHistory();
        history.Record(Entry(Doc(1)));
        history.Back(Entry(Doc(2)), Always);
        Assert.Equal(1, history.ForwardCount);

        history.Record(Entry(Doc(3)));

        Assert.Equal(0, history.ForwardCount);
        Assert.Null(history.Forward(Entry(Doc(3)), Always));
    }

    [Fact]
    public void AtMostFiftyEntriesAreKeptEachWayAndTheOldestGoFirst()
    {
        var history = new NavigationHistory();
        for (int i = 0; i < 60; i++)
        {
            history.Record(Entry(Doc(i), x: i));
        }

        Assert.Equal(NavigationHistory.Capacity, history.BackCount);
        Assert.Equal(50, NavigationHistory.Capacity);
        NavigationEntry? oldest = null;
        for (int i = 0; i < 50; i++)
        {
            oldest = history.Back(Entry(Home), Always);
        }

        Assert.Equal(Doc(10), oldest?.Document);
        Assert.Equal(50, history.ForwardCount);
        Assert.Equal(0, history.BackCount);
        for (int i = 0; i < 70; i++)
        {
            history.Record(Entry(Doc(i)));
            history.Back(Entry(Home, x: i), Always);
        }

        Assert.True(history.ForwardCount <= NavigationHistory.Capacity);
    }

    [Fact]
    public void EntriesWhoseDocumentIsGoneAreSkipped()
    {
        var history = new NavigationHistory();
        history.Record(Entry(Doc(1)));
        history.Record(Entry(Doc(2)));
        history.Record(Entry(Doc(3)));

        NavigationEntry? back = history.Back(Entry(Home), id => id == Doc(1));

        Assert.Equal(Doc(1), back?.Document);
        Assert.Equal(0, history.BackCount);
        Assert.Null(history.Back(Entry(Home), id => id == Doc(1)));
    }

    [Fact]
    public void WhenEveryEntryIsGoneBackReturnsNullAndLeavesForwardAlone()
    {
        var history = new NavigationHistory();
        history.Record(Entry(Doc(1)));

        Assert.Null(history.Back(Entry(Home), id => false));
        Assert.Equal(0, history.ForwardCount);
        Assert.False(history.CanGoBack(id => false));
    }

    [Fact]
    public void AnEntryIdenticalToTheLastOneIsNotRecordedTwice()
    {
        var history = new NavigationHistory();
        history.Record(Entry(Doc(1), 5, 2, "n1"));
        history.Record(Entry(Doc(1), 5, 2, "n1"));

        Assert.Equal(1, history.BackCount);
    }
}
