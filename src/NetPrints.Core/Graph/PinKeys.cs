#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrints.Graph
{
    /// <summary>
    /// Computes and resolves the pin references used by <c>ConnectionDocument.From/To</c> and pin
    /// state entries (document-format.md §1.4.2): <c>&lt;direction&gt;.&lt;kind&gt;.&lt;key&gt;</c>,
    /// where <c>key</c> is <see cref="Node.GetPinKeyName"/>'s result, disambiguated with
    /// <c>~2</c>, <c>~3</c>, … when more than one pin of the same (direction, kind) collection shares
    /// it. Does not include the node id (callers prepend <c>"&lt;nodeId&gt;/"</c>).
    /// </summary>
    public static class PinKeys
    {
        /// <summary>
        /// Returns <paramref name="pin"/>'s pin reference.
        /// </summary>
        /// <param name="pin">Pin to compute the reference of.</param>
        /// <returns>The pin reference (without a node id).</returns>
        /// <exception cref="InvalidOperationException"><paramref name="pin"/> is of an unknown pin type.</exception>
        public static string For(NodePin pin)
        {
            Node node = pin.Node;
            var (direction, kind, collection) = Describe(node, pin);

            string keyName = node.GetPinKeyName(pin);
            int occurrence = 0;
            int matchOccurrence = -1;

            for (int i = 0; i < collection.Count; i++)
            {
                if (node.GetPinKeyName(collection[i]) == keyName)
                {
                    occurrence++;
                    if (ReferenceEquals(collection[i], pin))
                    {
                        matchOccurrence = occurrence;
                    }
                }
            }

            string key = matchOccurrence <= 1 ? keyName : $"{keyName}~{matchOccurrence}";
            return $"{direction}.{kind}.{key}";
        }

        /// <summary>
        /// Returns the pin of <paramref name="node"/> whose reference (<see cref="For"/>) exactly
        /// equals <paramref name="pinReference"/>.
        /// </summary>
        /// <param name="node">Node to search.</param>
        /// <param name="pinReference">Pin reference to resolve (without a node id).</param>
        /// <returns>The matching pin, or <see langword="null"/> for an unknown or malformed reference
        /// (including the index form <c>"in.data.0"</c>, which is never accepted).</returns>
        public static NodePin? Find(Node node, string pinReference)
        {
            foreach (NodePin candidate in AllPins(node))
            {
                if (For(candidate) == pinReference)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<NodePin> AllPins(Node node)
        {
            foreach (NodeInputDataPin pin in node.InputDataPins)
            {
                yield return pin;
            }

            foreach (NodeOutputDataPin pin in node.OutputDataPins)
            {
                yield return pin;
            }

            foreach (NodeInputExecPin pin in node.InputExecPins)
            {
                yield return pin;
            }

            foreach (NodeOutputExecPin pin in node.OutputExecPins)
            {
                yield return pin;
            }

            foreach (NodeInputTypePin pin in node.InputTypePins)
            {
                yield return pin;
            }

            foreach (NodeOutputTypePin pin in node.OutputTypePins)
            {
                yield return pin;
            }
        }

        private static (string Direction, string Kind, IReadOnlyList<NodePin> Collection) Describe(Node node, NodePin pin)
        {
            return pin switch
            {
                NodeInputDataPin => ("in", "data", node.InputDataPins.Cast<NodePin>().ToList()),
                NodeOutputDataPin => ("out", "data", node.OutputDataPins.Cast<NodePin>().ToList()),
                NodeInputExecPin => ("in", "exec", node.InputExecPins.Cast<NodePin>().ToList()),
                NodeOutputExecPin => ("out", "exec", node.OutputExecPins.Cast<NodePin>().ToList()),
                NodeInputTypePin => ("in", "type", node.InputTypePins.Cast<NodePin>().ToList()),
                NodeOutputTypePin => ("out", "type", node.OutputTypePins.Cast<NodePin>().ToList()),
                _ => throw new InvalidOperationException($"Unknown pin type '{pin.GetType()}'."),
            };
        }
    }
}
