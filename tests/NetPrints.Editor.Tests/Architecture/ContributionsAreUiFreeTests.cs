using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>ADR-0020, contracts/contributions.md: the <c>NetPrints.Editor.Contributions</c> namespace references no Avalonia, Nodify or Dock type, so P3 can move it into a public package.</summary>
public class ContributionsAreUiFreeTests
{
    private static readonly string[] BannedRoots = ["Avalonia", "Nodify", "Dock"];

    private static bool IsBanned(string name) =>
        BannedRoots.Any(root => name == root || name.StartsWith(root + ".", StringComparison.Ordinal));

    /// <summary>The banned names a source text mentions: using directives, qualified names and aliases.</summary>
    private static List<string> BannedNamesIn(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        return [.. root.DescendantNodes().OfType<UsingDirectiveSyntax>().Select(u => u.NamespaceOrType.ToString())
            .Concat(root.DescendantNodes().OfType<QualifiedNameSyntax>().Select(q => q.ToString().Replace("global::", string.Empty, StringComparison.Ordinal)))
            .Where(IsBanned)];
    }

    [Fact]
    public void NoSourceFileOfTheNamespaceMentionsAUiToolkit()
    {
        string folder = Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Editor", "Contributions");
        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(files);

        var offenders = files.SelectMany(file => BannedNamesIn(File.ReadAllText(file)).Select(name => $"{Path.GetFileName(file)}: {name}"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void NoPublicSignatureOfTheNamespaceUsesAUiToolkitType()
    {
        var types = typeof(IContributionRegistry).Assembly.GetTypes().Where(t => t.Namespace is { } ns && (ns == typeof(IContributionRegistry).Namespace || ns.StartsWith(typeof(IContributionRegistry).Namespace + ".", StringComparison.Ordinal))).ToList();
        Assert.NotEmpty(types);

        const System.Reflection.BindingFlags all = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
        var used = types.SelectMany(t => new[] { t.BaseType }.Concat(t.GetInterfaces())
            .Concat(t.GetFields(all).Select(f => f.FieldType))
            .Concat(t.GetProperties(all).Select(p => p.PropertyType))
            .Concat(t.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))))
            .OfType<Type>()
            .SelectMany(t => t.IsGenericType ? t.GetGenericArguments().Append(t) : [t])
            .Select(t => t.Namespace ?? string.Empty);

        Assert.Empty(used.Where(IsBanned).Distinct());
    }

    [Theory]
    [InlineData("using Avalonia.Input;\nclass C { }")]
    [InlineData("using Dock.Model.Core;\nclass C { }")]
    [InlineData("class C { Nodify.Connection c; }")]
    [InlineData("class C { global::Avalonia.Point p; }")]
    public void TheScannerFlagsAToolkitReference(string source) => Assert.NotEmpty(BannedNamesIn(source));

    [Fact]
    public void TheScannerAcceptsPlainCode() => Assert.Empty(BannedNamesIn("using System;\nclass C { Docker d; }"));
}
