using System.Collections.Frozen;
using System.Text;
using NetPrints.Core;

namespace NetPrints.Editor.Search;

/// <summary>
/// Writes a method or constructor the way C# declares it, for the override and overload pickers (FR-093):
/// return type, name, then the parameters, with type names without namespaces and C# keywords for built-in types.
/// The text does not depend on the current culture. Node search keeps <see cref="SuggestionItem.FormatMethod"/>.
/// </summary>
public static class MethodSignatureFormatter
{
    private const string ArraySuffix = "[]";
    private const string NullableName = "System.Nullable";
    private const string VoidKeyword = "void";

    private static readonly FrozenDictionary<string, string> Keywords = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Decimal"] = "decimal",
        ["System.Double"] = "double",
        ["System.Single"] = "float",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Object"] = "object",
        ["System.String"] = "string",
        ["System.Void"] = VoidKeyword,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Formats a method as <c>returnType Name&lt;T&gt;(parameters)</c>.</summary>
    /// <param name="method">The method to format.</param>
    /// <returns>The signature; a method with no return type shows <c>void</c>, with several it shows a tuple.</returns>
    public static string Format(MethodSpecifier method)
    {
        ArgumentNullException.ThrowIfNull(method);
        var text = new StringBuilder();
        text.Append(ReturnText(method.ReturnTypes)).Append(' ').Append(method.Name);
        if (method.GenericArguments.Count > 0)
        {
            text.Append('<').AppendJoin(", ", method.GenericArguments.Select(TypeName)).Append('>');
        }

        return AppendParameters(text, method.Parameters).ToString();
    }

    /// <summary>Formats a constructor as <c>TypeName(parameters)</c>.</summary>
    /// <param name="constructor">The constructor to format.</param>
    /// <returns>The signature.</returns>
    public static string Format(ConstructorSpecifier constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return AppendParameters(new StringBuilder(TypeName(constructor.DeclaringType)), constructor.Arguments).ToString();
    }

    /// <summary>Gets the short name of the type that declares <paramref name="method"/>, for a group header.</summary>
    /// <param name="method">The method.</param>
    /// <returns>The declaring type's name without its namespace.</returns>
    public static string DeclaringTypeName(MethodSpecifier method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return TypeName(method.DeclaringType);
    }

    /// <summary>Gets the short name of the type that declares <paramref name="constructor"/>, for a group header.</summary>
    /// <param name="constructor">The constructor.</param>
    /// <returns>The declaring type's name without its namespace.</returns>
    public static string DeclaringTypeName(ConstructorSpecifier constructor)
    {
        ArgumentNullException.ThrowIfNull(constructor);
        return TypeName(constructor.DeclaringType);
    }

    private static string ReturnText(IList<BaseType> returnTypes) => returnTypes.Count switch
    {
        0 => VoidKeyword,
        1 => TypeName(returnTypes[0]),
        _ => $"({string.Join(", ", returnTypes.Select(TypeName))})",
    };

    private static StringBuilder AppendParameters(StringBuilder text, IEnumerable<MethodParameter> parameters)
    {
        text.Append('(');
        bool first = true;
        foreach (MethodParameter parameter in parameters)
        {
            if (!first)
            {
                text.Append(", ");
            }

            first = false;
            text.Append(Modifier(parameter)).Append(TypeName(parameter.Value)).Append(' ').Append(parameter.Name);
        }

        return text.Append(')');
    }

    private static string Modifier(MethodParameter parameter) => parameter.PassType switch
    {
        MethodParameterPassType.Reference => "ref ",
        MethodParameterPassType.Out => "out ",
        MethodParameterPassType.In => "in ",
        _ => parameter.IsParams ? "params " : "",
    };

    private static string TypeName(BaseType type)
    {
        if (type is not TypeSpecifier specifier)
        {
            return type.ShortName;
        }

        string name = specifier.Name;
        int arrayRanks = 0;
        while (name.EndsWith(ArraySuffix, StringComparison.Ordinal))
        {
            name = name[..^ArraySuffix.Length];
            arrayRanks++;
        }

        string text;
        if (specifier.GenericArguments.Count == 1 && string.Equals(name, NullableName, StringComparison.Ordinal))
        {
            text = TypeName(specifier.GenericArguments[0]) + "?";
        }
        else
        {
            text = Keywords.TryGetValue(name, out string? keyword) ? keyword : WithoutNamespace(name);
            if (specifier.GenericArguments.Count > 0)
            {
                text += "<" + string.Join(", ", specifier.GenericArguments.Select(TypeName)) + ">";
            }
        }

        for (int rank = 0; rank < arrayRanks; rank++)
        {
            text += ArraySuffix;
        }

        return text;
    }

    private static string WithoutNamespace(string name)
    {
        int nestedAt = name.IndexOf('+', StringComparison.Ordinal);
        int namespaceEnd = (nestedAt < 0 ? name : name[..nestedAt]).LastIndexOf('.');
        return name[(namespaceEnd + 1)..].Replace('+', '.');
    }
}
