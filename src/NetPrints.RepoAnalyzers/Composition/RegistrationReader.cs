using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetPrints.RepoAnalyzers.Generation;
using NetPrints.Roslyn;

namespace NetPrints.RepoAnalyzers.Composition;

/// <summary>Reads service attributes and scope factories from symbols into pipeline models.</summary>
internal static class RegistrationReader
{
    private const string LoggingNamespace = "Microsoft.Extensions.Logging";

    private const string LoggerName = "ILogger";

    private const string ProviderType = "System.IServiceProvider";

    private const string ConstructorMarker = "ActivatorUtilitiesConstructorAttribute";

    private const string CreatePrefix = "Create";

    private const string NotImplemented = "it does not implement {0}";

    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    public static EquatableArray<Registration> Read(GeneratorAttributeSyntaxContext context, RegistrationLifetime lifetime, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol type)
        {
            return default;
        }

        List<Registration> registrations = [];
        foreach (AttributeData attribute in context.Attributes)
        {
            if (attribute.AttributeClass is { TypeArguments: { Length: 1 } arguments })
            {
                registrations.Add(Read(type, arguments[0], attribute, lifetime, context.SemanticModel.Compilation, token));
            }
        }

        return new EquatableArray<Registration>(registrations.ToArray());
    }

    public static ScopeFactoryModel[] ReadScopeFactory(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null, TypeParameters.Length: 0 } type)
        {
            return [];
        }

        List<FactoryMethod> methods = [];
        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (method.IsPartialDefinition && method.PartialImplementationPart is null && method.Name.StartsWith(CreatePrefix, StringComparison.Ordinal))
            {
                methods.Add(ReadMethod(method, token));
            }
        }

        string ns = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
        return [new ScopeFactoryModel(ns, type.Name, new EquatableArray<FactoryMethod>(methods.ToArray()))];
    }

    private static Registration Read(INamedTypeSymbol type, ITypeSymbol service, AttributeData attribute, RegistrationLifetime lifetime, Compilation compilation, CancellationToken token)
    {
        LocationInfo location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax(token).GetLocation());
        string implementation = Display(type);
        string serviceName = Display(service);
        string factory = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == CompositionSources.FactoryArgument).Value.Value as string ?? string.Empty;
        List<DiagnosticModel> diagnostics = [];
        Creation? creation = factory.Length == 0
            ? ReadConstructor(type, service, compilation, location, diagnostics)
            : ReadFactory(type, service, factory, compilation, location, diagnostics);

        return new Registration(serviceName, implementation, lifetime, IsDisposable(type), creation, location, new EquatableArray<DiagnosticModel>(diagnostics.ToArray()));
    }

    private static Creation? ReadConstructor(INamedTypeSymbol type, ITypeSymbol service, Compilation compilation, LocationInfo location, List<DiagnosticModel> diagnostics)
    {
        string implementation = Display(type);
        string? invalid = null;
        if (type.IsAbstract)
        {
            invalid = "the type is abstract";
        }
        else if (type.TypeParameters.Length > 0)
        {
            invalid = "the type is an open generic";
        }
        else if (!Converts(compilation, type, service))
        {
            invalid = string.Format(System.Globalization.CultureInfo.InvariantCulture, NotImplemented, Display(service));
        }

        if (invalid is not null)
        {
            diagnostics.Add(new DiagnosticModel(NpiDiagnosticCodes.InvalidRegistration, location, implementation, invalid));
            return null;
        }

        IReadOnlyList<IMethodSymbol> candidates = Constructors(type);
        IMethodSymbol? constructor = Pick(candidates);
        if (constructor is null)
        {
            string problem = candidates.Count == 0 ? "no accessible constructor" : "more than one accessible constructor";
            diagnostics.Add(new DiagnosticModel(NpiDiagnosticCodes.AmbiguousConstructor, location, implementation, problem));
            return null;
        }

        return new Creation(implementation, string.Empty, Dependencies(constructor, type));
    }

    private static Creation? ReadFactory(INamedTypeSymbol type, ITypeSymbol service, string factory, Compilation compilation, LocationInfo location, List<DiagnosticModel> diagnostics)
    {
        string implementation = Display(type);
        IMethodSymbol[] methods = [.. type.GetMembers(factory).OfType<IMethodSymbol>().Where(method => method.IsStatic && method.MethodKind == MethodKind.Ordinary && !method.IsGenericMethod)];
        string? invalid = null;
        if (type.TypeParameters.Length > 0)
        {
            invalid = "the type is an open generic";
        }
        else if (methods.Length != 1)
        {
            invalid = $"it has no unique static, non-generic method named {factory}";
        }
        else if (!Converts(compilation, methods[0].ReturnType, service))
        {
            invalid = $"{factory} returns {Display(methods[0].ReturnType)}, which is not a {Display(service)}";
        }

        if (invalid is not null)
        {
            diagnostics.Add(new DiagnosticModel(NpiDiagnosticCodes.InvalidRegistration, location, implementation, invalid));
            return null;
        }

        return new Creation(implementation, factory, Dependencies(methods[0], type));
    }

    private static FactoryMethod ReadMethod(IMethodSymbol method, CancellationToken token)
    {
        string modifiers = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(token) is MethodDeclarationSyntax syntax
            ? string.Join(" ", syntax.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.PartialKeyword)).Select(modifier => modifier.Text))
            : "public";
        MethodParameter[] parameters = [.. method.Parameters.Select(parameter => new MethodParameter(Display(parameter.Type), parameter.Name))];
        Creation? creation = null;
        if (method.ReturnType is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, TypeParameters.Length: 0 } returned)
        {
            IMethodSymbol? constructor = Pick(Constructors(returned));
            creation = constructor is null ? null : new Creation(Display(returned), string.Empty, Dependencies(constructor, returned));
        }

        return new FactoryMethod(modifiers, Display(method.ReturnType), method.Name, new EquatableArray<MethodParameter>(parameters), creation, LocationInfo.From(method.Locations.FirstOrDefault()));
    }

    private static IReadOnlyList<IMethodSymbol> Constructors(INamedTypeSymbol type) =>
        [.. type.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal)];

    private static IMethodSymbol? Pick(IReadOnlyList<IMethodSymbol> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        IMethodSymbol[] marked = [.. candidates.Where(candidate => candidate.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == ConstructorMarker))];
        return marked.Length == 1 ? marked[0] : null;
    }

    private static EquatableArray<Dependency> Dependencies(IMethodSymbol method, INamedTypeSymbol owner)
    {
        string category = owner.ToDisplayString();
        return new EquatableArray<Dependency>([.. method.Parameters.Select(parameter => Describe(parameter, category))]);
    }

    private static Dependency Describe(IParameterSymbol parameter, string category)
    {
        ITypeSymbol type = parameter.Type;
        string name = Display(type);
        if (parameter.HasExplicitDefaultValue)
        {
            return new Dependency(DependencyKind.Optional, name, Literal(parameter));
        }

        if (type is INamedTypeSymbol named)
        {
            if (named.ToDisplayString() == ProviderType)
            {
                return new Dependency(DependencyKind.Provider, name, string.Empty);
            }

            if (named.Name == LoggerName && named.ContainingNamespace.ToDisplayString() == LoggingNamespace)
            {
                return named.TypeArguments.Length == 1
                    ? new Dependency(DependencyKind.Logger, Display(named.TypeArguments[0]), string.Empty)
                    : new Dependency(DependencyKind.CategoryLogger, name, category);
            }

            if (named.IsGenericType && named.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return new Dependency(DependencyKind.Enumerable, Display(named.TypeArguments[0]), string.Empty);
            }
        }

        return new Dependency(DependencyKind.Service, name, string.Empty);
    }

    private static string Literal(IParameterSymbol parameter)
    {
        object? value = parameter.ExplicitDefaultValue;
        if (value is null)
        {
            return "default";
        }

        string? text = SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false);
        if (text is null)
        {
            return "default";
        }

        return parameter.Type.TypeKind == TypeKind.Enum ? "(" + Display(parameter.Type) + ")" + text : text;
    }

    private static bool IsDisposable(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(contract => contract.ToDisplayString() is "System.IDisposable" or "System.IAsyncDisposable");

    private static bool Converts(Compilation compilation, ITypeSymbol from, ITypeSymbol to) =>
        compilation.ClassifyConversion(from, to) is { Exists: true, IsImplicit: true };

    private static string Display(ITypeSymbol type) => type.ToDisplayString(Qualified);
}
