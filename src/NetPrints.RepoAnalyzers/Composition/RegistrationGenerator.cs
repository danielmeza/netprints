using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NetPrints.RepoAnalyzers.Generation;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>
/// Emits <c>AddEditorServices()</c> from <c>[Singleton&lt;T&gt;]</c> and <c>[SessionScoped&lt;T&gt;]</c> (ADR-0031): factory lambdas that call
/// constructors or a named static factory method, with no reflection and no assembly scanning, and the <c>Create</c> methods of
/// partial classes marked <c>[ScopeFactory]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class RegistrationGenerator : IIncrementalGenerator
{
    internal const string RegistrationsStep = "Registrations";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
            output.AddSource(CompositionSources.AttributesHintName, SourceText.From(CompositionSources.Attributes, Encoding.UTF8)));

        IncrementalValueProvider<EquatableArray<Registration>> singletons = Collect(context, CompositionSources.SingletonMetadataName, RegistrationLifetime.Singleton);
        IncrementalValueProvider<EquatableArray<Registration>> sessionScoped = Collect(context, CompositionSources.SessionScopedMetadataName, RegistrationLifetime.SessionScoped);
        IncrementalValueProvider<EquatableArray<Registration>> registrations = singletons
            .Combine(sessionScoped)
            .Select(static (pair, _) => Merge(pair.Left, pair.Right))
            .WithTrackingName(RegistrationsStep);

        context.RegisterSourceOutput(registrations, static (output, all) => EmitRegistrations(output, all));

        IncrementalValuesProvider<ScopeFactoryModel> factories = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                CompositionSources.ScopeFactoryMetadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attribute, token) => RegistrationReader.ReadScopeFactory(attribute, token))
            .SelectMany(static (found, _) => found);

        context.RegisterSourceOutput(factories.Combine(registrations), static (output, pair) => EmitScopeFactory(output, pair.Left, pair.Right));
    }

    private static IncrementalValueProvider<EquatableArray<Registration>> Collect(IncrementalGeneratorInitializationContext context, string metadataName, RegistrationLifetime lifetime) =>
        context.SyntaxProvider
            .ForAttributeWithMetadataName(
                metadataName,
                static (node, _) => node is ClassDeclarationSyntax,
                (attribute, token) => RegistrationReader.Read(attribute, lifetime, token))
            .SelectMany(static (found, _) => found.Items)
            .Collect()
            .Select(static (items, _) => new EquatableArray<Registration>(items.ToArray()));

    private static EquatableArray<Registration> Merge(EquatableArray<Registration> left, EquatableArray<Registration> right) =>
        new([.. left.Concat(right).OrderBy(registration => registration.Service, StringComparer.Ordinal).ThenBy(registration => registration.Implementation, StringComparer.Ordinal).ThenBy(registration => registration.Location.FilePath, StringComparer.Ordinal).ThenBy(registration => registration.Location.Span.Start)]);

    private static void EmitRegistrations(SourceProductionContext output, EquatableArray<Registration> all)
    {
        foreach (DiagnosticModel diagnostic in all.SelectMany(registration => registration.Diagnostics).Concat(RegistrationRules.Check(all.Items)))
        {
            Report(output, diagnostic);
        }

        string? source = RegistrationEmitter.Registrations(all.Items);
        if (source is not null)
        {
            output.AddSource(RegistrationEmitter.RegistrationHintName, SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void EmitScopeFactory(SourceProductionContext output, ScopeFactoryModel factory, EquatableArray<Registration> all)
    {
        List<FactoryMethod> unresolved = [];
        string? source = RegistrationEmitter.ScopeFactory(factory, all.Items, unresolved);
        foreach (FactoryMethod method in unresolved)
        {
            Report(output, new DiagnosticModel(NpiDiagnosticCodes.InvalidRegistration, method.Location, method.ReturnType, "it is neither registered nor a class with one constructor to call"));
        }

        if (source is not null)
        {
            output.AddSource(RegistrationEmitter.ScopeFactoryHintName(factory), SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void Report(SourceProductionContext output, DiagnosticModel diagnostic) =>
        output.ReportDiagnostic(Diagnostic.Create(NpiDiagnosticCodes.For(diagnostic.Id), diagnostic.Location.ToLocation(), diagnostic.First, diagnostic.Second));
}
