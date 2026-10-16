using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NetPrints.Cli.Git;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>FR-007 (research R7): <c>format</c>, <c>show</c> and the merge driver keep unknown document members as opaque values.</summary>
public sealed class UnknownMembersTests : IDisposable
{
    private static readonly string FixturePath = Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "HelloWorld", "HelloWorld.Program.netpc.json");

    private readonly string _temp = Directory.CreateTempSubdirectory("np-unknown-").FullName;
    private readonly IDocumentFormat _format = GraphFormats.CreateRegistry().Default;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static JsonObject Fixture() => JsonNode.Parse(File.ReadAllText(FixturePath))?.AsObject()
        ?? throw new InvalidOperationException("The fixture is not a JSON object.");

    private static JsonObject Node(JsonObject root) => root["methods"]?[0]?["graph"]?["nodes"]?[2]?.AsObject()
        ?? throw new InvalidOperationException("No call node.");

    private static JsonObject BaseWithUnknown()
    {
        JsonObject root = Fixture();
        root["x"] = 1;
        Node(root)["hint"] = "a";
        return root;
    }

    private async Task<ClassDocument> ReadAsync(JsonObject root)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString()));
        return await _format.ReadClassAsync(input, new DocumentId("a.netpc.json"), TestContext.Current.CancellationToken);
    }

    private async Task<string> PrintAsync(ClassDocument document)
    {
        using var output = new MemoryStream();
        await _format.WriteClassAsync(document, output, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private async Task<MergeOutcome> MergeAsync(JsonObject b, JsonObject o, JsonObject t) =>
        await new GraphMerger(_format).MergeAsync(await ReadAsync(b), await ReadAsync(o), await ReadAsync(t), TestContext.Current.CancellationToken);

    private async Task<(int Exit, string Output)> ShowAsync(JsonObject root)
    {
        string path = Path.Combine(_temp, "U.netpc.json");
        await File.WriteAllTextAsync(path, root.ToJsonString(), TestContext.Current.CancellationToken);
        var host = new CliTestHost(_temp);
        int exit = await host.RunAsync("show", path);
        return (exit, host.Output.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FormatKeepsUnknownMembersAndWritesThemInOrdinalOrder()
    {
        JsonObject root = BaseWithUnknown();
        root["b"] = 2;
        string path = Path.Combine(_temp, "A.netpc.json");
        await File.WriteAllTextAsync(path, root.ToJsonString(), TestContext.Current.CancellationToken);

        var host = new CliTestHost(_temp);
        Assert.Equal(ExitCodes.Success, await host.RunAsync("format", path));

        JsonObject formatted = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))?.AsObject()
            ?? throw new InvalidOperationException("Not an object.");
        Assert.Equal(1, (int?)formatted["x"]);
        Assert.Equal(2, (int?)formatted["b"]);
        Assert.Equal("a", (string?)Node(formatted)["hint"]);
        string text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        Assert.True(text.IndexOf("\"b\":", StringComparison.Ordinal) < text.IndexOf("\"x\":", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShowPrintsUnknownMembersAsOpaqueValues()
    {
        JsonObject root = BaseWithUnknown();
        root["opaque"] = new JsonObject { ["k"] = new JsonArray(1, 2) };

        (int exit, string output) = await ShowAsync(root);

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("\n  unknown opaque {\"k\":[1,2]}\n", output, StringComparison.Ordinal);
        Assert.Contains("\n  unknown x 1\n", output, StringComparison.Ordinal);
        Assert.Contains("\n      unknown hint \"a\"\n", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowIsUnchangedWithoutUnknownMembers()
    {
        (int exit, string output) = await ShowAsync(Fixture());

        Assert.Equal(ExitCodes.Success, exit);
        Assert.DoesNotContain("unknown ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChangeOnOneSideAndAnAdditionOnTheOtherAreBothKept()
    {
        JsonObject ours = BaseWithUnknown();
        ours["x"] = 2;
        JsonObject theirs = BaseWithUnknown();
        theirs["y"] = "t";

        MergeOutcome outcome = await MergeAsync(BaseWithUnknown(), ours, theirs);

        string merged = await PrintAsync(Assert.IsType<MergeOutcome.Clean>(outcome).Document);
        JsonObject result = JsonNode.Parse(merged)?.AsObject() ?? throw new InvalidOperationException("Not an object.");
        Assert.Equal(2, (int?)result["x"]);
        Assert.Equal("t", (string?)result["y"]);
    }

    [Fact]
    public async Task TheSameChangeOnBothSidesIsNotAConflict()
    {
        JsonObject ours = BaseWithUnknown();
        ours["x"] = 5;
        JsonObject theirs = BaseWithUnknown();
        theirs["x"] = 5;

        MergeOutcome outcome = await MergeAsync(BaseWithUnknown(), ours, theirs);

        Assert.IsType<MergeOutcome.Clean>(outcome);
    }

    [Fact]
    public async Task ADifferentChangeOfTheSameMemberOnBothSidesConflicts()
    {
        JsonObject ours = BaseWithUnknown();
        ours["x"] = 2;
        JsonObject theirs = BaseWithUnknown();
        theirs["x"] = 3;

        MergeOutcome outcome = await MergeAsync(BaseWithUnknown(), ours, theirs);

        MergeConflict conflict = Assert.Single(Assert.IsType<MergeOutcome.Conflicted>(outcome).Conflicts);
        Assert.Equal(MergeConflictKind.Scalar, conflict.Kind);
        Assert.Contains("x", conflict.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownMemberOfANodeMergesWithAKnownChangeOfTheSameNode()
    {
        JsonObject ours = BaseWithUnknown();
        Node(ours)["hint"] = "b";
        JsonObject theirs = BaseWithUnknown();
        Node(theirs)["pins"]?[0]?.AsObject()["value"]?.AsObject().Remove("value");
        Node(theirs)["pins"]?[0]?.AsObject()["value"]?.AsObject().Add("value", "Hi");

        MergeOutcome outcome = await MergeAsync(BaseWithUnknown(), ours, theirs);

        JsonObject result = JsonNode.Parse(await PrintAsync(Assert.IsType<MergeOutcome.Clean>(outcome).Document))?.AsObject()
            ?? throw new InvalidOperationException("Not an object.");
        Assert.Equal("b", (string?)Node(result)["hint"]);
        Assert.Equal("Hi", (string?)Node(result)["pins"]?[0]?["value"]?["value"]);
    }

    [Fact]
    public async Task ADeletionOnOneSideWithTheOtherSideUnchangedIsApplied()
    {
        JsonObject ours = BaseWithUnknown();
        ours.Remove("x");

        MergeOutcome outcome = await MergeAsync(BaseWithUnknown(), ours, BaseWithUnknown());

        JsonObject result = JsonNode.Parse(await PrintAsync(Assert.IsType<MergeOutcome.Clean>(outcome).Document))?.AsObject()
            ?? throw new InvalidOperationException("Not an object.");
        Assert.False(result.ContainsKey("x"));
    }
}
