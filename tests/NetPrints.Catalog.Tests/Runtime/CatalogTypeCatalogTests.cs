using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests.Runtime;

/// <summary>CT-T09 and CT-T18: what a catalog loaded at run time answers, and how two catalogs with one id are resolved.</summary>
public sealed class CatalogTypeCatalogTests
{
    private static readonly CatalogDocument Document = FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId).Document;

    private static readonly ITypeCatalog Catalog = CatalogLoader.Load(Document);

    private static readonly TypeSpecifier Circle = new("Fixture.Geometry.Circle");

    private static readonly TypeSpecifier ShapeBase = new("Fixture.Geometry.ShapeBase");

    private static readonly TypeSpecifier Shape = new("Fixture.Geometry.IShape", isInterface: true);

    private static readonly TypeSpecifier Vector2 = new("Fixture.Geometry.Vector2");

    private static readonly TypeSpecifier Single = new("System.Single");

    private static readonly TypeSpecifier Level = new("Fixture.Legacy.Level", isEnum: true);

    private static MethodSpecifier MethodOf(TypeSpecifier type, string name) =>
        Catalog.GetMethods(new ReflectionProviderMethodQuery().WithType(type)).Single(method => method.Name == name && method.DeclaringType == type);

    [Fact]
    public void InfoCarriesTheCatalogIdVersionAndCoveredAssemblies()
    {
        Assert.Equal(FixtureCatalog.FixtureId, Catalog.Info.Id);
        Assert.Equal(Document.Version, Catalog.Info.Version);
        Assert.Equal(["CatalogFixtureLib"], Catalog.Info.CoveredAssemblyNames);
    }

    [Fact]
    public void SubclassFollowsBaseTypesAndInterfacesTransitively()
    {
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Circle, Circle));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Circle, ShapeBase));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Circle, Shape));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Circle, new TypeSpecifier("System.Object")));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Vector2, Shape));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Vector2, new TypeSpecifier("System.IEquatable", false, true, [Vector2])));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Vector2, new TypeSpecifier("System.ValueType")));
        Assert.True(Catalog.TypeSpecifierIsSubclassOf(Level, new TypeSpecifier("System.Enum")));

        Assert.False(Catalog.TypeSpecifierIsSubclassOf(ShapeBase, Circle));
        Assert.False(Catalog.TypeSpecifierIsSubclassOf(Shape, ShapeBase));
        Assert.False(Catalog.TypeSpecifierIsSubclassOf(Circle, Vector2));
        Assert.False(Catalog.TypeSpecifierIsSubclassOf(new TypeSpecifier("System.Int32"), ShapeBase));
    }

    [Fact]
    public void ImplicitCastsCoverIdentityReferenceBoxingAndOperatorImplicit()
    {
        Assert.True(Catalog.HasImplicitCast(Circle, Circle));
        Assert.True(Catalog.HasImplicitCast(Circle, ShapeBase));
        Assert.True(Catalog.HasImplicitCast(Circle, Shape));
        Assert.True(Catalog.HasImplicitCast(Vector2, Shape));
        Assert.True(Catalog.HasImplicitCast(Shape, new TypeSpecifier("System.Object")));
        Assert.True(Catalog.HasImplicitCast(Single, Vector2));

        Assert.False(Catalog.HasImplicitCast(ShapeBase, Circle));
        Assert.False(Catalog.HasImplicitCast(Vector2, Single));
        Assert.False(Catalog.HasImplicitCast(Single, Shape));
        Assert.False(Catalog.HasImplicitCast(Circle, Vector2));
    }

    [Fact]
    public void DocumentationIsAnsweredPerMethodParameterAndReturn()
    {
        MethodSpecifier add = MethodOf(Vector2, "Add");

        Assert.Equal("Adds another vector to this one.", Catalog.GetMethodDocumentation(add));
        Assert.Equal("The other vector.", Catalog.GetMethodParameterDocumentation(add, 0));
        Assert.Equal("The sum.", Catalog.GetMethodReturnDocumentation(add, 0));
        Assert.Null(Catalog.GetMethodDocumentation(MethodOf(Vector2, "Area")));
        Assert.Null(Catalog.GetMethodParameterDocumentation(add, 5));
    }

    [Fact]
    public void EnumNamesListTheEnumMembers()
    {
        Assert.Equal(["Low", "High", "Big"], Catalog.GetEnumNames(Level));
        Assert.Empty(Catalog.GetEnumNames(Circle));
    }

    [Fact]
    public void OverridableMethodsAreTheVirtualAbstractAndOverrideOnesWithoutTheOverriddenBase()
    {
        string[] Overridable(TypeSpecifier type) =>
            [.. Catalog.GetOverridableMethodsForType(type).Select(method => $"{method.DeclaringType.ShortName}.{method.Name}").Order(StringComparer.Ordinal)];

        Assert.Equal(["Circle.Area", "ShapeBase.Describe"], Overridable(Circle));
        Assert.Equal(["ShapeBase.Area", "ShapeBase.Describe"], Overridable(ShapeBase));
        Assert.Equal(["IShape.Area"], Overridable(Shape));
        Assert.Equal(["Vector2.Equals", "Vector2.GetHashCode", "Vector2.ToString"], Overridable(Vector2));
    }

    [Fact]
    public void OnlyCatalogedTypesAreOffered()
    {
        Assert.Contains(Circle, Catalog.GetNonStaticTypes());
        Assert.Contains(new TypeSpecifier("Fixture.Utilities.Helpers"), Catalog.GetNonStaticTypes());
        Assert.DoesNotContain(new TypeSpecifier("System.String"), Catalog.GetNonStaticTypes());
        Assert.Empty(Catalog.GetConstructors(new TypeSpecifier("System.String")));
    }

    [Fact]
    public void LoadJsonAndLoadFileAnswerLikeLoad()
    {
        string json = CanonicalCatalogWriter.Write(Document);
        string path = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.npcat.json");
        File.WriteAllText(path, json);
        try
        {
            foreach (ITypeCatalog loaded in new[] { CatalogLoader.LoadJson(json), CatalogLoader.LoadFile(path) })
            {
                Assert.Equal(Catalog.Info.Id, loaded.Info.Id);
                Assert.Equal(Catalog.Info.Version, loaded.Info.Version);
                Assert.Equal(Catalog.Info.CoveredAssemblyNames, loaded.Info.CoveredAssemblyNames);
                Assert.Equal(Catalog.GetNonStaticTypes(), loaded.GetNonStaticTypes());
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TwoCatalogsWithOneIdKeepTheFirstAndLogNpc103()
    {
        ITypeCatalog first = CatalogLoader.Load(Document);
        ITypeCatalog duplicate = CatalogLoader.Load(Document with { Version = "9.9.9.9" });
        ITypeCatalog other = CatalogLoader.Load(Document with { Id = "another" });
        var logger = new CollectingLogger();

        IReadOnlyList<ITypeCatalog> kept = CatalogLoader.FirstOfEachId([first, duplicate, other], logger);

        Assert.Equal([first, other], kept);
        LogEntry warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("NPC103", warning.Message, StringComparison.Ordinal);
        Assert.Contains(FixtureCatalog.FixtureId, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DistinctIdsLogNothing()
    {
        var logger = new CollectingLogger();

        IReadOnlyList<ITypeCatalog> kept = CatalogLoader.FirstOfEachId([CatalogLoader.Load(Document), CatalogLoader.Load(Document with { Id = "another" })], logger);

        Assert.Equal(2, kept.Count);
        Assert.Empty(logger.Entries);
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CollectingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
