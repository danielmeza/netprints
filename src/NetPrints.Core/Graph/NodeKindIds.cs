#nullable enable

namespace NetPrints.Graph
{
    /// <summary>
    /// The stored kind id of every built-in node class that declares its pins by convention, declared once
    /// here for <see cref="NodeKindAttribute"/>; the document layer's built-in kinds forward these values.
    /// </summary>
    public static class NodeKindIds
    {
        /// <summary>The kind id of <see cref="MethodEntryNode"/>.</summary>
        public const string MethodEntry = "methodEntry";

        /// <summary>The kind id of <see cref="ConstructorEntryNode"/>.</summary>
        public const string ConstructorEntry = "constructorEntry";

        /// <summary>The kind id of <see cref="ReturnNode"/>.</summary>
        public const string Return = "return";

        /// <summary>The kind id of <see cref="ClassReturnNode"/>.</summary>
        public const string ClassReturn = "classReturn";

        /// <summary>The kind id of <see cref="TypeReturnNode"/>.</summary>
        public const string TypeReturn = "typeReturn";

        /// <summary>The kind id of <see cref="EventEntryNode"/>.</summary>
        public const string EventEntry = "eventEntry";

        /// <summary>The kind id of <see cref="MakeArrayTypeNode"/>.</summary>
        public const string MakeArrayType = "makeArrayType";

        /// <summary>The kind id of <see cref="TypeOfNode"/>.</summary>
        public const string TypeOf = "typeOf";

        /// <summary>The kind id of <see cref="IfElseNode"/>.</summary>
        public const string IfElse = "ifElse";

        /// <summary>The kind id of <see cref="ForLoopNode"/>.</summary>
        public const string ForLoop = "forLoop";

        /// <summary>The kind id of <see cref="ThrowNode"/>.</summary>
        public const string Throw = "throw";

        /// <summary>The kind id of <see cref="DefaultNode"/>.</summary>
        public const string Default = "default";
    }
}
