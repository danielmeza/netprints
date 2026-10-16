using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Style;

/// <summary>The contract of the NPS descriptors (contracts/analyzer-diagnostics.md §2): severity, and off until the repository turns them on (T005).</summary>
public class NpsDescriptorTests
{
    public static TheoryData<string, DiagnosticSeverity> Contract => new()
    {
        { "NPS001", DiagnosticSeverity.Error },
        { "NPS002", DiagnosticSeverity.Error },
        { "NPS003", DiagnosticSeverity.Info },
    };

    [Theory]
    [MemberData(nameof(Contract))]
    public void EachRuleHasItsContractSeverityAndIsOffUntilTheRepositoryEnablesIt(string id, DiagnosticSeverity severity)
    {
        DiagnosticDescriptor descriptor = AnalyzerBinaries.Load("NetPrints.RepoAnalyzers").GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
            .SelectMany(type => Assert.IsType<DiagnosticAnalyzer>(System.Activator.CreateInstance(type), exactMatch: false).SupportedDiagnostics)
            .Single(candidate => candidate.Id == id);

        Assert.Equal(severity, descriptor.DefaultSeverity);
        Assert.False(descriptor.IsEnabledByDefault);
        Assert.EndsWith("#" + id.ToLowerInvariant(), descriptor.HelpLinkUri);
    }
}
