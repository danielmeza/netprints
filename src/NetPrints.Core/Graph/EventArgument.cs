#nullable enable
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// One argument of a custom event entry: the name and type of an output data pin, and so of a parameter of
    /// the generated method.
    /// </summary>
    /// <param name="Name">The argument's name; a valid C# identifier, unique among the entry's arguments.</param>
    /// <param name="Type">The argument's type.</param>
    public sealed record EventArgument(string Name, TypeSpecifier Type);
}
