#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Translator
{
    /// <summary>
    /// Translates execution graphs into C#, dispatching each node to the translator its
    /// <see cref="TranslationEnvironment"/> registers for the node's type. Not thread-safe: use one
    /// instance per translation.
    /// </summary>
    public sealed class ExecutionGraphTranslator : IExecutionTranslationContext, IBuiltInTranslationContext
    {
        private const string JumpStackVarName = "jumpStack";
        private const string JumpStackType = "System.Collections.Generic.Stack<int>";

        // Placeholder replaced by TranslateJumpStack's declaration, once it is known to be needed.
        private const string JumpStackPlaceholder = "%JUMPSTACKPLACEHOLDER%";

        private readonly Dictionary<NodeOutputDataPin, string> variableNames = new Dictionary<NodeOutputDataPin, string>();
        private readonly HashSet<string> reservedLocalNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<Node, List<int>> nodeStateIds = new Dictionary<Node, List<int>>();
        private int nextStateId = 0;
        private IEnumerable<Node> execNodes = new List<Node>();
        private IEnumerable<Node> nodes = new List<Node>();
        private readonly HashSet<NodeInputExecPin> pinsJumpedTo = new HashSet<NodeInputExecPin>();

        private int jumpStackStateId;

        private readonly StringBuilder builder = new StringBuilder();
        private readonly List<NodeOffset> nodeOffsets = new List<NodeOffset>();

        private readonly TranslationEnvironment environment;

        /// <summary>
        /// Creates a translator that resolves node translators through <paramref name="environment"/>.
        /// </summary>
        /// <param name="environment">Node translators to dispatch to.</param>
        public ExecutionGraphTranslator(TranslationEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <inheritdoc />
        public NodeGraph Graph => graph;

        /// <inheritdoc />
        public ITypeDeclaration? Declaration => graph.Class;

        /// <inheritdoc />
        public ClassGraph? Class => graph.Class;

        /// <inheritdoc />
        public void Append(string code) => builder.Append(code);

        /// <inheritdoc />
        public void AppendLine(string code = "") => builder.AppendLine(code);

        /// <inheritdoc />
        public string CreateTemporaryVariableName() => TranslatorUtil.GetTemporaryVariableName(random);

        /// <summary>
        /// The offset of each impure node's own statements in the unformatted code the last
        /// <see cref="Translate(ExecutionGraph, bool)"/> or <see cref="TranslateEventEntry(EventGraph, EventEntryNode)"/>
        /// call returned, in the order they were written (research.md R3). Used only by
        /// <see cref="ClassTranslator"/> to build a <see cref="SourceMap"/>.
        /// </summary>
        internal IReadOnlyList<NodeOffset> LastNodeOffsets => nodeOffsets;

        bool IBuiltInTranslationContext.IsFinalExecState(NodeInputExecPin pin) =>
            GetExecPinStateId(pin) == nodeStateIds.Count - 1;

        // Set as the first statement of Translate()/TranslateEventEntry(), which every other method
        // here is only ever called from (directly or indirectly), never before. Backed by a nullable
        // field instead of asserted with `!` so a genuine misuse (calling a Translate*Node method
        // without going through one of those first) throws a clear exception instead of a
        // NullReferenceException. Typed NodeGraph (not ExecutionGraph) so an EventGraph (sub-phase G:
        // no single EntryNode, several EventEntryNodes may share one) can be translated through the
        // same context; TranslateSignature casts back to ExecutionGraph, the only case it applies to.
        private NodeGraph? graphField;
        private NodeGraph graph
        {
            get => graphField ?? throw new InvalidOperationException(
                $"{nameof(ExecutionGraphTranslator)}.{nameof(graph)} was read before {nameof(Translate)}() was called.");
            set => graphField = value;
        }

        private Random? randomField;
        private Random random
        {
            get => randomField ?? throw new InvalidOperationException(
                $"{nameof(ExecutionGraphTranslator)}.{nameof(random)} was read before {nameof(Translate)}() was called.");
            set => randomField = value;
        }

        private int GetNextStateId()
        {
            return nextStateId++;
        }

        private int GetExecPinStateId(NodeInputExecPin pin)
        {
            return nodeStateIds[pin.Node][pin.Node.InputExecPins.IndexOf(pin)];
        }

        /// <inheritdoc />
        public string GetOrCreatePinName(NodeOutputDataPin pin)
        {
            if (variableNames.ContainsKey(pin))
            {
                return variableNames[pin];
            }

            string pinName;

            // Special case for property setters, input name "value".
            // TODO: Don't rely on set_ prefix
            // TODO: Use PropertyGraph instead of MethodGraph
            if (pin.Node is MethodEntryNode && graph is MethodGraph methodGraph && methodGraph.Name.StartsWith("set_", StringComparison.Ordinal))
            {
                pinName = "value";
            }
            else
            {
                // Local variable names (declared first, TranslateVariables) are reserved before a pin
                // ever gets one of its own, so a generated pin name can never shadow a user-declared
                // local (data-model.md §3).
                List<string> reservedNames = variableNames.Values.Concat(reservedLocalNames).ToList();
                pinName = TranslatorUtil.GetUniqueVariableName(pin.Name.Replace("<", "_", StringComparison.Ordinal).Replace(">", "_", StringComparison.Ordinal), reservedNames);
            }

            variableNames.Add(pin, pinName);
            return pinName;
        }

        /// <summary>
        /// The C# expression for a pin's incoming value, or null to mean "omit the argument, use the
        /// parameter's own default value" (<see cref="NodeInputDataPin.UsesExplicitDefaultValue"/>).
        /// Callers check the result for null (e.g. the call-method translator's `prependArgumentName`),
        /// they do not simply emit it.
        /// </summary>
        public string? GetPinIncomingValue(NodeInputDataPin pin)
        {
            if (pin.IncomingPin == null)
            {
                if (pin.UsesUnconnectedValue && pin.UnconnectedValue != null)
                {
                    // The translator only ever runs on a fully type-resolved graph (GraphTypeInference
                    // already ran during deserialization), so PinType.Value is set here.
                    return TranslatorUtil.ObjectToLiteral(pin.UnconnectedValue, (TypeSpecifier)ResolvedPinType(pin));
                }
                else if (pin.UsesExplicitDefaultValue)
                {
                    return null;
                }
                else
                {
                    throw new InvalidOperationException($"Input data pin {pin} on {pin.Node} was unconnected without an explicit default or unconnected value.");
                    //return $"default({pin.PinType.Value.FullCodeName})";
                }
            }
            else
            {
                return GetOrCreatePinName(pin.IncomingPin);
            }
        }

        private IEnumerable<string> GetOrCreatePinNames(IEnumerable<NodeOutputDataPin> pins)
        {
            return pins.Select(pin => GetOrCreatePinName(pin)).ToList();
        }

        private IEnumerable<string?> GetPinIncomingValues(IEnumerable<NodeInputDataPin> pins)
        {
            return pins.Select(pin => GetPinIncomingValue(pin)).ToList();
        }

        /// <inheritdoc />
        public string GetOrCreateTypedPinName(NodeOutputDataPin pin)
        {
            string pinName = GetOrCreatePinName(pin);
            return $"{ResolvedPinType(pin).FullCodeName} {pinName}";
        }

        private static BaseType ResolvedPinType(NodeDataPin pin) =>
            pin.PinType.Value ?? throw new InvalidOperationException($"The type of pin {pin} on {pin.Node} is not resolved.");

        private IEnumerable<string> GetOrCreateTypedPinNames(IEnumerable<NodeOutputDataPin> pins)
        {
            return pins.Select(pin => GetOrCreateTypedPinName(pin)).ToList();
        }

        private void CreateStates()
        {
            foreach (Node node in execNodes)
            {
                if (!(node is MethodEntryNode))
                {
                    nodeStateIds.Add(node, new List<int>());

                    foreach (NodeInputExecPin execPin in node.InputExecPins)
                    {
                        nodeStateIds[node].Add(GetNextStateId());
                    }
                }
            }
        }

        private void CreateVariables()
        {
            foreach (Node node in nodes)
            {
                // Result discarded: called only to assign each output pin a variable name.
                GetOrCreatePinNames(node.OutputDataPins);
            }
        }

        /// <summary>
        /// Validates and reserves <paramref name="execGraph"/>'s local variable names (data-model.md
        /// §3), before any pin gets its own generated name (<see cref="GetOrCreatePinName"/>): each
        /// name must be a valid, non-keyword C# identifier, distinct from every parameter name and from
        /// every other local of the same graph. <see cref="ExecutionGraph.IsLocalNameAvailable"/> keeps
        /// the editor from creating a conflicting local in the first place; this re-checks a graph built
        /// or edited outside that gate.
        /// </summary>
        /// <param name="execGraph">Graph whose local variable names to reserve.</param>
        /// <exception cref="TranslationException">
        /// A local's name is not a valid C# identifier, matches a parameter name, or matches another
        /// local's name (<c>NPT004</c>).
        /// </exception>
        private void ReserveLocalVariableNames(ExecutionGraph execGraph)
        {
            var parameterNames = new HashSet<string>(execGraph.NamedArgumentTypes.Select(argument => argument.Name), StringComparer.Ordinal);

            foreach (string name in execGraph.LocalVariables.Select(local => local.Name))
            {
                bool conflicts = !SyntaxFacts.IsValidIdentifier(name)
                    || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
                    || parameterNames.Contains(name)
                    || !reservedLocalNames.Add(name);

                if (conflicts)
                {
                    throw new TranslationException(TranslationDiagnosticCodes.LocalVariableNameConflict,
                        $"Local variable '{name}' conflicts with a parameter, another local, or is not a valid identifier.",
                        TranslatorUtil.TryGetGraphKey(execGraph));
                }
            }
        }

        private void TranslateVariables()
        {
            builder.AppendLine("// Variables");

            if (graph is ExecutionGraph execGraph)
            {
                foreach (LocalVariable local in execGraph.LocalVariables)
                {
                    string typeName = local.Type.FullCodeName;
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{typeName} {local.Name} = default({typeName});");
                }
            }

            foreach (var v in variableNames)
            {
                NodeOutputDataPin pin = v.Key;
                string variableName = v.Value;

                // An entry node's own output data pins are its arguments, already declared as method
                // parameters (TranslateSignature/TranslateEventSignature); MethodEntryNode here,
                // EventEntryNode for an event method (sub-phase G). ConstructorEntryNode has no output
                // data pins yet (T103a), so it never reaches this check either way.
                if (pin.Node is not (MethodEntryNode or EventEntryNode))
                {
                    string typeName = ResolvedPinType(pin).FullCodeName;
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{typeName} {variableName} = default({typeName});");
                }
            }
        }

        private void TranslateSignature(IEnumerable<string> extraModifiers)
        {
            // Only ever called from Translate(ExecutionGraph, ...): graph is a MethodGraph or
            // ConstructorGraph here, never an EventGraph (TranslateEventEntry writes its own signature).
            var execGraph = (ExecutionGraph)graph;

            builder.AppendLine(CultureInfo.InvariantCulture, $"// {execGraph}");

            // Write visibility
            builder.Append(CultureInfo.InvariantCulture, $"{TranslatorUtil.VisibilityTokens[execGraph.Visibility]} ");

            MethodGraph? methodGraph = execGraph as MethodGraph;
            List<string> written = new List<string>();

            void WriteModifier(string modifier)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{modifier} ");
                written.Add(modifier);
            }

            if (methodGraph != null)
            {
                // Write modifiers
                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Async))
                {
                    WriteModifier("async");
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Static))
                {
                    WriteModifier(CSharpKeywords.Static);
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Abstract))
                {
                    WriteModifier(CSharpKeywords.Abstract);
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Sealed))
                {
                    WriteModifier(CSharpKeywords.Sealed);
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Override))
                {
                    WriteModifier(CSharpKeywords.Override);
                }
                else if (methodGraph.Modifiers.HasFlag(MethodModifiers.Virtual))
                {
                    WriteModifier(CSharpKeywords.Virtual);
                }
            }

            // Extra modifiers from member emitters; "partial" goes last, directly before the return type.
            List<string> extraModifiersList = extraModifiers.ToList();
            foreach (string modifier in extraModifiersList.Where(modifier => modifier != CSharpKeywords.Partial && !written.Contains(modifier)).OrderBy(modifier => modifier, StringComparer.Ordinal))
            {
                WriteModifier(modifier);
            }

            if (extraModifiersList.Contains(CSharpKeywords.Partial))
            {
                WriteModifier(CSharpKeywords.Partial);
            }

            if (methodGraph != null)
            {
                // Write return type
                if (methodGraph.ReturnTypes.Count() > 1)
                {
                    // Tuple<Types..> (won't be needed in the future)
                    string returnType = typeof(Tuple).FullName + "<" + string.Join(", ", methodGraph.ReturnTypes.Select(t => t.FullCodeName)) + ">";
                    builder.Append(returnType + " ");

                    //builder.Append($"({string.Join(", ", method.ReturnTypes.Select(t => t.FullName))}) ");
                }
                else if (methodGraph.ReturnTypes.Count() == 1)
                {
                    builder.Append(CultureInfo.InvariantCulture, $"{methodGraph.ReturnTypes.Single().FullCodeName} ");
                }
                else
                {
                    builder.Append("void ");
                }
            }

            // Write name
            builder.Append(graph.ToString());

            if (methodGraph != null)
            {
                // Write generic arguments if any
                if (methodGraph.GenericArgumentTypes.Any())
                {
                    builder.Append("<" + string.Join(", ", methodGraph.GenericArgumentTypes.Select(arg => arg.FullCodeName)) + ">");
                }
            }

            // Write parameters
            builder.AppendLine(CultureInfo.InvariantCulture, $"({string.Join(", ", GetOrCreateTypedPinNames(execGraph.EntryNode.OutputDataPins))})");
        }

        private void TranslateJumpStack()
        {
            builder.AppendLine("// Jump stack");

            builder.AppendLine(CultureInfo.InvariantCulture, $"State{jumpStackStateId}:");
            builder.AppendLine(CultureInfo.InvariantCulture, $"if ({JumpStackVarName}.Count == 0) throw new System.Exception();");
            builder.AppendLine(CultureInfo.InvariantCulture, $"switch ({JumpStackVarName}.Pop())");
            builder.AppendLine("{");

            foreach (NodeInputExecPin pin in pinsJumpedTo)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"case {GetExecPinStateId(pin)}:");
                WriteGotoInputPin(pin);
            }

            builder.AppendLine("default:");
            builder.AppendLine("throw new System.Exception();");

            builder.AppendLine("}"); // End switch
        }

        /// <summary>
        /// Translates a method to C#.
        /// </summary>
        /// <param name="graph">Execution graph to translate.</param>
        /// <param name="withSignature">Whether to translate the signature.</param>
        /// <returns>C# code for the method.</returns>
        public string Translate(ExecutionGraph graph, bool withSignature) =>
            Translate(graph, withSignature, Array.Empty<string>());

        /// <summary>
        /// Translates a method to C# with additional modifiers (from member emitters) in its signature.
        /// </summary>
        /// <param name="graph">Execution graph to translate.</param>
        /// <param name="withSignature">Whether to translate the signature.</param>
        /// <param name="extraModifiers">Modifiers to write after the graph's own, <c>partial</c> last; ignored
        /// when <paramref name="withSignature"/> is <see langword="false"/>.</param>
        /// <returns>C# code for the method.</returns>
        public string Translate(ExecutionGraph graph, bool withSignature, IEnumerable<string> extraModifiers)
        {
            this.graph = graph;

            // Reset state
            variableNames.Clear();
            reservedLocalNames.Clear();
            nodeStateIds.Clear();
            pinsJumpedTo.Clear();
            nodeOffsets.Clear();
            nextStateId = 0;
            builder.Clear();
            random = new Random(0);

            nodes = TranslatorUtil.GetAllNodesInExecGraph(graph);
            execNodes = TranslatorUtil.GetExecNodesInExecGraph(graph);

            // Assign a state id to every non-pure node
            CreateStates();

            // Assign jump stack state id
            // Write it later once we know which states get jumped to
            jumpStackStateId = GetNextStateId();

            // Reserve local variable names before any pin gets its own generated name (data-model.md §3)
            ReserveLocalVariableNames(graph);

            // Create variables for all output pins for every node
            CreateVariables();

            // Write the signatures
            if (withSignature)
            {
                TranslateSignature(extraModifiers);
            }

            builder.AppendLine("{"); // Method start

            // Write a placeholder for the jump stack declaration
            // Replaced later
            builder.Append(JumpStackPlaceholder);

            // Write the variable declarations
            TranslateVariables();
            builder.AppendLine();

            // Start at node after method entry if necessary (id!=0)
            NodeInputExecPin? initialOutgoingPin = graph.EntryNode.OutputExecPins[0].OutgoingPin;
            if (initialOutgoingPin != null && GetExecPinStateId(initialOutgoingPin) != 0)
            {
                WriteGotoOutputPin(graph.EntryNode.OutputExecPins[0]);
            }

            // Translate every exec node
            foreach (Node node in execNodes)
            {
                if (!(node is MethodEntryNode))
                {
                    for (int pinIndex = 0; pinIndex < node.InputExecPins.Count; pinIndex++)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"State{nodeStateIds[node][pinIndex]}:");
                        nodeOffsets.Add(new NodeOffset(builder.Length, node.Id));
                        TranslateNode(node, pinIndex);
                        builder.AppendLine();
                    }
                }
            }

            // Write the jump stack if it was ever used
            if (pinsJumpedTo.Count > 0)
            {
                TranslateJumpStack();

                builder.Replace(JumpStackPlaceholder, $"{JumpStackType} {JumpStackVarName} = new {JumpStackType}();{Environment.NewLine}");
            }
            else
            {
                builder.Replace(JumpStackPlaceholder, "");
            }

            builder.AppendLine("}"); // Method end

            string code = builder.ToString();

            // Remove unused labels
            return RemoveUnnecessaryLabels(code);
        }

        /// <summary>
        /// Translates one entry of an event graph (data-model.md §4) to its own C# method: a
        /// <see langword="void"/> (or <see langword="async"/> <c>System.Threading.Tasks.Task</c> if
        /// <see cref="MethodModifiers.Async"/>) method named <see cref="EventEntryNode.EventName"/>,
        /// with <paramref name="entry"/>'s visibility and modifiers. Only nodes reachable from
        /// <paramref name="entry"/> (its own exec successors and their pure dependencies) are
        /// translated, even if <paramref name="graph"/> holds other entries' nodes too.
        /// </summary>
        /// <param name="graph">Event graph <paramref name="entry"/> belongs to.</param>
        /// <param name="entry">Entry to translate.</param>
        /// <returns>C# code for the generated method.</returns>
        public string TranslateEventEntry(EventGraph graph, EventEntryNode entry) =>
            TranslateEventEntry(graph, entry, Array.Empty<string>());

        /// <summary>
        /// Same as <see cref="TranslateEventEntry(EventGraph, EventEntryNode)"/>, with additional
        /// modifiers (from member emitters) in the method's signature.
        /// </summary>
        /// <param name="graph">Event graph <paramref name="entry"/> belongs to.</param>
        /// <param name="entry">Entry to translate.</param>
        /// <param name="extraModifiers">Modifiers to write after <paramref name="entry"/>'s own,
        /// <c>partial</c> last.</param>
        /// <returns>C# code for the generated method.</returns>
        /// <exception cref="TranslationException">
        /// A node reachable only through another entry of <paramref name="graph"/> is depended on for
        /// data (<c>NPT001</c>, research.md K13).
        /// </exception>
        public string TranslateEventEntry(EventGraph graph, EventEntryNode entry, IEnumerable<string> extraModifiers)
        {
            ArgumentNullException.ThrowIfNull(graph);
            ArgumentNullException.ThrowIfNull(entry);

            this.graph = graph;

            // Reset state
            variableNames.Clear();
            reservedLocalNames.Clear();
            nodeStateIds.Clear();
            pinsJumpedTo.Clear();
            nodeOffsets.Clear();
            nextStateId = 0;
            builder.Clear();
            random = new Random(0);

            var ownExecNodes = new HashSet<Node>(TranslatorUtil.GetExecNodesFrom(entry));
            nodes = TranslatorUtil.GetAllNodesFrom(entry);
            execNodes = ownExecNodes;

            // A data or type dependency on an impure node not part of this entry's own exec flow means
            // it belongs to a different entry sharing the same graph: that node's value is never
            // computed here (research.md K13).
            Node? crossEntryDependency = nodes.FirstOrDefault(node => !node.IsPure && !ownExecNodes.Contains(node));
            if (crossEntryDependency is not null)
            {
                throw new TranslationException(TranslationDiagnosticCodes.CrossEntryDependency,
                    $"Event '{entry.EventName}' depends on node '{crossEntryDependency}', which belongs to a different event entry of the same graph.",
                    TranslatorUtil.TryGetGraphKey(graph), crossEntryDependency.Id);
            }

            // Assign a state id to every non-entry exec node
            CreateStates();

            // Assign jump stack state id
            jumpStackStateId = GetNextStateId();

            // Create variables for all output pins for every node
            CreateVariables();

            TranslateEventSignature(entry, extraModifiers);

            builder.AppendLine("{"); // Method start

            // Write a placeholder for the jump stack declaration
            builder.Append(JumpStackPlaceholder);

            // Write the variable declarations
            TranslateVariables();
            builder.AppendLine();

            // Start after the entry if necessary (id != 0)
            NodeInputExecPin? initialOutgoingPin = entry.InitialExecutionPin.OutgoingPin;
            if (initialOutgoingPin != null && GetExecPinStateId(initialOutgoingPin) != 0)
            {
                WriteGotoOutputPin(entry.InitialExecutionPin);
            }

            // Translate every exec node reachable from this entry
            foreach (Node node in execNodes)
            {
                if (!(node is EventEntryNode))
                {
                    for (int pinIndex = 0; pinIndex < node.InputExecPins.Count; pinIndex++)
                    {
                        builder.AppendLine(CultureInfo.InvariantCulture, $"State{nodeStateIds[node][pinIndex]}:");
                        nodeOffsets.Add(new NodeOffset(builder.Length, node.Id));
                        TranslateNode(node, pinIndex);
                        builder.AppendLine();
                    }
                }
            }

            // Write the jump stack if it was ever used
            if (pinsJumpedTo.Count > 0)
            {
                TranslateJumpStack();

                builder.Replace(JumpStackPlaceholder, $"{JumpStackType} {JumpStackVarName} = new {JumpStackType}();{Environment.NewLine}");
            }
            else
            {
                builder.Replace(JumpStackPlaceholder, "");
            }

            builder.AppendLine("}"); // Method end

            return RemoveUnnecessaryLabels(builder.ToString());
        }

        private void TranslateEventSignature(EventEntryNode entry, IEnumerable<string> extraModifiers)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"// {entry}");

            builder.Append(CultureInfo.InvariantCulture, $"{TranslatorUtil.VisibilityTokens[entry.Visibility]} ");

            bool isAsync = entry.Modifiers.HasFlag(MethodModifiers.Async);
            List<string> written = new List<string>();

            void WriteModifier(string modifier)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{modifier} ");
                written.Add(modifier);
            }

            if (isAsync)
            {
                WriteModifier("async");
            }

            if (entry.Modifiers.HasFlag(MethodModifiers.Static))
            {
                WriteModifier(CSharpKeywords.Static);
            }

            if (entry.Modifiers.HasFlag(MethodModifiers.Override))
            {
                WriteModifier(CSharpKeywords.Override);
            }

            // Extra modifiers from member emitters; "partial" goes last, directly before the return type.
            List<string> extraModifiersList = extraModifiers.ToList();
            foreach (string modifier in extraModifiersList.Where(modifier => modifier != CSharpKeywords.Partial && !written.Contains(modifier)).OrderBy(modifier => modifier, StringComparer.Ordinal))
            {
                WriteModifier(modifier);
            }

            if (extraModifiersList.Contains(CSharpKeywords.Partial))
            {
                WriteModifier(CSharpKeywords.Partial);
            }

            builder.Append(isAsync ? "System.Threading.Tasks.Task " : "void ");
            builder.Append(entry.EventName);
            builder.AppendLine(CultureInfo.InvariantCulture, $"({string.Join(", ", GetOrCreateTypedPinNames(entry.OutputDataPins))})");
        }

        private string RemoveUnnecessaryLabels(string code)
        {
            foreach (int stateId in nodeStateIds.Values.SelectMany(i => i))
            {
                if (!code.Contains($"goto State{stateId};", StringComparison.Ordinal))
                {
                    code = RemoveLabel(code, $"State{stateId}:");
                }
            }

            return code;
        }

        /// <summary>
        /// Removes every occurrence of <paramref name="label"/> from <paramref name="code"/> (normally
        /// exactly one: each state id is unique), shifting every recorded node offset past a removed
        /// occurrence down by its length (research.md R3), so it still points at the same generated text.
        /// </summary>
        /// <param name="code">Code to remove <paramref name="label"/> from.</param>
        /// <param name="label">Label text to remove, without its trailing line break.</param>
        /// <returns><paramref name="code"/> with every occurrence of <paramref name="label"/> removed.</returns>
        private string RemoveLabel(string code, string label)
        {
            int index;
            while ((index = code.IndexOf(label, StringComparison.Ordinal)) >= 0)
            {
                code = string.Concat(code.AsSpan(0, index), code.AsSpan(index + label.Length));

                for (int i = 0; i < nodeOffsets.Count; i++)
                {
                    if (nodeOffsets[i].Offset >= index)
                    {
                        nodeOffsets[i] = new NodeOffset(nodeOffsets[i].Offset - label.Length, nodeOffsets[i].NodeId);
                    }
                }
            }

            return code;
        }

        /// <summary>
        /// Translates a single node into C# by dispatching to the translator registered for
        /// <paramref name="node"/>'s runtime type. Writes a `// {node}` comment first unless
        /// <paramref name="node"/> is a <see cref="RerouteNode"/>.
        /// </summary>
        /// <param name="node">Node to translate.</param>
        /// <param name="pinIndex">Input exec pin index passed to the translator.</param>
        /// <exception cref="TranslationException">
        /// No translator is registered for the node's type (<c>NPT006</c>).
        /// </exception>
        private void TranslateNode(Node node, int pinIndex)
        {
            INodeTranslator translator = environment.Nodes.Find(node.GetType())
                ?? throw new TranslationException(TranslationDiagnosticCodes.NoTranslatorForNode, $"No translator for {node.GetType()}", TranslatorUtil.TryGetGraphKey(node.Graph), node.Id);

            if (!(node is RerouteNode))
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"// {node}");
            }

            translator.Translate(this, node, pinIndex);
        }

        /// <inheritdoc />
        public void WriteGotoJumpStack()
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"goto State{jumpStackStateId};");
        }

        /// <inheritdoc />
        public void WritePushJumpStack(NodeInputExecPin pin)
        {
            if (!pinsJumpedTo.Contains(pin))
            {
                pinsJumpedTo.Add(pin);
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"{JumpStackVarName}.Push({GetExecPinStateId(pin)});");
        }

        private void WriteGotoInputPin(NodeInputExecPin pin)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"goto State{GetExecPinStateId(pin)};");
        }

        /// <inheritdoc />
        public void WriteGotoOutputPin(NodeOutputExecPin pin)
        {
            if (pin.OutgoingPin == null)
            {
                WriteGotoJumpStack();
            }
            else
            {
                WriteGotoInputPin(pin.OutgoingPin);
            }
        }

        /// <inheritdoc />
        public bool WriteGotoOutputPinIfNecessary(NodeOutputExecPin pin, NodeInputExecPin fromPin)
        {
            int fromId = GetExecPinStateId(fromPin);
            int nextId = fromId + 1;

            if (pin.OutgoingPin == null)
            {
                if (nextId != jumpStackStateId)
                {
                    WriteGotoJumpStack();
                    return true;
                }
            }
            else
            {
                int toId = GetExecPinStateId(pin.OutgoingPin);

                // Only write the goto if the next state is not
                // the state we want to go to.
                if (nextId != toId)
                {
                    WriteGotoInputPin(pin.OutgoingPin);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Translates every pure node <paramref name="node"/> depends on (transitively, through data
        /// pins), in the dependency order computed by <see cref="TranslatorUtil.GetSortedPureNodes"/>,
        /// before <paramref name="node"/> itself is translated. Each dependent node is translated with
        /// pin index 0 (pure nodes register a single handler).
        /// </summary>
        /// <param name="node">Impure or pure node whose pure dependencies are translated.</param>
        public void TranslateDependentPureNodes(Node node)
        {
            var sortedPureNodes = TranslatorUtil.GetSortedPureNodes(node);
            foreach (Node depNode in sortedPureNodes)
            {
                TranslateNode(depNode, 0);
            }
        }
    }
}
