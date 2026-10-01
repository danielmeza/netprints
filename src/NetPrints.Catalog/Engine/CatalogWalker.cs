using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Catalog;

/// <summary>The state of one <c>CatalogBuilder.Build</c> call: walks namespaces and types and turns symbols into catalog records.</summary>
internal sealed class CatalogWalker(RoslynCompilation compilation, ICatalogFilter filter, IDocumentationSource documentation)
{
    private const string ArrayTypeName = "System.Array";

    private const string ObjectTypeName = "System.Object";

    private const string UnavailableAssembly = "an unavailable assembly";

    private const string StaticModifier = "static";

    private const string AbstractModifier = "abstract";

    private const string SealedModifier = "sealed";

    private const string ReadonlyModifier = "readonly";

    private readonly List<CatalogType> types = [];

    private readonly List<CatalogDiagnostic> diagnostics = [];

    private readonly HashSet<string> typeIds = new(StringComparer.Ordinal);

    public IReadOnlyList<CatalogType> Types => types;

    public IReadOnlyList<CatalogDiagnostic> SortedDiagnostics() =>
        [.. diagnostics.OrderBy(d => d.Source, StringComparer.Ordinal).ThenBy(d => d.Code, StringComparer.Ordinal).ThenBy(d => d.Message, StringComparer.Ordinal)];

    public void Visit(INamespaceSymbol container)
    {
        foreach (INamespaceSymbol child in container.GetNamespaceMembers())
        {
            Visit(child);
        }

        foreach (INamedTypeSymbol type in container.GetTypeMembers())
        {
            VisitType(type, containerIncluded: true);
        }
    }

    private static string RenderedName(CatalogTypeRef reference) =>
        reference.Args is { Count: > 0 } args ? $"{reference.Name}<{string.Join(",", args.Select(RenderedName))}>" : reference.Name;

    private static string? MissingAssembly(ITypeSymbol type)
    {
        switch (type)
        {
            case IErrorTypeSymbol error:
                return error.ContainingAssembly?.Identity.Name ?? UnavailableAssembly;
            case IArrayTypeSymbol array:
                return MissingAssembly(array.ElementType);
            case IPointerTypeSymbol pointer:
                return MissingAssembly(pointer.PointedAtType);
            case INamedTypeSymbol named:
                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    if (MissingAssembly(argument) is { } missing)
                    {
                        return missing;
                    }
                }

                return named.ContainingType is { } outer ? MissingAssembly(outer) : null;
            default:
                return null;
        }
    }

    private static bool IsUnsupported(ITypeSymbol type) => type switch
    {
        IPointerTypeSymbol or IFunctionPointerTypeSymbol => true,
        IArrayTypeSymbol array => IsUnsupported(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(IsUnsupported),
        _ => false,
    };

    private static string TypeName(INamedTypeSymbol type)
    {
        List<string> names = [type.Name];
        for (INamedTypeSymbol? outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            names.Insert(0, outer.Name);
        }

        string name = string.Join("+", names);
        return type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() + "." + name : name;
    }

    private static CatalogTypeKind? KindOf(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Class => CatalogTypeKind.Class,
        TypeKind.Struct => CatalogTypeKind.Struct,
        TypeKind.Interface => CatalogTypeKind.Interface,
        TypeKind.Enum => CatalogTypeKind.Enum,
        TypeKind.Delegate => CatalogTypeKind.Delegate,
        _ => null,
    };

    private static IReadOnlyList<string>? NullIfEmpty(List<string> values) => values.Count == 0 ? null : values;

    private static IReadOnlyList<string>? GenericParameterNames(ImmutableArray<ITypeParameterSymbol> parameters) =>
        parameters.IsEmpty ? null : [.. parameters.Select(parameter => parameter.Name)];

    private static IReadOnlyList<string>? TypeModifiers(INamedTypeSymbol type)
    {
        List<string> modifiers = [];
        if (type.TypeKind == TypeKind.Class)
        {
            if (type.IsStatic)
            {
                modifiers.Add(StaticModifier);
            }
            else if (type.IsAbstract)
            {
                modifiers.Add(AbstractModifier);
            }
            else if (type.IsSealed)
            {
                modifiers.Add(SealedModifier);
            }
        }

        return NullIfEmpty(modifiers);
    }

    private static CatalogPassType? PassTypeOf(RefKind kind) => kind switch
    {
        RefKind.Ref => CatalogPassType.Reference,
        RefKind.Out => CatalogPassType.Out,
        RefKind.In or RefKind.RefReadOnlyParameter => CatalogPassType.In,
        _ => null,
    };

    private void Report(string code, string message, string source) =>
        diagnostics.Add(new CatalogDiagnostic(code, CatalogDiagnosticSeverity.Warning, message, source));

    private void VisitType(INamedTypeSymbol type, bool containerIncluded)
    {
        if (KindOf(type) is not { } kind || type.Name.IndexOf("<", StringComparison.Ordinal) >= 0)
        {
            return;
        }

        bool exposed = Exposure.IsExposedType(type);
        WarnAboutIgnoredAnnotations(type, exposed);

        bool included = containerIncluded && exposed && filter.IncludeType(type) && TryBuildType(type, kind);
        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            VisitType(nested, included);
        }
    }

    private void WarnAboutIgnoredAnnotations(INamedTypeSymbol type, bool exposed)
    {
        if (!exposed && SymbolAttributes.Has(type, SymbolAttributes.TypeAttribute))
        {
            Report(CatalogDiagnosticCodes.IgnoredAnnotation, $"[NetPrintsType] on '{type.ToDisplayString()}' is ignored because the type is not public.", SymbolIds.Of(type));
        }

        foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
        {
            if (SymbolAttributes.Has(method, SymbolAttributes.NodeAttribute) && (!exposed || !Exposure.IsCatalogMember(method)))
            {
                Report(CatalogDiagnosticCodes.IgnoredAnnotation, $"[NetPrintsNode] on '{method.ToDisplayString()}' is ignored because the method is not public.", SymbolIds.Of(method));
            }
        }
    }

    private bool TryBuildType(INamedTypeSymbol type, CatalogTypeKind kind)
    {
        string id = SymbolIds.Of(type);
        if (typeIds.Contains(id))
        {
            return false;
        }

        List<INamedTypeSymbol> relatives = [.. type.AllInterfaces];
        if (type.BaseType is { } baseSymbol)
        {
            relatives.Insert(0, baseSymbol);
        }

        foreach (INamedTypeSymbol relative in relatives)
        {
            if (MissingAssembly(relative) is { } missing)
            {
                Report(CatalogDiagnosticCodes.SkippedMemberMissingAssembly, $"'{type.ToDisplayString()}' is not cataloged: it derives from '{relative.ToDisplayString()}' of assembly '{missing}', which is not available.", id);
                return false;
            }
        }

        typeIds.Add(id);
        List<CatalogConstructor> constructors = [];
        List<CatalogMethod> methods = [];
        List<CatalogVariable> variables = [];
        if (kind is CatalogTypeKind.Class or CatalogTypeKind.Struct or CatalogTypeKind.Interface)
        {
            foreach (ISymbol member in type.GetMembers().Where(Exposure.IsCatalogMember).Where(filter.IncludeMember))
            {
                if (MemberMissingAssembly(member) is { } missing)
                {
                    Report(CatalogDiagnosticCodes.SkippedMemberMissingAssembly, $"'{member.ToDisplayString()}' is not cataloged: it uses a type of assembly '{missing}', which is not available.", SymbolIds.Of(member));
                    continue;
                }

                AddMember(member, constructors, methods, variables);
            }
        }

        types.Add(new CatalogType
        {
            Id = id,
            Namespace = type.ContainingNamespace is { IsGlobalNamespace: false } containing ? containing.ToDisplayString() : null,
            Name = type.Name,
            Kind = kind,
            Modifiers = TypeModifiers(type),
            GenericParameters = GenericParameterNames(type.TypeParameters),
            DeclaringType = type.ContainingType is { } outer ? SymbolIds.Of(outer) : null,
            BaseType = BaseTypeOf(type),
            Interfaces = type.AllInterfaces.Length == 0 ? null : [.. type.AllInterfaces.Select(TypeRef).OrderBy(RenderedName, StringComparer.Ordinal)],
            EnumMembers = kind == CatalogTypeKind.Enum ? EnumMemberNames(type) : null,
            Summary = documentation.GetSummary(id),
            Node = filter.DescribeNode(type),
            Constructors = constructors.Count == 0 ? null : [.. constructors.OrderBy(c => c.Id, StringComparer.Ordinal)],
            Methods = methods.Count == 0 ? null : [.. methods.OrderBy(m => m.Id, StringComparer.Ordinal)],
            Variables = variables.Count == 0 ? null : [.. variables.OrderBy(v => v.Id, StringComparer.Ordinal)],
        });
        return true;
    }

    private CatalogTypeRef? BaseTypeOf(INamedTypeSymbol type) =>
        type.BaseType is { } baseType && !SymbolEqualityComparer.Default.Equals(baseType, compilation.ObjectType) ? TypeRef(baseType) : null;

    private static IReadOnlyList<string>? EnumMemberNames(INamedTypeSymbol type)
    {
        List<string> names = [.. type.GetMembers().OfType<IFieldSymbol>().Where(field => field.IsConst).Select(field => field.Name)];
        return NullIfEmpty(names);
    }

    private static string? MemberMissingAssembly(ISymbol member)
    {
        IEnumerable<ITypeSymbol> used = member switch
        {
            IMethodSymbol method => method.Parameters.Select(p => p.Type).Concat(method.ReturnsVoid ? [] : [method.ReturnType]),
            IPropertySymbol property => [property.Type],
            IFieldSymbol field => [field.Type],
            _ => [],
        };

        foreach (ITypeSymbol type in used)
        {
            if (MissingAssembly(type) is { } missing)
            {
                return missing;
            }
        }

        return null;
    }

    private static bool UsesUnsupportedType(ISymbol member) => member switch
    {
        IMethodSymbol method => method.Parameters.Any(p => IsUnsupported(p.Type)) || (!method.ReturnsVoid && IsUnsupported(method.ReturnType)),
        IPropertySymbol property => IsUnsupported(property.Type),
        IFieldSymbol field => IsUnsupported(field.Type),
        _ => false,
    };

    private void AddMember(ISymbol member, List<CatalogConstructor> constructors, List<CatalogMethod> methods, List<CatalogVariable> variables)
    {
        if (UsesUnsupportedType(member))
        {
            return;
        }

        switch (member)
        {
            case IMethodSymbol { MethodKind: MethodKind.Constructor } constructor:
                constructors.Add(BuildConstructor(constructor));
                break;
            case IMethodSymbol method:
                methods.Add(BuildMethod(method));
                break;
            case IPropertySymbol property when BuildProperty(property) is { } built:
                variables.Add(built);
                break;
            case IFieldSymbol field:
                variables.Add(BuildField(field));
                break;
        }
    }

    private CatalogConstructor BuildConstructor(IMethodSymbol constructor)
    {
        string id = SymbolIds.Of(constructor);
        return new CatalogConstructor
        {
            Id = id,
            Visibility = Exposure.VisibilityOf(constructor.DeclaredAccessibility) ?? CatalogVisibility.Public,
            Parameters = Parameters(constructor, id),
            Summary = documentation.GetSummary(id),
        };
    }

    private CatalogMethod BuildMethod(IMethodSymbol method)
    {
        string id = SymbolIds.Of(method);
        return new CatalogMethod
        {
            Id = id,
            Name = method.Name,
            Visibility = Exposure.VisibilityOf(method.DeclaredAccessibility) ?? CatalogVisibility.Public,
            Modifiers = MethodModifiers(method),
            GenericParameters = GenericParameterNames(method.TypeParameters),
            Parameters = Parameters(method, id),
            ReturnType = method.ReturnsVoid ? null : TypeRef(method.ReturnType),
            ReturnSummary = documentation.GetReturns(id),
            Summary = documentation.GetSummary(id),
            Obsolete = SymbolAttributes.ObsoleteOf(method),
            Node = filter.DescribeNode(method),
        };
    }

    private static IReadOnlyList<string>? MethodModifiers(IMethodSymbol method)
    {
        List<string> modifiers = [];
        if (method.IsStatic)
        {
            modifiers.Add(StaticModifier);
        }

        if (method.IsAbstract)
        {
            modifiers.Add(AbstractModifier);
        }

        if (method.IsVirtual)
        {
            modifiers.Add("virtual");
        }

        if (method.IsOverride)
        {
            modifiers.Add("override");
        }

        if (method.IsSealed && method.IsOverride)
        {
            modifiers.Add(SealedModifier);
        }

        if (method.IsExtensionMethod)
        {
            modifiers.Add("extension");
        }

        if (method.MethodKind is MethodKind.UserDefinedOperator or MethodKind.Conversion)
        {
            modifiers.Add("operator");
        }

        return NullIfEmpty(modifiers);
    }

    private IReadOnlyList<CatalogParameter>? Parameters(IMethodSymbol method, string methodId) =>
        method.Parameters.Length == 0
            ? null
            : [.. method.Parameters.Select(parameter => new CatalogParameter
            {
                Name = parameter.Name,
                Type = TypeRef(parameter.Type),
                PassType = PassTypeOf(parameter.RefKind),
                Params = parameter.IsParams,
                Default = DefaultOf(parameter),
                Summary = documentation.GetParameter(methodId, parameter.Name),
            })];

    private CatalogTypedValue? DefaultOf(IParameterSymbol parameter)
    {
        if (!parameter.HasExplicitDefaultValue)
        {
            return null;
        }

        object? value = parameter.ExplicitDefaultValue;
        return value is null
            ? new CatalogTypedValue { Type = TypeRef(parameter.Type).Name }
            : new CatalogTypedValue { Type = value.GetType().FullName ?? value.GetType().Name, Value = SymbolAttributes.Scalar(value) };
    }

    private CatalogVariable? BuildProperty(IPropertySymbol property)
    {
        CatalogVisibility? get = property.GetMethod is { } getter ? Exposure.VisibilityOf(getter.DeclaredAccessibility) : null;
        CatalogVisibility? set = property.SetMethod is { } setter ? Exposure.VisibilityOf(setter.DeclaredAccessibility) : null;
        if (get is null && set is null)
        {
            return null;
        }

        List<string> modifiers = [];
        if (property.IsStatic)
        {
            modifiers.Add(StaticModifier);
        }

        if (property.IsReadOnly)
        {
            modifiers.Add(ReadonlyModifier);
        }

        string id = SymbolIds.Of(property);
        return new CatalogVariable
        {
            Id = id,
            Name = property.Name,
            Kind = CatalogVariableKind.Property,
            Type = TypeRef(property.Type),
            Modifiers = NullIfEmpty(modifiers),
            Get = get,
            Set = set,
            Summary = documentation.GetSummary(id),
        };
    }

    private CatalogVariable BuildField(IFieldSymbol field)
    {
        List<string> modifiers = [];
        if (field.IsStatic)
        {
            modifiers.Add(StaticModifier);
        }

        if (field.IsReadOnly)
        {
            modifiers.Add(ReadonlyModifier);
        }

        if (field.IsConst)
        {
            modifiers.Add("const");
        }

        CatalogVisibility visibility = Exposure.VisibilityOf(field.DeclaredAccessibility) ?? CatalogVisibility.Public;
        string id = SymbolIds.Of(field);
        return new CatalogVariable
        {
            Id = id,
            Name = field.Name,
            Kind = CatalogVariableKind.Field,
            Type = TypeRef(field.Type),
            Modifiers = NullIfEmpty(modifiers),
            Get = visibility,
            Set = field.IsReadOnly || field.IsConst ? null : visibility,
            Summary = documentation.GetSummary(id),
        };
    }

    private CatalogTypeRef TypeRef(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return new CatalogTypeRef { Name = parameter.Name, Generic = true };
            case IArrayTypeSymbol:
                return new CatalogTypeRef { Name = ArrayTypeName };
            case IDynamicTypeSymbol:
                return new CatalogTypeRef { Name = ObjectTypeName };
            case INamedTypeSymbol named:
                return new CatalogTypeRef
                {
                    Name = TypeName(named),
                    IsEnum = named.TypeKind == TypeKind.Enum,
                    IsInterface = named.TypeKind == TypeKind.Interface,
                    Args = named.TypeArguments.Length == 0 ? null : [.. named.TypeArguments.Select(TypeRef)],
                };
            default:
                return new CatalogTypeRef { Name = type.ToDisplayString() };
        }
    }
}
