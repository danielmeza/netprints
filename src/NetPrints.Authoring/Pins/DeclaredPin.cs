using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NetPrints.Authoring.Pins;

/// <summary>One pin a node class declares, with the property when the convention declared it.</summary>
/// <param name="Kind">The collection the pin belongs to.</param>
/// <param name="Key">The pin key.</param>
/// <param name="Property">The pin property, or null for a pin the constructor adds.</param>
internal sealed record DeclaredPin(PinKind Kind, string Key, IPropertySymbol? Property)
{
    /// <summary>Gets the pin as <c>&lt;in|out&gt;.&lt;exec|data|type&gt;.&lt;key&gt;</c>.</summary>
    public string Reference => Group(Kind) + "." + Key;

    /// <summary>Lists the pins a class declares, base classes first.</summary>
    /// <param name="type">The node class.</param>
    /// <returns>The pins in declaration order.</returns>
    public static List<DeclaredPin> Of(INamedTypeSymbol type)
    {
        List<DeclaredPin> pins = [];
        foreach (INamedTypeSymbol member in PinSymbols.Ancestors(type).Reverse().Concat([type]))
        {
            List<PinProperty> properties = [.. PinSymbols.PinProperties(member).Where(pin => pin.Symbol.IsPartialDefinition)];
            if (properties.Count > 0)
            {
                pins.AddRange(properties.Select(pin => new DeclaredPin(pin.Kind, PinSymbols.KeyOf(pin.Symbol), pin.Symbol)));
            }
            else
            {
                pins.AddRange(PinSymbols.DeclaredKeys(member).Select(entry => new DeclaredPin(entry.Kind, entry.Key, null)));
            }
        }

        return pins;
    }

    /// <summary>Gets the direction and collection of a pin kind, such as <c>in.exec</c>.</summary>
    /// <param name="kind">The pin kind.</param>
    /// <returns>The group text.</returns>
    public static string Group(PinKind kind)
    {
        string direction = kind is PinKind.InputExec or PinKind.InputData or PinKind.InputType ? "in" : "out";
        string collection = kind is PinKind.InputExec or PinKind.OutputExec ? "exec" : kind is PinKind.InputType or PinKind.OutputType ? "type" : "data";
        return direction + "." + collection;
    }
}
