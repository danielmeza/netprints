namespace NetPrints.Analyzers.Tests.Pins;

/// <summary>The shared prelude of the NPA rule samples.</summary>
internal static class PinSamples
{
    /// <summary>The usings and namespace every sample starts with.</summary>
    public const string Header = """
        using System.ComponentModel;
        using NetPrints.Core;
        using NetPrints.Graph;

        namespace Sample;


        """;
}
