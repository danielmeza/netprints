using System.Collections.Frozen;
using NetPrints.Core;
using NetPrints.Reflection;

namespace NetPrints.Editor.Graph.Pins;

/// <summary>
/// The colour group of a pin (FR-107). A family has one style class, <c>pin-&lt;name&gt;</c>, and one
/// <c>Pin.&lt;Name&gt;</c> theme token in each variant.
/// </summary>
public sealed class PinTypeFamily
{
    private static readonly TypeSpecifier NullableType = TypeSpecifier.FromType(typeof(int?).GetGenericTypeDefinition());

    private static readonly TypeSpecifier MulticastDelegateType = TypeSpecifier.FromType<MulticastDelegate>();

    private static readonly TypeSpecifier ValueTypeType = TypeSpecifier.FromType(typeof(System.ValueType));

    private static readonly TypeSpecifier ObjectType = TypeSpecifier.FromType<object>();

    private PinTypeFamily(string name)
    {
        Name = name;
        StyleClass = "pin-" + name.ToLowerInvariant();
        TokenKey = "Pin." + name;
    }

    /// <summary>Execution pins.</summary>
    public static PinTypeFamily Exec { get; } = new("Exec");

    /// <summary>Type pins.</summary>
    public static PinTypeFamily Type { get; } = new("Type");

    /// <summary><see cref="bool"/>.</summary>
    public static PinTypeFamily Bool { get; } = new("Bool");

    /// <summary>The integral types, <see cref="nint"/> and <see cref="nuint"/> included.</summary>
    public static PinTypeFamily Integer { get; } = new("Integer");

    /// <summary><see cref="float"/>, <see cref="double"/> and <see cref="decimal"/>.</summary>
    public static PinTypeFamily Float { get; } = new("Float");

    /// <summary><see cref="string"/> and <see cref="char"/>.</summary>
    public static PinTypeFamily String { get; } = new("String");

    /// <summary>Other reference types: classes, interfaces, arrays and <see cref="object"/>.</summary>
    public static PinTypeFamily Object { get; } = new("Object");

    /// <summary>Other value types: structs and enums.</summary>
    public static PinTypeFamily ValueType { get; } = new("ValueType");

    /// <summary>Delegate types.</summary>
    public static PinTypeFamily Delegate { get; } = new("Delegate");

    /// <summary>Generic parameters and types the reflection provider cannot resolve.</summary>
    public static PinTypeFamily Generic { get; } = new("Generic");

    /// <summary>Every family, in the order of the spec.</summary>
    public static IReadOnlyList<PinTypeFamily> All { get; } = [Exec, Type, Bool, Integer, Float, String, Object, ValueType, Delegate, Generic];

    private static FrozenDictionary<string, PinTypeFamily> BuiltInTypes { get; } = new Dictionary<string, PinTypeFamily>
    {
        [TypeSpecifier.FromType<bool>().Name] = Bool,
        [TypeSpecifier.FromType<sbyte>().Name] = Integer,
        [TypeSpecifier.FromType<byte>().Name] = Integer,
        [TypeSpecifier.FromType<short>().Name] = Integer,
        [TypeSpecifier.FromType<ushort>().Name] = Integer,
        [TypeSpecifier.FromType<int>().Name] = Integer,
        [TypeSpecifier.FromType<uint>().Name] = Integer,
        [TypeSpecifier.FromType<long>().Name] = Integer,
        [TypeSpecifier.FromType<ulong>().Name] = Integer,
        [TypeSpecifier.FromType<nint>().Name] = Integer,
        [TypeSpecifier.FromType<nuint>().Name] = Integer,
        [TypeSpecifier.FromType<float>().Name] = Float,
        [TypeSpecifier.FromType<double>().Name] = Float,
        [TypeSpecifier.FromType<decimal>().Name] = Float,
        [TypeSpecifier.FromType<string>().Name] = String,
        [TypeSpecifier.FromType<char>().Name] = String,
    }.ToFrozenDictionary();

    /// <summary>The family's name, which is also the suffix of its <c>Pin.&lt;Name&gt;</c> token.</summary>
    public string Name { get; }

    /// <summary>The style class that selects the family's colour.</summary>
    public string StyleClass { get; }

    /// <summary>The key of the family's theme token.</summary>
    public string TokenKey { get; }

    /// <summary>Gets the family of a pin.</summary>
    /// <param name="kind">The pin's kind; execution and type pins have a family of their own.</param>
    /// <param name="type">The data pin's type, or <see langword="null"/> while it has none.</param>
    /// <param name="provider">The reflection provider that classifies types the built-in names do not cover, or <see langword="null"/> while none is loaded.</param>
    /// <returns>The family: <see cref="Generic"/> for a generic parameter and for a type that cannot be resolved.</returns>
    public static PinTypeFamily Of(PinKind kind, BaseType? type, IReflectionProvider? provider)
    {
        if (kind == PinKind.Exec)
        {
            return Exec;
        }

        if (kind == PinKind.Type)
        {
            return Type;
        }

        if (type is not TypeSpecifier specifier)
        {
            return Generic;
        }

        if (specifier.Name == NullableType.Name && specifier.GenericArguments.Count == 1)
        {
            return Of(kind, specifier.GenericArguments[0], provider);
        }

        if (BuiltInTypes.TryGetValue(specifier.Name, out PinTypeFamily? builtIn))
        {
            return builtIn;
        }

        return provider is null ? Generic : Resolve(specifier, provider);
    }

    private static PinTypeFamily Resolve(TypeSpecifier specifier, IReflectionProvider provider)
    {
        try
        {
            if (provider.TypeSpecifierIsSubclassOf(specifier, MulticastDelegateType))
            {
                return Delegate;
            }

            if (specifier.IsEnum || provider.TypeSpecifierIsSubclassOf(specifier, ValueTypeType))
            {
                return ValueType;
            }

            bool resolved = specifier.IsInterface
                ? provider.TypeSpecifierIsSubclassOf(specifier, specifier)
                : provider.TypeSpecifierIsSubclassOf(specifier, ObjectType);
            return resolved ? Object : Generic;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            return Generic;
        }
    }
}
