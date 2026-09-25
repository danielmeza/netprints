#nullable enable
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Helper for resolving a node's generic type/method against its input type pins.
    /// </summary>
    public static class GenericsHelper
    {
        /*public static TypeSpecifier DetermineTypeNodeType(TypeNode node)
        {
            // TODO: Copy node.Type

            // Replace generic arguments with input pins
            foreach (NodeInputTypePin inputTypePin in node.InputTypePins)
            {
                // TODO: Check inputTypePin constraints
                TypeSpecifier inputType = DetermineTypeNodeType(inputTypePin.Node);

                int pinIndex = node.InputTypePins.IndexOf(inputTypePin);
                node.Type.GenericArguments[pinIndex] = inputType;
            }

            return node.Type;
        }*/

        /// <summary>
        /// Constructs <paramref name="type"/> with its generic arguments replaced by the inferred
        /// types of <paramref name="inputTypePins"/> whose name matches a generic argument's name.
        /// If <paramref name="type"/> is itself a <see cref="GenericType"/>, returns the matching
        /// input type pin's inferred type directly. Returns <paramref name="type"/> unchanged if no
        /// pin matches, or if <see cref="TypeSpecifier.Construct"/> throws (eg. a mismatched generic
        /// arity).
        /// </summary>
        /// <param name="type">Type to construct, generic or not.</param>
        /// <param name="inputTypePins">Input type pins to resolve generic arguments from, matched by name.</param>
        /// <returns>The constructed type, or <paramref name="type"/> unchanged.</returns>
        public static BaseType ConstructWithTypePins(BaseType type, IEnumerable<NodeInputTypePin> inputTypePins)
        {
            if (type is TypeSpecifier typeSpecifier)
            {
                // Find types to replace and build dictionary
                Dictionary<GenericType, BaseType> replacementTypes = new Dictionary<GenericType, BaseType>();

                foreach (var inputTypePin in inputTypePins)
                {
                    if (inputTypePin.InferredType?.Value is BaseType replacementType && !(replacementType is null))
                    {
                        GenericType? typeToReplace = typeSpecifier.GenericArguments.SingleOrDefault(arg => arg.Name == inputTypePin.Name) as GenericType;

                        // If we can not replace all 
                        if (!(typeToReplace is null))
                        {
                            replacementTypes.Add(typeToReplace, replacementType);
                        }
                    }
                }

                try
                {
                    var constructedType = typeSpecifier.Construct(replacementTypes);
                    return constructedType;
                }
                catch
                {
                    return typeSpecifier;
                }
            }
            else if (type is GenericType genericType)
            {
                BaseType? replacementType = inputTypePins.SingleOrDefault(t => t.Name == type.Name)?.InferredType?.Value;
                if (replacementType != null)
                {
                    return replacementType;
                }
            }

            return type;
        }
    }
}
