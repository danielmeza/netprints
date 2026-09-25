#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Translator
{
    /// <summary>
    /// Translates execution graphs into C#.
    /// </summary>
    public class ExecutionGraphTranslator
    {
        private const string JumpStackVarName = "jumpStack";
        private const string JumpStackType = "System.Collections.Generic.Stack<int>";

        private readonly Dictionary<NodeOutputDataPin, string> variableNames = new Dictionary<NodeOutputDataPin, string>();
        private readonly Dictionary<Node, List<int>> nodeStateIds = new Dictionary<Node, List<int>>();
        private int nextStateId = 0;
        private IEnumerable<Node> execNodes = new List<Node>();
        private IEnumerable<Node> nodes = new List<Node>();
        private readonly HashSet<NodeInputExecPin> pinsJumpedTo = new HashSet<NodeInputExecPin>();

        private int jumpStackStateId;

        private readonly StringBuilder builder = new StringBuilder();

        // Set as the first statement of Translate(), which every other method here is only ever
        // called from (directly or indirectly), never before. Backed by a nullable field instead of
        // asserted with `!` so a genuine misuse (calling a Translate*Node method without going
        // through Translate() first) throws a clear exception instead of a NullReferenceException.
        private ExecutionGraph? graphField;
        private ExecutionGraph graph
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

        private delegate void NodeTypeHandler(ExecutionGraphTranslator translator, Node node);

        // Each closure below casts to the exact type it is registered under (keyed by
        // node.GetType() in TranslateNode), so the cast always succeeds; a hard cast is used
        // instead of `as` + `!` so a violated invariant throws a clear InvalidCastException.
        private readonly Dictionary<Type, List<NodeTypeHandler>> nodeTypeHandlers = new Dictionary<Type, List<NodeTypeHandler>>()
        {
            { typeof(CallMethodNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateCallMethodNode((CallMethodNode)node) } },
            { typeof(VariableSetterNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateVariableSetterNode((VariableSetterNode)node) } },
            { typeof(ReturnNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateReturnNode((ReturnNode)node) } },
            { typeof(MethodEntryNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateMethodEntry((MethodEntryNode)node) } },
            { typeof(IfElseNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateIfElseNode((IfElseNode)node) } },
            { typeof(ConstructorNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateConstructorNode((ConstructorNode)node) } },
            { typeof(ExplicitCastNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateExplicitCastNode((ExplicitCastNode)node) } },
            { typeof(ThrowNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateThrowNode((ThrowNode)node) } },
            { typeof(AwaitNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateAwaitNode((AwaitNode)node) } },
            { typeof(TernaryNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateTernaryNode((TernaryNode)node) } },

            { typeof(ForLoopNode), new List<NodeTypeHandler> {
                (translator, node) => translator.TranslateStartForLoopNode((ForLoopNode)node),
                (translator, node) => translator.TranslateContinueForLoopNode((ForLoopNode)node)} },

            { typeof(RerouteNode), new List<NodeTypeHandler> { (translator, node) => translator.TranslateRerouteNode((RerouteNode)node) } },

            { typeof(VariableGetterNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateVariableGetterNode((VariableGetterNode)node) } },
            { typeof(LiteralNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateLiteralNode((LiteralNode)node) } },
            { typeof(MakeDelegateNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateMakeDelegateNode((MakeDelegateNode)node) } },
            { typeof(TypeOfNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateTypeOfNode((TypeOfNode)node) } },
            { typeof(MakeArrayNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateMakeArrayNode((MakeArrayNode)node) } },
            { typeof(DefaultNode), new List<NodeTypeHandler> { (translator, node) => translator.PureTranslateDefaultNode((DefaultNode)node) } },
        };

        private int GetNextStateId()
        {
            return nextStateId++;
        }

        private int GetExecPinStateId(NodeInputExecPin pin)
        {
            return nodeStateIds[pin.Node][pin.Node.InputExecPins.IndexOf(pin)];
        }

        private string GetOrCreatePinName(NodeOutputDataPin? pin)
        {
            // Return the default value of the pin type if nothing is connected
            if (pin == null)
            {
                return "null";
            }

            if (variableNames.ContainsKey(pin))
            {
                return variableNames[pin];
            }

            string pinName;

            // Special case for property setters, input name "value".
            // TODO: Don't rely on set_ prefix
            // TODO: Use PropertyGraph instead of MethodGraph
            if (pin.Node is MethodEntryNode && graph is MethodGraph methodGraph && methodGraph.Name.StartsWith("set_"))
            {
                pinName = "value";
            }
            else
            {
                pinName = TranslatorUtil.GetUniqueVariableName(pin.Name.Replace("<", "_").Replace(">", "_"), variableNames.Values.ToList());
            }

            variableNames.Add(pin, pinName);
            return pinName;
        }

        /// <summary>
        /// The C# expression for a pin's incoming value, or null to mean "omit the argument, use the
        /// parameter's own default value" (<see cref="NodeInputDataPin.UsesExplicitDefaultValue"/>).
        /// Callers check the result for null (e.g. <see cref="TranslateCallMethodNode"/>'s
        /// `prependArgumentName`), they do not simply emit it.
        /// </summary>
        private string? GetPinIncomingValue(NodeInputDataPin pin)
        {
            if (pin.IncomingPin == null)
            {
                if (pin.UsesUnconnectedValue && pin.UnconnectedValue != null)
                {
                    // The translator only ever runs on a fully type-resolved graph (GraphTypeInference
                    // already ran during deserialization), so PinType.Value is set here.
                    return TranslatorUtil.ObjectToLiteral(pin.UnconnectedValue, (TypeSpecifier)pin.PinType.Value!);
                }
                else if (pin.UsesExplicitDefaultValue)
                {
                    return null;
                }
                else
                {
                    throw new Exception($"Input data pin {pin} on {pin.Node} was unconnected without an explicit default or unconnected value.");
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

        private string GetOrCreateTypedPinName(NodeOutputDataPin pin)
        {
            string pinName = GetOrCreatePinName(pin);
            // Same resolved-graph invariant as GetPinIncomingValue above.
            return $"{pin.PinType.Value!.FullCodeName} {pinName}";
        }

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
                var v = GetOrCreatePinNames(node.OutputDataPins);
            }
        }

        private void TranslateVariables()
        {
            builder.AppendLine("// Variables");

            foreach (var v in variableNames)
            {
                NodeOutputDataPin pin = v.Key;
                string variableName = v.Value;

                if (!(pin.Node is MethodEntryNode))
                {
                    // Same resolved-graph invariant as GetPinIncomingValue above.
                    string typeName = pin.PinType.Value!.FullCodeName;
                    builder.AppendLine($"{typeName} {variableName} = default({typeName});");
                }
            }
        }

        private void TranslateSignature()
        {
            builder.AppendLine($"// {graph}");

            // Write visibility
            builder.Append($"{TranslatorUtil.VisibilityTokens[graph.Visibility]} ");

            MethodGraph? methodGraph = graph as MethodGraph;

            if (methodGraph != null)
            {
                // Write modifiers
                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Async))
                {
                    builder.Append("async ");
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Static))
                {
                    builder.Append("static ");
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Abstract))
                {
                    builder.Append("abstract ");
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Sealed))
                {
                    builder.Append("sealed ");
                }

                if (methodGraph.Modifiers.HasFlag(MethodModifiers.Override))
                {
                    builder.Append("override ");
                }
                else if (methodGraph.Modifiers.HasFlag(MethodModifiers.Virtual))
                {
                    builder.Append("virtual ");
                }

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
                    builder.Append($"{methodGraph.ReturnTypes.Single().FullCodeName} ");
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
            builder.AppendLine($"({string.Join(", ", GetOrCreateTypedPinNames(graph.EntryNode.OutputDataPins))})");
        }

        private void TranslateJumpStack()
        {
            builder.AppendLine("// Jump stack");

            builder.AppendLine($"State{jumpStackStateId}:");
            builder.AppendLine($"if ({JumpStackVarName}.Count == 0) throw new System.Exception();");
            builder.AppendLine($"switch ({JumpStackVarName}.Pop())");
            builder.AppendLine("{");

            foreach (NodeInputExecPin pin in pinsJumpedTo)
            {
                builder.AppendLine($"case {GetExecPinStateId(pin)}:");
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
        public string Translate(ExecutionGraph graph, bool withSignature)
        {
            this.graph = graph;

            // Reset state
            variableNames.Clear();
            nodeStateIds.Clear();
            pinsJumpedTo.Clear();
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

            // Create variables for all output pins for every node
            CreateVariables();

            // Write the signatures
            if (withSignature)
            {
                TranslateSignature();
            }

            builder.AppendLine("{"); // Method start

            // Write a placeholder for the jump stack declaration
            // Replaced later
            builder.Append("%JUMPSTACKPLACEHOLDER%");

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
                        builder.AppendLine($"State{nodeStateIds[node][pinIndex]}:");
                        TranslateNode(node, pinIndex);
                        builder.AppendLine();
                    }
                }
            }

            // Write the jump stack if it was ever used
            if (pinsJumpedTo.Count > 0)
            {
                TranslateJumpStack();

                builder.Replace("%JUMPSTACKPLACEHOLDER%", $"{JumpStackType} {JumpStackVarName} = new {JumpStackType}();{Environment.NewLine}");
            }
            else
            {
                builder.Replace("%JUMPSTACKPLACEHOLDER%", "");
            }

            builder.AppendLine("}"); // Method end

            string code = builder.ToString();

            // Remove unused labels
            return RemoveUnnecessaryLabels(code);
        }

        private string RemoveUnnecessaryLabels(string code)
        {
            foreach (int stateId in nodeStateIds.Values.SelectMany(i => i))
            {
                if (!code.Contains($"goto State{stateId};"))
                {
                    code = code.Replace($"State{stateId}:", "");
                }
            }

            return code;
        }

        /// <summary>
        /// Translates a single node into C# by dispatching to the handler registered for
        /// <paramref name="node"/>'s runtime type (see the type-to-handler table built in the
        /// constructor field initializer). Writes a `// {node}` comment first unless
        /// <paramref name="node"/> is a <see cref="RerouteNode"/>. Does nothing beyond logging via
        /// <see cref="Debug.WriteLine(string)"/> if the type has no registered handler.
        /// </summary>
        /// <param name="node">Node to translate.</param>
        /// <param name="pinIndex">
        /// Index into the handlers registered for <paramref name="node"/>'s type; most node types
        /// register exactly one handler (index 0), but a type that emits code for more than one of
        /// its input exec pins (<see cref="ForLoopNode"/>: start vs. continue) registers one handler
        /// per pin and this selects which one runs.
        /// </param>
        public void TranslateNode(Node node, int pinIndex)
        {
            if (!(node is RerouteNode))
            {
                builder.AppendLine($"// {node}");
            }

            if (nodeTypeHandlers.ContainsKey(node.GetType()))
            {
                nodeTypeHandlers[node.GetType()][pinIndex](this, node);
            }
            else
            {
                Debug.WriteLine($"Unhandled type {node.GetType()} in TranslateNode");
            }
        }

        private void WriteGotoJumpStack()
        {
            builder.AppendLine($"goto State{jumpStackStateId};");
        }

        private void WritePushJumpStack(NodeInputExecPin pin)
        {
            if (!pinsJumpedTo.Contains(pin))
            {
                pinsJumpedTo.Add(pin);
            }

            builder.AppendLine($"{JumpStackVarName}.Push({GetExecPinStateId(pin)});");
        }

        private void WriteGotoInputPin(NodeInputExecPin pin)
        {
            builder.AppendLine($"goto State{GetExecPinStateId(pin)};");
        }

        private void WriteGotoOutputPin(NodeOutputExecPin pin)
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

        private void WriteGotoOutputPinIfNecessary(NodeOutputExecPin pin, NodeInputExecPin fromPin)
        {
            int fromId = GetExecPinStateId(fromPin);
            int nextId = fromId + 1;

            if (pin.OutgoingPin == null)
            {
                if (nextId != jumpStackStateId)
                {
                    WriteGotoJumpStack();
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
                }
            }
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

        /// <summary>
        /// Registered handler for <see cref="MethodEntryNode"/>. Currently a no-op: the entry node's
        /// own state is never jumped to by anything but the implicit fallthrough <see cref="Translate"/>
        /// already writes before the first state label, so there is nothing left to emit here.
        /// </summary>
        /// <param name="node">Entry node of the graph being translated.</param>
        public void TranslateMethodEntry(MethodEntryNode node)
        {
            /*// Go to the next state.
            // Only write if it's not the initial state (id==0) anyway.
            if (node.OutputExecPins[0].OutgoingPin != null && GetExecPinStateId(node.OutputExecPins[0].OutgoingPin) != 0)
            {
                WriteGotoOutputPin(node.OutputExecPins[0]);
            }*/
        }

        /// <summary>
        /// Translates a call to the method (or, if <see cref="OperatorUtil.TryGetOperatorInfo"/>
        /// recognizes it, operator) described by <paramref name="node"/>. Emits its pure dependencies
        /// first, wraps the call in a try/catch when <see cref="CallMethodNode.HandlesExceptions"/> is
        /// set (assigning the caught exception to <see cref="CallMethodNode.ExceptionPin"/> and the
        /// return values to their defaults on the exception path), assigns single or tuple-wrapped
        /// return values, and advances execution to the next state unless the node is pure.
        /// </summary>
        /// <param name="node">Call-method node to translate.</param>
        /// <exception cref="Exception">
        /// The resolved operator does not have exactly the argument count its arity requires.
        /// </exception>
        public void TranslateCallMethodNode(CallMethodNode node)
        {
            // Wrap in try / catch
            if (node.HandlesExceptions)
            {
                builder.AppendLine("try");
                builder.AppendLine("{");
            }

            string? temporaryReturnName = null;

            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Write assignment of return values
            if (node.ReturnValuePins.Count == 1)
            {
                string returnName = GetOrCreatePinName(node.ReturnValuePins[0]);

                builder.Append($"{returnName} = ");
            }
            else if (node.ReturnValuePins.Count > 1)
            {
                temporaryReturnName = TranslatorUtil.GetTemporaryVariableName(random);

                // Same resolved-graph invariant as GetPinIncomingValue above.
                var returnTypeNames = string.Join(", ", node.ReturnValuePins.Select(pin => pin.PinType.Value!.FullCodeName));

                builder.Append($"{typeof(Tuple).FullName}<{returnTypeNames}> {temporaryReturnName} = ");
            }

            // Get arguments for method call
            var argumentNames = GetPinIncomingValues(node.ArgumentPins);

            // Check whether the method is an operator and we need to translate its name
            // into operator symbols. Otherwise just call the method normally.
            if (OperatorUtil.TryGetOperatorInfo(node.MethodSpecifier, out var operatorInfo))
            {
                Debug.Assert(!argumentNames.Any(a => a is null));

                if (operatorInfo.Unary)
                {
                    if (argumentNames.Count() != 1)
                    {
                        throw new Exception($"Unary operator was found but did not have one argument: {node.MethodName}");
                    }

                    if (operatorInfo.UnaryRightPosition)
                    {
                        builder.AppendLine($"{argumentNames.ElementAt(0)}{operatorInfo.Symbol};");
                    }
                    else
                    {
                        builder.AppendLine($"{operatorInfo.Symbol}{argumentNames.ElementAt(0)};");
                    }
                }
                else
                {
                    if (argumentNames.Count() != 2)
                    {
                        throw new Exception($"Binary operator was found but did not have two arguments: {node.MethodName}");
                    }

                    builder.AppendLine($"{argumentNames.ElementAt(0)}{operatorInfo.Symbol}{argumentNames.ElementAt(1)};");
                }
            }
            else
            {
                // Static: Write class name / target, default to own class name
                // Instance: Write target, default to this

                if (node.IsStatic)
                {
                    builder.Append($"{node.DeclaringType.FullCodeName}.");
                }
                else
                {
                    if (node.TargetPin.IncomingPin != null)
                    {
                        string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                        builder.Append($"{targetName}.");
                    }
                    else
                    {
                        // Default to this
                        builder.Append("this.");
                    }
                }

                string?[] argNameArray = argumentNames.ToArray();
                Debug.Assert(argNameArray.Length == node.MethodSpecifier.Parameters.Count);

                bool prependArgumentName = argNameArray.Any(a => a is null);

                List<string> arguments = new List<string>();

                foreach ((var argName, var methodParameter) in argNameArray.Zip(node.MethodSpecifier.Parameters, Tuple.Create))
                {
                    // null means use default value
                    if (!(argName is null))
                    {
                        string argument = argName;

                        // Prepend with argument name if wanted
                        if (prependArgumentName)
                        {
                            argument = $"{methodParameter.Name}: {argument}";
                        }

                        // Prefix with "out" / "ref" / "in"
                        switch (methodParameter.PassType)
                        {
                            case MethodParameterPassType.Out:
                                argument = "out " + argument;
                                break;
                            case MethodParameterPassType.Reference:
                                argument = "ref " + argument;
                                break;
                            case MethodParameterPassType.In:
                                // Don't pass with in as it could break implicit casts.
                                // argument = "in " + argument;
                                break;
                            default:
                                break;
                        }

                        arguments.Add(argument);
                    }
                }

                // Write the method call
                builder.AppendLine($"{node.BoundMethodName}({string.Join(", ", arguments)});");
            }

            // Assign the real variables from the temporary tuple
            if (node.ReturnValuePins.Count > 1)
            {
                var returnNames = GetOrCreatePinNames(node.ReturnValuePins);
                for (int i = 0; i < returnNames.Count(); i++)
                {
                    builder.AppendLine($"{returnNames.ElementAt(i)} = {temporaryReturnName}.Item{i + 1};");
                }
            }

            // Set the exception to null on success if catch pin is connected
            if (node.HandlesExceptions)
            {
                builder.AppendLine($"{GetOrCreatePinName(node.ExceptionPin)} = null;");
            }

            // Go to the next state
            if (!node.IsPure)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }

            // Catch exceptions if catch pin is connected
            if (node.HandlesExceptions)
            {
                string exceptionVarName = TranslatorUtil.GetTemporaryVariableName(random);
                builder.AppendLine("}");
                builder.AppendLine($"catch (System.Exception {exceptionVarName})");
                builder.AppendLine("{");
                builder.AppendLine($"{GetOrCreatePinName(node.ExceptionPin)} = {exceptionVarName};");

                // Set all return values to default on exception
                foreach (var returnValuePin in node.ReturnValuePins)
                {
                    string returnName = GetOrCreatePinName(returnValuePin);
                    // Same resolved-graph invariant as GetPinIncomingValue above.
                    builder.AppendLine($"{returnName} = default({returnValuePin.PinType.Value!.FullCodeName});");
                }

                if (!node.IsPure)
                {
                    WriteGotoOutputPinIfNecessary(node.CatchPin, node.InputExecPins[0]);
                }

                builder.AppendLine("}");
            }
        }

        /// <summary>
        /// Translates <paramref name="node"/> into a `new` expression: emits its pure dependencies,
        /// assigns the constructed instance to the node's output pin, and writes the constructor
        /// arguments (named and/or `out`/`ref`-prefixed as <see cref="TranslateCallMethodNode"/> does
        /// for method arguments). Advances execution to the next state unless the node is pure.
        /// </summary>
        /// <param name="node">Constructor node to translate.</param>
        public void TranslateConstructorNode(ConstructorNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Write assignment and constructor
            string returnName = GetOrCreatePinName(node.OutputDataPins[0]);
            builder.Append($"{returnName} = new {node.ClassType}");

            // Write constructor arguments
            var argumentNames = GetPinIncomingValues(node.ArgumentPins);
            //builder.AppendLine($"({string.Join(", ", argumentNames)});");

            string?[] argNameArray = argumentNames.ToArray();
            Debug.Assert(argNameArray.Length == node.ConstructorSpecifier.Arguments.Count);

            bool prependArgumentName = argNameArray.Any(a => a is null);

            List<string> arguments = new List<string>();

            foreach ((var argName, var constructorParameter) in argNameArray.Zip(node.ConstructorSpecifier.Arguments, Tuple.Create))
            {
                // null means use default value
                if (!(argName is null))
                {
                    string argument = argName;

                    // Prepend with argument name if wanted
                    if (prependArgumentName)
                    {
                        argument = $"{constructorParameter.Name}: {argument}";
                    }

                    // Prefix with "out" / "ref" / "in"
                    switch (constructorParameter.PassType)
                    {
                        case MethodParameterPassType.Out:
                            argument = "out " + argument;
                            break;
                        case MethodParameterPassType.Reference:
                            argument = "ref " + argument;
                            break;
                        case MethodParameterPassType.In:
                            // Don't pass with in as it could break implicit casts.
                            // argument = "in " + argument;
                            break;
                        default:
                            break;
                    }

                    arguments.Add(argument);
                }
            }

            // Write the method call
            builder.AppendLine($"({string.Join(", ", arguments)});");

            if (!node.IsPure)
            {
                // Go to the next state
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }
        }

        /// <summary>
        /// Translates <paramref name="node"/> into either a hard cast (`(T)x`, throwing on failure) or
        /// an `as` cast with a null check branching to <see cref="ExplicitCastNode.CastFailedPin"/> /
        /// <see cref="ExplicitCastNode.CastSuccessPin"/>, depending on whether the failure pin is
        /// connected. Does nothing if <see cref="ExplicitCastNode.ObjectToCast"/> is unconnected.
        /// </summary>
        /// <param name="node">Cast node to translate.</param>
        public void TranslateExplicitCastNode(ExplicitCastNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Try to cast the incoming object and go to next states.
            if (node.ObjectToCast.IncomingPin != null)
            {
                // GetPinIncomingValue only returns null for an unconnected pin using its parameter's
                // default value; IncomingPin != null above rules that out here.
                string pinToCastName = GetPinIncomingValue(node.ObjectToCast)!;
                string outputName = GetOrCreatePinName(node.CastPin);

                // If failure pin is not connected write explicit cast that throws.
                // Otherwise check if cast object is null and execute failure
                // path if it is.
                if (node.IsPure || node.CastFailedPin.OutgoingPin == null)
                {
                    builder.AppendLine($"{outputName} = ({node.CastType.FullCodeNameUnbound}){pinToCastName};");

                    if (!node.IsPure)
                    {
                        WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                    }
                }
                else
                {
                    builder.AppendLine($"{outputName} = {pinToCastName} as {node.CastType.FullCodeNameUnbound};");

                    if (!node.IsPure)
                    {
                        builder.AppendLine($"if ({outputName} is null)");
                        builder.AppendLine("{");
                        WriteGotoOutputPinIfNecessary(node.CastFailedPin, node.InputExecPins[0]);
                        builder.AppendLine("}");
                        builder.AppendLine("else");
                        builder.AppendLine("{");
                        WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                        builder.AppendLine("}");
                    }
                }
            }
        }

        /// <summary>
        /// Translates <paramref name="node"/> into a `throw &lt;expression&gt;;` statement, after
        /// emitting its pure dependencies.
        /// </summary>
        /// <param name="node">Throw node to translate.</param>
        public void TranslateThrowNode(ThrowNode node)
        {
            TranslateDependentPureNodes(node);
            builder.AppendLine($"throw {GetPinIncomingValue(node.ExceptionPin)};");
        }

        /// <summary>
        /// Translates <paramref name="node"/> into an `await &lt;task&gt;;` statement, after emitting
        /// its pure dependencies. Assigns the awaited result to <see cref="AwaitNode.ResultPin"/>'s
        /// variable first if the awaited task has one.
        /// </summary>
        /// <param name="node">Await node to translate.</param>
        public void TranslateAwaitNode(AwaitNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            // Store result if task has a return value.
            if (node.ResultPin != null)
            {
                builder.Append($"{GetOrCreatePinName(node.ResultPin)} = ");
            }

            builder.AppendLine($"await {GetPinIncomingValue(node.TaskPin)};");
        }

        /// <summary>
        /// Translates <paramref name="node"/> into a `condition ? trueValue : falseValue` assignment,
        /// after emitting its pure dependencies, and advances execution to the next state unless the
        /// node is pure.
        /// </summary>
        /// <param name="node">Ternary node to translate.</param>
        public void TranslateTernaryNode(TernaryNode node)
        {
            if (!node.IsPure)
            {
                // Translate all the pure nodes this node depends on in
                // the correct order
                TranslateDependentPureNodes(node);
            }

            builder.Append($"{GetOrCreatePinName(node.OutputObjectPin)} = ");
            builder.Append($"{GetPinIncomingValue(node.ConditionPin)} ? ");
            builder.Append($"{GetPinIncomingValue(node.TrueObjectPin)} : ");
            builder.AppendLine($"{GetPinIncomingValue(node.FalseObjectPin)};");

            if (!node.IsPure)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins.Single(), node.InputExecPins.Single());
            }
        }

        /// <summary>
        /// Translates <paramref name="node"/> into an assignment to the target variable, property or
        /// indexer (instance, static, or indexed by <see cref="VariableNode.IndexPin"/> when
        /// <see cref="VariableNode.IsIndexer"/> is set), after emitting its pure dependencies. Also
        /// assigns the same value to the node's output pin, and advances execution to the next state.
        /// </summary>
        /// <param name="node">Variable-setter node to translate.</param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="node"/> is a static setter with no explicit target type and its graph has
        /// no declaring class.
        /// </exception>
        public void TranslateVariableSetterNode(VariableSetterNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            // The value pin does not use the explicit-default-value convention (that is only set on
            // CallMethodNode's argument pins), so this is never null.
            string valueName = GetPinIncomingValue(node.NewValuePin)!;

            // Add target name if there is a target (null for local and static variables)
            if (node.IsStatic)
            {
                if (!(node.TargetType is null))
                {
                    builder.Append(node.TargetType.FullCodeName);
                }
                else
                {
                    var declaringClass = node.Graph.Class
                        ?? throw new InvalidOperationException("A static variable setter's graph has no class.");
                    builder.Append(declaringClass.Name);
                }
            }
            if (node.TargetPin != null)
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append(targetName);
                }
                else
                {
                    builder.Append("this");
                }
            }

            // Add index if needed
            if (node.IsIndexer)
            {
                // IsIndexer implies IndexPin is not null (VariableNode.IndexPin).
                builder.Append($"[{GetPinIncomingValue(node.IndexPin!)}]");
            }
            else
            {
                builder.Append($".{node.VariableName}");
            }

            builder.AppendLine($" = {valueName};");

            // Set output pin of this node to the same value
            builder.AppendLine($"{GetOrCreatePinName(node.OutputDataPins[0])} = {valueName};");

            // Go to the next state
            WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
        }

        /// <summary>
        /// Translates <paramref name="node"/> into a `return` statement, after emitting its pure
        /// dependencies: no value for a void or <see cref="Task"/>-returning method, the single input
        /// pin's value for one return value, or a tuple construction for more than one. Omits the
        /// bare `return;` entirely when the node has no return values and is the graph's last state
        /// (fallthrough already reaches the end of the method body).
        /// </summary>
        /// <param name="node">Return node to translate.</param>
        public void TranslateReturnNode(ReturnNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            if (node.InputDataPins.Count == 0)
            {
                // Only write return if the return node is not the last node
                if (GetExecPinStateId(node.InputExecPins[0]) != nodeStateIds.Count - 1)
                {
                    builder.AppendLine("return;");
                }
            }
            else if (node.InputDataPins.Count == 1)
            {
                // Special case for async functions returning Task (no return value)
                if (node.InputDataPins[0].PinType == TypeSpecifier.FromType<Task>())
                {
                    builder.AppendLine("return;");
                }
                else
                {
                    builder.AppendLine($"return {GetPinIncomingValue(node.InputDataPins[0])};");
                }
            }
            else
            {
                var returnValues = node.InputDataPins.Select(pin => GetPinIncomingValue(pin));

                // Tuple<Types..> (won't be needed in the future)
                // Same resolved-graph invariant as GetPinIncomingValue above.
                string returnType = typeof(Tuple).FullName + "<" + string.Join(", ", node.InputDataPins.Select(pin => pin.PinType.Value!.FullCodeName)) + ">";
                builder.AppendLine($"return new {returnType}({string.Join(", ", returnValues)});");
            }
        }

        /// <summary>
        /// Translates <paramref name="node"/> into an `if (condition) { ... } else { ... }` statement,
        /// after emitting its pure dependencies. Each branch either advances execution to the outgoing
        /// pin's target state or, when a branch's exec pin is unconnected, emits a bare `return;`.
        /// </summary>
        /// <param name="node">If/else node to translate.</param>
        public void TranslateIfElseNode(IfElseNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            // The condition pin does not use the explicit-default-value convention, so this is never
            // null (see GetPinIncomingValue).
            string conditionVar = GetPinIncomingValue(node.ConditionPin)!;

            builder.AppendLine($"if ({conditionVar})");
            builder.AppendLine("{");

            if (node.TruePin.OutgoingPin != null)
            {
                WriteGotoOutputPinIfNecessary(node.TruePin, node.InputExecPins[0]);
            }
            else
            {
                builder.AppendLine("return;");
            }

            builder.AppendLine("}");

            builder.AppendLine("else");
            builder.AppendLine("{");

            if (node.FalsePin.OutgoingPin != null)
            {
                WriteGotoOutputPinIfNecessary(node.FalsePin, node.InputExecPins[0]);
            }
            else
            {
                builder.AppendLine("return;");
            }

            builder.AppendLine("}");
        }

        /// <summary>
        /// Registered handler for the "start" input exec pin of <paramref name="node"/> (see
        /// <see cref="TranslateNode"/>'s pin-index dispatch). Initializes the loop index from
        /// <see cref="ForLoopNode.InitialIndexPin"/>, and, while it is below
        /// <see cref="ForLoopNode.MaxIndexPin"/>, pushes the continue state onto the jump stack and
        /// enters the loop body, after emitting the node's pure dependencies.
        /// </summary>
        /// <param name="node">For-loop node to translate.</param>
        public void TranslateStartForLoopNode(ForLoopNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            builder.AppendLine($"{GetOrCreatePinName(node.IndexPin)} = {GetPinIncomingValue(node.InitialIndexPin)};");
            builder.AppendLine($"if ({GetOrCreatePinName(node.IndexPin)} < {GetPinIncomingValue(node.MaxIndexPin)})");
            builder.AppendLine("{");
            WritePushJumpStack(node.ContinuePin);
            WriteGotoOutputPinIfNecessary(node.LoopPin, node.ExecutionPin);
            builder.AppendLine("}");
        }

        /// <summary>
        /// Registered handler for the "continue" input exec pin of <paramref name="node"/> (see
        /// <see cref="TranslateNode"/>'s pin-index dispatch). Increments the loop index and, while it
        /// is below <see cref="ForLoopNode.MaxIndexPin"/>, pushes the continue state onto the jump
        /// stack and re-enters the loop body; otherwise advances to
        /// <see cref="ForLoopNode.CompletedPin"/>. Emits the node's pure dependencies first.
        /// </summary>
        /// <param name="node">For-loop node to translate.</param>
        public void TranslateContinueForLoopNode(ForLoopNode node)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            TranslateDependentPureNodes(node);

            builder.AppendLine($"{GetOrCreatePinName(node.IndexPin)}++;");
            builder.AppendLine($"if ({GetOrCreatePinName(node.IndexPin)} < {GetPinIncomingValue(node.MaxIndexPin)})");
            builder.AppendLine("{");
            WritePushJumpStack(node.ContinuePin);
            WriteGotoOutputPinIfNecessary(node.LoopPin, node.ContinuePin);
            builder.AppendLine("}");

            WriteGotoOutputPinIfNecessary(node.CompletedPin, node.ContinuePin);
        }

        /// <summary>
        /// Translates <paramref name="node"/> into a read of the target variable, property or indexer
        /// (instance, static, or indexed by <see cref="VariableNode.IndexPin"/> when
        /// <see cref="VariableNode.IsIndexer"/> is set), assigned to the node's output pin.
        /// </summary>
        /// <param name="node">Variable-getter node to translate.</param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="node"/> is a static getter with no explicit target type and its graph has
        /// no declaring class.
        /// </exception>
        public void PureTranslateVariableGetterNode(VariableGetterNode node)
        {
            string valueName = GetOrCreatePinName(node.OutputDataPins[0]);

            builder.Append($"{valueName} = ");

            if (node.IsStatic)
            {
                if (!(node.TargetType is null))
                {
                    builder.Append(node.TargetType.FullCodeName);
                }
                else
                {
                    var declaringClass = node.Graph.Class
                        ?? throw new InvalidOperationException("A static variable getter's graph has no class.");
                    builder.Append(declaringClass.Name);
                }
            }
            else
            {
                if (node.TargetPin?.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append(targetName);
                }
                else
                {
                    // Default to this
                    builder.Append("this");
                }
            }

            // Add index if needed
            if (node.IsIndexer)
            {
                // IsIndexer implies IndexPin is not null (VariableNode.IndexPin).
                builder.Append($"[{GetPinIncomingValue(node.IndexPin!)}]");
            }
            else
            {
                builder.Append($".{node.VariableName}");
            }

            builder.AppendLine(";");
        }

        /// <summary>
        /// Translates <paramref name="node"/> by assigning its single input pin's value (its literal,
        /// or an incoming expression if connected) to the node's output pin.
        /// </summary>
        /// <param name="node">Literal node to translate.</param>
        public void PureTranslateLiteralNode(LiteralNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.ValuePin)} = {GetPinIncomingValue(node.InputDataPins[0])};");
        }

        /// <summary>
        /// Translates <paramref name="node"/> by assigning a method-group expression (a delegate
        /// bound to a static or instance method) to the node's output pin.
        /// </summary>
        /// <param name="node">Make-delegate node to translate.</param>
        public void PureTranslateMakeDelegateNode(MakeDelegateNode node)
        {
            // Write assignment of return value
            string returnName = GetOrCreatePinName(node.OutputDataPins[0]);
            builder.Append($"{returnName} = ");

            // Static: Write class name / target, default to own class name
            // Instance: Write target, default to this

            if (node.IsFromStaticMethod)
            {
                builder.Append($"{node.MethodSpecifier.DeclaringType}.");
            }
            else
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    string targetName = GetOrCreatePinName(node.TargetPin.IncomingPin);
                    builder.Append($"{targetName}.");
                }
                else
                {
                    // Default to thise
                    builder.Append("this.");
                }
            }

            // Write method name
            builder.AppendLine($"{node.MethodSpecifier.Name};");
        }

        /// <summary>
        /// Translates <paramref name="node"/> by assigning `typeof(&lt;type&gt;)` to the node's output
        /// pin, using <see cref="object"/> as the type when the node's input type pin has not
        /// inferred a type.
        /// </summary>
        /// <param name="node">Type-of node to translate.</param>
        public void PureTranslateTypeOfNode(TypeOfNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.TypePin)} = typeof({node.InputTypePin.InferredType?.Value?.FullCodeNameUnbound ?? "System.Object"});");
        }

        /// <summary>
        /// Translates <paramref name="node"/> into an array-creation expression assigned to the node's
        /// output pin: a predefined-size allocation (`new T[size]`) when
        /// <see cref="MakeArrayNode.UsePredefinedSize"/> is set, otherwise an initializer list built
        /// from the node's input data pins.
        /// </summary>
        /// <param name="node">Make-array node to translate.</param>
        public void PureTranslateMakeArrayNode(MakeArrayNode node)
        {
            builder.Append($"{GetOrCreatePinName(node.OutputDataPins[0])} = new {node.ArrayType.FullCodeName}");

            // Use predefined size or initializer list
            if (node.UsePredefinedSize)
            {
                // HACKish: Remove trailing "[]" contained in type
                builder.Remove(builder.Length - 2, 2);
                builder.AppendLine($"[{GetPinIncomingValue(node.SizePin)}];");
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine("{");

                foreach (var inputDataPin in node.InputDataPins)
                {
                    builder.AppendLine($"{GetPinIncomingValue(inputDataPin)},");
                }

                builder.AppendLine("};");
            }
        }
        /// <summary>
        /// Translates <paramref name="node"/> by assigning `default(&lt;type&gt;)` to the node's
        /// output pin.
        /// </summary>
        /// <param name="node">Default node to translate.</param>
        public void PureTranslateDefaultNode(DefaultNode node)
        {
            builder.AppendLine($"{GetOrCreatePinName(node.DefaultValuePin)} = default({node.Type.FullCodeName});");
        }

        /// <summary>
        /// Translates <paramref name="node"/> by passing its single connection through: assigns the
        /// incoming value to the output data pin for a data reroute, or advances execution to the next
        /// state for an exec reroute. A type reroute has no runtime representation and is a no-op here
        /// (type reroutes only affect type inference).
        /// </summary>
        /// <param name="node">Reroute node to translate.</param>
        /// <exception cref="NotImplementedException">
        /// <paramref name="node"/> does not reroute exactly one pin: exactly one of
        /// <see cref="RerouteNode.ExecRerouteCount"/>, <see cref="RerouteNode.TypeRerouteCount"/> and
        /// <see cref="RerouteNode.DataRerouteCount"/> must be 1 and the others 0.
        /// </exception>
        public void TranslateRerouteNode(RerouteNode node)
        {
            if (node.ExecRerouteCount + node.TypeRerouteCount + node.DataRerouteCount != 1)
            {
                throw new NotImplementedException("Only implemented reroute nodes with exactly 1 type of pin.");
            }

            if (node.DataRerouteCount == 1)
            {
                builder.AppendLine($"{GetOrCreatePinName(node.OutputDataPins[0])} = {GetPinIncomingValue(node.InputDataPins[0])};");
            }
            else if (node.ExecRerouteCount == 1)
            {
                WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
            }
        }
    }
}
