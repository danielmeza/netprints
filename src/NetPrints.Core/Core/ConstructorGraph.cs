#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// A class's constructor: an <see cref="ExecutionGraph"/> whose entry node is a
    /// <see cref="ConstructorEntryNode"/>.
    /// </summary>
    [DataContract]
    public class ConstructorGraph : ExecutionGraph
    {
        /// <summary>
        /// Creates a constructor graph and its <see cref="ConstructorEntryNode"/>. <see cref="NodeGraph.Class"/>
        /// is unset; the caller assigns it (see <see cref="ClassGraph.Constructors"/>).
        /// </summary>
        public ConstructorGraph()
        {
            EntryNode = new ConstructorEntryNode(this);
        }

        /// <summary>
        /// Returns the declaring class's name, or "?" if the graph has no class yet.
        /// </summary>
        /// <returns>The declaring class's name, or "?".</returns>
        public override string ToString()
        {
            return Class?.Name ?? "?";
        }
    }
}
