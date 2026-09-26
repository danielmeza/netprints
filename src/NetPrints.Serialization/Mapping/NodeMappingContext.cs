#nullable enable
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Converts model types (<see cref="BaseType"/>, <see cref="MethodSpecifier"/>,
/// <see cref="ConstructorSpecifier"/>, <see cref="VariableSpecifier"/>, and typed values) to and from
/// their document reference DTOs (document-format.md §1.6, §2.6). Shared by every
/// <c>INodeDocumentConverter</c> so the reference shape stays consistent across node kinds.
/// </summary>
public sealed class NodeMappingContext
{
    /// <summary>
    /// Creates a mapping context for one class.
    /// </summary>
    /// <param name="cls">Class the nodes being mapped belong to.</param>
    public NodeMappingContext(ClassGraph cls)
    {
        Class = cls;
    }

    /// <summary>
    /// The class the nodes being mapped belong to.
    /// </summary>
    public ClassGraph Class { get; }

    /// <summary>
    /// Converts <paramref name="type"/> to a <see cref="TypeRef"/>: an unbound
    /// <see cref="GenericType"/> becomes <c>Generic = true</c>; a <see cref="TypeSpecifier"/> carries
    /// its <see cref="TypeSpecifier.IsEnum"/>/<see cref="TypeSpecifier.IsInterface"/> flags and generic
    /// arguments.
    /// </summary>
    /// <param name="type">Type to convert.</param>
    /// <returns>The converted type reference.</returns>
    public TypeRef ToRef(BaseType type)
    {
        if (type is GenericType generic)
        {
            return new TypeRef(generic.Name, Generic: true);
        }

        var specifier = (TypeSpecifier)type;
        IReadOnlyList<TypeRef>? args = specifier.GenericArguments.Count > 0
            ? specifier.GenericArguments.Select(ToRef).ToList()
            : null;

        return new TypeRef(specifier.Name, Generic: false, specifier.IsEnum, specifier.IsInterface, args);
    }

    /// <summary>
    /// Converts <paramref name="type"/> back to a <see cref="BaseType"/>: the inverse of <see cref="ToRef(BaseType)"/>.
    /// </summary>
    /// <param name="type">Type reference to convert.</param>
    /// <returns>The converted type.</returns>
    public BaseType FromRef(TypeRef type)
    {
        if (type.Generic)
        {
            return new GenericType(type.Name);
        }

        IEnumerable<BaseType>? args = type.Args?.Select(FromRef);
        return new TypeSpecifier(type.Name, type.IsEnum, type.IsInterface, args);
    }

    /// <summary>
    /// Converts <paramref name="method"/> to a <see cref="MethodRef"/>.
    /// </summary>
    /// <param name="method">Method to convert.</param>
    /// <returns>The converted method reference.</returns>
    public MethodRef ToRef(MethodSpecifier method)
    {
        IReadOnlyList<ParameterRef>? parameters = method.Parameters.Count > 0
            ? method.Parameters.Select(ToParameterRef).ToList()
            : null;
        IReadOnlyList<TypeRef>? returnTypes = method.ReturnTypes.Count > 0
            ? method.ReturnTypes.Select(ToRef).ToList()
            : null;
        IReadOnlyList<TypeRef>? genericArgs = method.GenericArguments.Count > 0
            ? method.GenericArguments.Select(ToRef).ToList()
            : null;

        return new MethodRef(method.Name, ToRef(method.DeclaringType), parameters, returnTypes,
            method.Modifiers, method.Visibility, genericArgs);
    }

    /// <summary>
    /// Converts <paramref name="method"/> back to a <see cref="MethodSpecifier"/>: the inverse of
    /// <see cref="ToRef(MethodSpecifier)"/>.
    /// </summary>
    /// <param name="method">Method reference to convert.</param>
    /// <returns>The converted method specifier.</returns>
    public MethodSpecifier FromRef(MethodRef method)
    {
        IEnumerable<MethodParameter> parameters = method.Parameters?.Select(FromParameterRef)
            ?? Enumerable.Empty<MethodParameter>();
        IEnumerable<BaseType> returnTypes = method.ReturnTypes?.Select(FromRef)
            ?? Enumerable.Empty<BaseType>();
        IList<BaseType> genericArgs = method.GenericArgs?.Select(FromRef).ToList() ?? new List<BaseType>();

        return new MethodSpecifier(method.Name, parameters, returnTypes, method.Modifiers,
            method.Visibility, (TypeSpecifier)FromRef(method.DeclaringType), genericArgs);
    }

    /// <summary>
    /// Converts <paramref name="constructor"/> to a <see cref="ConstructorRef"/>.
    /// </summary>
    /// <param name="constructor">Constructor to convert.</param>
    /// <returns>The converted constructor reference.</returns>
    public ConstructorRef ToRef(ConstructorSpecifier constructor)
    {
        IReadOnlyList<ParameterRef>? parameters = constructor.Arguments.Count > 0
            ? constructor.Arguments.Select(ToParameterRef).ToList()
            : null;

        return new ConstructorRef(ToRef(constructor.DeclaringType), parameters);
    }

    /// <summary>
    /// Converts <paramref name="constructor"/> back to a <see cref="ConstructorSpecifier"/>: the
    /// inverse of <see cref="ToRef(ConstructorSpecifier)"/>.
    /// </summary>
    /// <param name="constructor">Constructor reference to convert.</param>
    /// <returns>The converted constructor specifier.</returns>
    public ConstructorSpecifier FromRef(ConstructorRef constructor)
    {
        IEnumerable<MethodParameter> parameters = constructor.Parameters?.Select(FromParameterRef)
            ?? Enumerable.Empty<MethodParameter>();

        return new ConstructorSpecifier(parameters, (TypeSpecifier)FromRef(constructor.DeclaringType));
    }

    /// <summary>
    /// Converts <paramref name="variable"/> to a <see cref="VariableRef"/>. <see cref="VariableRef.Scope"/>
    /// is always <see cref="VariableScope.Member"/> (method-local variables are sub-phase H).
    /// </summary>
    /// <param name="variable">Variable to convert.</param>
    /// <returns>The converted variable reference.</returns>
    public VariableRef ToRef(VariableSpecifier variable)
    {
        return new VariableRef(variable.Name, ToRef(variable.Type), ToRef(variable.DeclaringType),
            variable.GetterVisibility, variable.SetterVisibility, variable.Visibility, variable.Modifiers,
            VariableScope.Member);
    }

    /// <summary>
    /// Converts <paramref name="variable"/> back to a <see cref="VariableSpecifier"/>: the inverse of
    /// <see cref="ToRef(VariableSpecifier)"/>.
    /// </summary>
    /// <param name="variable">Variable reference to convert.</param>
    /// <returns>The converted variable specifier.</returns>
    /// <exception cref="DocumentFormatException"><paramref name="variable"/> has no
    /// <see cref="VariableRef.DeclaringType"/> (only valid for a method-local variable, not yet
    /// supported).</exception>
    public VariableSpecifier FromRef(VariableRef variable)
    {
        if (variable.DeclaringType is null)
        {
            throw new DocumentFormatException($"Variable '{variable.Name}' has no declaring type.");
        }

        return new VariableSpecifier(variable.Name, (TypeSpecifier)FromRef(variable.Type),
            variable.GetterVisibility, variable.SetterVisibility, (TypeSpecifier)FromRef(variable.DeclaringType),
            variable.Modifiers);
    }

    /// <summary>
    /// Converts <paramref name="value"/> to a <see cref="TypedValue"/> (<see cref="TypedValueConverter.ToTypedValue"/>),
    /// or <see langword="null"/> if <paramref name="value"/> itself is <see langword="null"/> (no value
    /// to record).
    /// </summary>
    /// <param name="value">Value to convert, or <see langword="null"/>.</param>
    /// <param name="where">Location used in the exception message for an unsupported type.</param>
    /// <returns>The converted value, or <see langword="null"/>.</returns>
    /// <exception cref="DocumentFormatException"><paramref name="value"/>'s runtime type is unsupported.</exception>
    public TypedValue? ToValue(object? value, string where) => value is null ? null : TypedValueConverter.ToTypedValue(value, where);

    /// <summary>
    /// Converts <paramref name="value"/> back to a runtime value (<see cref="TypedValueConverter.FromTypedValue"/>),
    /// or <see langword="null"/> if <paramref name="value"/> itself is <see langword="null"/>.
    /// </summary>
    /// <param name="value">Value to convert, or <see langword="null"/>.</param>
    /// <returns>The converted value, or <see langword="null"/>.</returns>
    public object? FromValue(TypedValue? value) => value is null ? null : TypedValueConverter.FromTypedValue(value);

    private readonly HashSet<MethodGraph> claimedMainReturnNodes = new();

    /// <summary>
    /// Returns <paramref name="graph"/>'s already-existing <see cref="MethodGraph.MainReturnNode"/> the
    /// first time it is called for <paramref name="graph"/> in this context's lifetime, and
    /// <see langword="null"/> on every later call for the same graph. Used by the <c>return</c> node
    /// converter (<c>Mapping/BuiltIn/EntryReturnConverters.cs</c>) to tell the method graph's one
    /// constructor-created main return node (reconfigured in place) apart from any additional return
    /// node a document lists (created fresh; its pins replicate the main node's automatically).
    /// </summary>
    /// <param name="graph">Method graph to claim the main return node of.</param>
    /// <returns>The graph's main return node, or <see langword="null"/> if already claimed.</returns>
    internal ReturnNode? ClaimMainReturnNode(MethodGraph graph) => claimedMainReturnNodes.Add(graph) ? graph.MainReturnNode : null;

    private ParameterRef ToParameterRef(MethodParameter parameter)
    {
        TypedValue? defaultValue = null;
        if (parameter.HasExplicitDefaultValue)
        {
            defaultValue = parameter.ExplicitDefaultValue is null
                ? new TypedValue(ToRef(parameter.Value).Name, null)
                : ToValue(parameter.ExplicitDefaultValue, $"default value of parameter '{parameter.Name}'");
        }

        return new ParameterRef(parameter.Name, ToRef(parameter.Value), parameter.PassType, defaultValue);
    }

    private MethodParameter FromParameterRef(ParameterRef parameter)
    {
        bool hasDefault = parameter.Default is not null;
        object? defaultValue = hasDefault ? FromValue(parameter.Default) : null;
        return new MethodParameter(parameter.Name, FromRef(parameter.Type), parameter.PassType, hasDefault, defaultValue);
    }
}
