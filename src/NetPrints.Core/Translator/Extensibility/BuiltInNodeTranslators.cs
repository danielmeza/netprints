#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Translator;

/// <summary>
/// The translators of every built-in node kind, moved unchanged from the former type-to-handler table of
/// <see cref="ExecutionGraphTranslator"/>. Each method writes through the <see cref="IExecutionTranslationContext"/>
/// it is given and keeps no state.
/// </summary>
internal static class BuiltInNodeTranslators
{
    /// <summary>A bare early-out, written by the control-flow nodes that skip the rest of their body.</summary>
    private const string EarlyOutStatement = "return;";

    /// <summary>
    /// Builds the table behind <see cref="NodeTranslatorRegistry.BuiltIn"/>.
    /// </summary>
    internal static IReadOnlyDictionary<Type, INodeTranslator> Create() => new Dictionary<Type, INodeTranslator>
    {
        [typeof(CallMethodNode)] = Of<CallMethodNode>(TranslateCallMethodNode),
        [typeof(VariableSetterNode)] = Of<VariableSetterNode>(TranslateVariableSetterNode),
        [typeof(ReturnNode)] = Of<ReturnNode>(TranslateReturnNode),
        [typeof(MethodEntryNode)] = Of<MethodEntryNode>(TranslateMethodEntry),
        [typeof(IfElseNode)] = Of<IfElseNode>(TranslateIfElseNode),
        [typeof(ConstructorNode)] = Of<ConstructorNode>(TranslateConstructorNode),
        [typeof(ExplicitCastNode)] = Of<ExplicitCastNode>(TranslateExplicitCastNode),
        [typeof(ThrowNode)] = Of<ThrowNode>(TranslateThrowNode),
        [typeof(AwaitNode)] = Of<AwaitNode>(TranslateAwaitNode),
        [typeof(TernaryNode)] = Of<TernaryNode>(TranslateTernaryNode),
        [typeof(ForLoopNode)] = new PerPinTranslator<ForLoopNode>(TranslateStartForLoopNode, TranslateContinueForLoopNode),
        [typeof(RerouteNode)] = Of<RerouteNode>(TranslateRerouteNode),
        [typeof(VariableGetterNode)] = Of<VariableGetterNode>(PureTranslateVariableGetterNode),
        [typeof(LiteralNode)] = Of<LiteralNode>(PureTranslateLiteralNode),
        [typeof(MakeDelegateNode)] = Of<MakeDelegateNode>(PureTranslateMakeDelegateNode),
        [typeof(TypeOfNode)] = Of<TypeOfNode>(PureTranslateTypeOfNode),
        [typeof(MakeArrayNode)] = Of<MakeArrayNode>(PureTranslateMakeArrayNode),
        [typeof(DefaultNode)] = Of<DefaultNode>(PureTranslateDefaultNode),
    };

    private static INodeTranslator Of<TNode>(Action<IExecutionTranslationContext, TNode> translate)
        where TNode : Node =>
        new PerPinTranslator<TNode>(translate);

    /// <summary>
    /// Adapts typed handlers to <see cref="INodeTranslator"/>: one handler per input exec pin (a node with a
    /// single handler uses it for every pin index, as pure nodes are always called with 0).
    /// </summary>
    private sealed class PerPinTranslator<TNode>(params Action<IExecutionTranslationContext, TNode>[] handlers) : INodeTranslator
        where TNode : Node
    {
        public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex)
        {
            if (node is not TNode typed)
            {
                throw new ArgumentException($"Expected a {typeof(TNode)} but got a {node.GetType()}.", nameof(node));
            }

            if (inputExecPinIndex < 0 || inputExecPinIndex >= handlers.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputExecPinIndex), inputExecPinIndex, $"{typeof(TNode)} has no translation for that input exec pin.");
            }

            handlers[inputExecPinIndex](context, typed);
        }
    }

    private static IEnumerable<string> PinNames(IExecutionTranslationContext context, IEnumerable<NodeOutputDataPin> pins) =>
        pins.Select(pin => context.GetOrCreatePinName(pin)).ToList();

    private static IEnumerable<string?> IncomingValues(IExecutionTranslationContext context, IEnumerable<NodeInputDataPin> pins) =>
        pins.Select(pin => context.GetPinIncomingValue(pin)).ToList();

    private static string RequiredIncomingValue(IExecutionTranslationContext context, NodeInputDataPin pin) =>
        context.GetPinIncomingValue(pin)
            ?? throw new InvalidOperationException($"Input data pin {pin} on {pin.Node} has no incoming value to emit.");

    private static NodeOutputDataPin ExceptionPinOf(CallMethodNode node) =>
        node.ExceptionPin ?? throw new InvalidOperationException($"{node} handles exceptions but has no exception pin.");

    private static string ResolvedTypeName(NodeDataPin pin) =>
        pin.PinType.Value?.FullCodeName
            ?? throw new InvalidOperationException($"The type of pin {pin} on {pin.Node} is not resolved.");

    private static IBuiltInTranslationContext BuiltInContext(IExecutionTranslationContext context) =>
        context as IBuiltInTranslationContext
            ?? throw new InvalidOperationException($"{context.GetType()} does not support the built-in node translators.");

    /// <summary>
    /// Registered handler for <see cref="MethodEntryNode"/>. Currently a no-op: the entry node's
    /// own state is never jumped to by anything but the implicit fallthrough <see cref="ExecutionGraphTranslator.Translate(ExecutionGraph, bool)"/>
    /// already writes before the first state label, so there is nothing left to emit here.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Entry node of the graph being translated.</param>
    internal static void TranslateMethodEntry(IExecutionTranslationContext context, MethodEntryNode node)
    {
    }

    /// <summary>
    /// Translates a call to the method (or, if <see cref="OperatorUtil.TryGetOperatorInfo"/>
    /// recognizes it, operator) described by <paramref name="node"/>. Emits its pure dependencies
    /// first, wraps the call in a try/catch when <see cref="CallMethodNode.HandlesExceptions"/> is
    /// set (assigning the caught exception to <see cref="CallMethodNode.ExceptionPin"/> and the
    /// return values to their defaults on the exception path), assigns single or tuple-wrapped
    /// return values, and advances execution to the next state unless the node is pure.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Call-method node to translate.</param>
    /// <exception cref="Exception">
    /// The resolved operator does not have exactly the argument count its arity requires.
    /// </exception>
    internal static void TranslateCallMethodNode(IExecutionTranslationContext context, CallMethodNode node)
    {
        // Wrap in try / catch
        if (node.HandlesExceptions)
        {
            context.AppendLine("try");
            context.AppendLine("{");
        }

        string? temporaryReturnName = null;

        if (!node.IsPure)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            context.TranslateDependentPureNodes(node);
        }

        // Write assignment of return values
        if (node.ReturnValuePins.Count == 1)
        {
            string returnName = context.GetOrCreatePinName(node.ReturnValuePins[0]);

            context.Append($"{returnName} = ");
        }
        else if (node.ReturnValuePins.Count > 1)
        {
            temporaryReturnName = context.CreateTemporaryVariableName();

            var returnTypeNames = string.Join(", ", node.ReturnValuePins.Select(ResolvedTypeName));

            context.Append($"{typeof(Tuple).FullName}<{returnTypeNames}> {temporaryReturnName} = ");
        }

        // Get arguments for method call
        var argumentNames = IncomingValues(context, node.ArgumentPins).ToList();

        // Check whether the method is an operator and we need to translate its name
        // into operator symbols. Otherwise just call the method normally.
        if (OperatorUtil.TryGetOperatorInfo(node.MethodSpecifier, out var operatorInfo))
        {
            Debug.Assert(!argumentNames.Any(a => a is null));

            if (operatorInfo.Unary)
            {
                if (argumentNames.Count != 1)
                {
                    throw new InvalidOperationException($"Unary operator was found but did not have one argument: {node.MethodName}");
                }

                if (operatorInfo.UnaryRightPosition)
                {
                    context.AppendLine($"{argumentNames[0]}{operatorInfo.Symbol};");
                }
                else
                {
                    context.AppendLine($"{operatorInfo.Symbol}{argumentNames[0]};");
                }
            }
            else
            {
                if (argumentNames.Count != 2)
                {
                    throw new InvalidOperationException($"Binary operator was found but did not have two arguments: {node.MethodName}");
                }

                context.AppendLine($"{argumentNames[0]}{operatorInfo.Symbol}{argumentNames[1]};");
            }
        }
        else
        {
            // Static: Write class name / target, default to own class name
            // Instance: Write target, default to this

            if (node.IsStatic)
            {
                context.Append($"{node.DeclaringType.FullCodeName}.");
            }
            else
            {
                if (node.TargetPin.IncomingPin != null)
                {
                    string targetName = context.GetOrCreatePinName(node.TargetPin.IncomingPin);
                    context.Append($"{targetName}.");
                }
                else
                {
                    // Default to this
                    context.Append("this.");
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
            context.AppendLine($"{node.BoundMethodName}({string.Join(", ", arguments)});");
        }

        // Assign the real variables from the temporary tuple
        if (node.ReturnValuePins.Count > 1)
        {
            var returnNames = PinNames(context, node.ReturnValuePins).ToList();
            for (int i = 0; i < returnNames.Count; i++)
            {
                context.AppendLine($"{returnNames[i]} = {temporaryReturnName}.Item{i + 1};");
            }
        }

        // Set the exception to null on success if catch pin is connected
        if (node.HandlesExceptions)
        {
            context.AppendLine($"{context.GetOrCreatePinName(ExceptionPinOf(node))} = null;");
        }

        // Go to the next state
        if (!node.IsPure)
        {
            context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
        }

        // Catch exceptions if catch pin is connected
        if (node.HandlesExceptions)
        {
            string exceptionVarName = context.CreateTemporaryVariableName();
            context.AppendLine("}");
            context.AppendLine($"catch (System.Exception {exceptionVarName})");
            context.AppendLine("{");
            context.AppendLine($"{context.GetOrCreatePinName(ExceptionPinOf(node))} = {exceptionVarName};");

            // Set all return values to default on exception
            foreach (var returnValuePin in node.ReturnValuePins)
            {
                string returnName = context.GetOrCreatePinName(returnValuePin);
                context.AppendLine($"{returnName} = default({ResolvedTypeName(returnValuePin)});");
            }

            if (!node.IsPure)
            {
                context.WriteGotoOutputPinIfNecessary(node.CatchPin, node.InputExecPins[0]);
            }

            context.AppendLine("}");
        }
    }

    /// <summary>
    /// Translates <paramref name="node"/> into a `new` expression: emits its pure dependencies,
    /// assigns the constructed instance to the node's output pin, and writes the constructor
    /// arguments (named and/or `out`/`ref`-prefixed as <see cref="TranslateCallMethodNode"/> does
    /// for method arguments). Advances execution to the next state unless the node is pure.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Constructor node to translate.</param>
    internal static void TranslateConstructorNode(IExecutionTranslationContext context, ConstructorNode node)
    {
        if (!node.IsPure)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            context.TranslateDependentPureNodes(node);
        }

        // Write assignment and constructor
        string returnName = context.GetOrCreatePinName(node.OutputDataPins[0]);
        context.Append($"{returnName} = new {node.ClassType}");

        // Write constructor arguments
        var argumentNames = IncomingValues(context, node.ArgumentPins);
        //context.AppendLine($"({string.Join(", ", argumentNames)});");

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
        context.AppendLine($"({string.Join(", ", arguments)});");

        if (!node.IsPure)
        {
            // Go to the next state
            context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
        }
    }

    /// <summary>
    /// Translates <paramref name="node"/> into either a hard cast (`(T)x`, throwing on failure) or
    /// an `as` cast with a null check branching to <see cref="ExplicitCastNode.CastFailedPin"/> /
    /// <see cref="ExplicitCastNode.CastSuccessPin"/>, depending on whether the failure pin is
    /// connected. Does nothing if <see cref="ExplicitCastNode.ObjectToCast"/> is unconnected.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Cast node to translate.</param>
    internal static void TranslateExplicitCastNode(IExecutionTranslationContext context, ExplicitCastNode node)
    {
        if (!node.IsPure)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            context.TranslateDependentPureNodes(node);
        }

        // Try to cast the incoming object and go to next states.
        if (node.ObjectToCast.IncomingPin != null)
        {
            // GetPinIncomingValue only returns null for an unconnected pin using its parameter's
            // default value; IncomingPin != null above rules that out here.
            string pinToCastName = RequiredIncomingValue(context, node.ObjectToCast);
            string outputName = context.GetOrCreatePinName(node.CastPin);

            // If failure pin is not connected write explicit cast that throws.
            // Otherwise check if cast object is null and execute failure
            // path if it is.
            if (node.IsPure || node.CastFailedPin.OutgoingPin == null)
            {
                context.AppendLine($"{outputName} = ({node.CastType.FullCodeNameUnbound}){pinToCastName};");

                if (!node.IsPure)
                {
                    context.WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                }
            }
            else
            {
                context.AppendLine($"{outputName} = {pinToCastName} as {node.CastType.FullCodeNameUnbound};");

                if (!node.IsPure)
                {
                    context.AppendLine($"if ({outputName} is null)");
                    context.AppendLine("{");
                    context.WriteGotoOutputPinIfNecessary(node.CastFailedPin, node.InputExecPins[0]);
                    context.AppendLine("}");
                    context.AppendLine("else");
                    context.AppendLine("{");
                    context.WriteGotoOutputPinIfNecessary(node.CastSuccessPin, node.InputExecPins[0]);
                    context.AppendLine("}");
                }
            }
        }
    }

    /// <summary>
    /// Translates <paramref name="node"/> into a `throw &lt;expression&gt;;` statement, after
    /// emitting its pure dependencies.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Throw node to translate.</param>
    internal static void TranslateThrowNode(IExecutionTranslationContext context, ThrowNode node)
    {
        context.TranslateDependentPureNodes(node);
        context.AppendLine($"throw {context.GetPinIncomingValue(node.ExceptionPin)};");
    }

    /// <summary>
    /// Translates <paramref name="node"/> into an `await &lt;task&gt;;` statement, after emitting
    /// its pure dependencies. Assigns the awaited result to <see cref="AwaitNode.ResultPin"/>'s
    /// variable first if the awaited task has one.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Await node to translate.</param>
    internal static void TranslateAwaitNode(IExecutionTranslationContext context, AwaitNode node)
    {
        if (!node.IsPure)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            context.TranslateDependentPureNodes(node);
        }

        // Store result if task has a return value.
        if (node.ResultPin != null)
        {
            context.Append($"{context.GetOrCreatePinName(node.ResultPin)} = ");
        }

        context.AppendLine($"await {context.GetPinIncomingValue(node.TaskPin)};");
    }

    /// <summary>
    /// Translates <paramref name="node"/> into a `condition ? trueValue : falseValue` assignment,
    /// after emitting its pure dependencies, and advances execution to the next state unless the
    /// node is pure.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Ternary node to translate.</param>
    internal static void TranslateTernaryNode(IExecutionTranslationContext context, TernaryNode node)
    {
        if (!node.IsPure)
        {
            // Translate all the pure nodes this node depends on in
            // the correct order
            context.TranslateDependentPureNodes(node);
        }

        context.Append($"{context.GetOrCreatePinName(node.OutputObjectPin)} = ");
        context.Append($"{context.GetPinIncomingValue(node.ConditionPin)} ? ");
        context.Append($"{context.GetPinIncomingValue(node.TrueObjectPin)} : ");
        context.AppendLine($"{context.GetPinIncomingValue(node.FalseObjectPin)};");

        if (!node.IsPure)
        {
            context.WriteGotoOutputPinIfNecessary(node.OutputExecPins.Single(), node.InputExecPins.Single());
        }
    }

    /// <summary>
    /// Translates <paramref name="node"/> into an assignment to the target variable, property or
    /// indexer (instance, static, or indexed by <see cref="VariableNode.IndexPin"/> when
    /// <see cref="VariableNode.IsIndexer"/> is set), after emitting its pure dependencies. Also
    /// assigns the same value to the node's output pin, and advances execution to the next state.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Variable-setter node to translate.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="node"/> is a static setter with no explicit target type and its graph has
    /// no declaring class.
    /// </exception>
    internal static void TranslateVariableSetterNode(IExecutionTranslationContext context, VariableSetterNode node)
    {
        // Translate all the pure nodes this node depends on in
        // the correct order
        context.TranslateDependentPureNodes(node);

        // The value pin does not use the explicit-default-value convention (that is only set on
        // CallMethodNode's argument pins), so this is never null.
        string valueName = RequiredIncomingValue(context, node.NewValuePin);

        // Add target name if there is a target (null for local and static variables)
        if (node.IsStatic)
        {
            if (!(node.TargetType is null))
            {
                context.Append(node.TargetType.FullCodeName);
            }
            else
            {
                var declaringClass = node.Graph.Class
                    ?? throw new InvalidOperationException("A static variable setter's graph has no class.");
                context.Append(declaringClass.Name);
            }
        }
        if (node.TargetPin != null)
        {
            if (node.TargetPin.IncomingPin != null)
            {
                string targetName = context.GetOrCreatePinName(node.TargetPin.IncomingPin);
                context.Append(targetName);
            }
            else
            {
                context.Append("this");
            }
        }

        // Add index if needed
        if (node.IsIndexer)
        {
            // IsIndexer implies IndexPin is not null (VariableNode.IndexPin).
            context.Append($"[{context.GetPinIncomingValue(node.IndexPin ?? throw new InvalidOperationException("An indexer node has no index pin."))}]");
        }
        else
        {
            context.Append($".{node.VariableName}");
        }

        context.AppendLine($" = {valueName};");

        // Set output pin of this node to the same value
        context.AppendLine($"{context.GetOrCreatePinName(node.OutputDataPins[0])} = {valueName};");

        // Go to the next state
        context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
    }

    /// <summary>
    /// Translates <paramref name="node"/> into a `return` statement, after emitting its pure
    /// dependencies: no value for a void or <see cref="Task"/>-returning method, the single input
    /// pin's value for one return value, or a tuple construction for more than one. Omits the
    /// bare `return;` entirely when the node has no return values and is the graph's last state
    /// (fallthrough already reaches the end of the method body).
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Return node to translate.</param>
    internal static void TranslateReturnNode(IExecutionTranslationContext context, ReturnNode node)
    {
        // Translate all the pure nodes this node depends on in
        // the correct order
        context.TranslateDependentPureNodes(node);

        if (node.InputDataPins.Count == 0)
        {
            // Only write return if the return node is not the last node
            if (!BuiltInContext(context).IsFinalExecState(node.InputExecPins[0]))
            {
                context.AppendLine(EarlyOutStatement);
            }
        }
        else if (node.InputDataPins.Count == 1)
        {
            // Special case for async functions returning Task (no return value)
            if (node.InputDataPins[0].PinType == TypeSpecifier.FromType<Task>())
            {
                context.AppendLine(EarlyOutStatement);
            }
            else
            {
                context.AppendLine($"return {context.GetPinIncomingValue(node.InputDataPins[0])};");
            }
        }
        else
        {
            var returnValues = node.InputDataPins.Select(pin => context.GetPinIncomingValue(pin));

            // Tuple<Types..> (won't be needed in the future)
            string returnType = typeof(Tuple).FullName + "<" + string.Join(", ", node.InputDataPins.Select(ResolvedTypeName)) + ">";
            context.AppendLine($"return new {returnType}({string.Join(", ", returnValues)});");
        }
    }

    /// <summary>
    /// Translates <paramref name="node"/> into an `if (condition) { ... } else { ... }` statement,
    /// after emitting its pure dependencies. Each branch either advances execution to the outgoing
    /// pin's target state or, when a branch's exec pin is unconnected, emits a bare `return;`.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">If/else node to translate.</param>
    internal static void TranslateIfElseNode(IExecutionTranslationContext context, IfElseNode node)
    {
        // Translate all the pure nodes this node depends on in
        // the correct order
        context.TranslateDependentPureNodes(node);

        // The condition pin does not use the explicit-default-value convention, so this is never
        // null (see GetPinIncomingValue).
        string conditionVar = RequiredIncomingValue(context, node.ConditionPin);

        context.AppendLine($"if ({conditionVar})");
        context.AppendLine("{");

        if (node.TruePin.OutgoingPin != null)
        {
            context.WriteGotoOutputPinIfNecessary(node.TruePin, node.InputExecPins[0]);
        }
        else
        {
            context.AppendLine(EarlyOutStatement);
        }

        context.AppendLine("}");

        context.AppendLine("else");
        context.AppendLine("{");

        if (node.FalsePin.OutgoingPin != null)
        {
            context.WriteGotoOutputPinIfNecessary(node.FalsePin, node.InputExecPins[0]);
        }
        else
        {
            context.AppendLine(EarlyOutStatement);
        }

        context.AppendLine("}");
    }

    /// <summary>
    /// Registered handler for the "start" input exec pin of <paramref name="node"/> (see
    /// the per-pin dispatch of <see cref="INodeTranslator"/>). Initializes the loop index from
    /// <see cref="ForLoopNode.InitialIndexPin"/>, and, while it is below
    /// <see cref="ForLoopNode.MaxIndexPin"/>, pushes the continue state onto the jump stack and
    /// enters the loop body, after emitting the node's pure dependencies.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">For-loop node to translate.</param>
    internal static void TranslateStartForLoopNode(IExecutionTranslationContext context, ForLoopNode node)
    {
        // Translate all the pure nodes this node depends on in
        // the correct order
        context.TranslateDependentPureNodes(node);

        context.AppendLine($"{context.GetOrCreatePinName(node.IndexPin)} = {context.GetPinIncomingValue(node.InitialIndexPin)};");
        context.AppendLine($"if ({context.GetOrCreatePinName(node.IndexPin)} < {context.GetPinIncomingValue(node.MaxIndexPin)})");
        context.AppendLine("{");
        context.WritePushJumpStack(node.ContinuePin);
        context.WriteGotoOutputPinIfNecessary(node.LoopPin, node.ExecutionPin);
        context.AppendLine("}");
    }

    /// <summary>
    /// Registered handler for the "continue" input exec pin of <paramref name="node"/> (see
    /// the per-pin dispatch of <see cref="INodeTranslator"/>). Increments the loop index and, while it
    /// is below <see cref="ForLoopNode.MaxIndexPin"/>, pushes the continue state onto the jump
    /// stack and re-enters the loop body; otherwise advances to
    /// <see cref="ForLoopNode.CompletedPin"/>. Emits the node's pure dependencies first.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">For-loop node to translate.</param>
    internal static void TranslateContinueForLoopNode(IExecutionTranslationContext context, ForLoopNode node)
    {
        // Translate all the pure nodes this node depends on in
        // the correct order
        context.TranslateDependentPureNodes(node);

        context.AppendLine($"{context.GetOrCreatePinName(node.IndexPin)}++;");
        context.AppendLine($"if ({context.GetOrCreatePinName(node.IndexPin)} < {context.GetPinIncomingValue(node.MaxIndexPin)})");
        context.AppendLine("{");
        context.WritePushJumpStack(node.ContinuePin);
        context.WriteGotoOutputPinIfNecessary(node.LoopPin, node.ContinuePin);
        context.AppendLine("}");

        context.WriteGotoOutputPinIfNecessary(node.CompletedPin, node.ContinuePin);
    }

    /// <summary>
    /// Translates <paramref name="node"/> into a read of the target variable, property or indexer
    /// (instance, static, or indexed by <see cref="VariableNode.IndexPin"/> when
    /// <see cref="VariableNode.IsIndexer"/> is set), assigned to the node's output pin.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Variable-getter node to translate.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="node"/> is a static getter with no explicit target type and its graph has
    /// no declaring class.
    /// </exception>
    internal static void PureTranslateVariableGetterNode(IExecutionTranslationContext context, VariableGetterNode node)
    {
        string valueName = context.GetOrCreatePinName(node.OutputDataPins[0]);

        context.Append($"{valueName} = ");

        if (node.IsStatic)
        {
            if (!(node.TargetType is null))
            {
                context.Append(node.TargetType.FullCodeName);
            }
            else
            {
                var declaringClass = node.Graph.Class
                    ?? throw new InvalidOperationException("A static variable getter's graph has no class.");
                context.Append(declaringClass.Name);
            }
        }
        else
        {
            if (node.TargetPin?.IncomingPin != null)
            {
                string targetName = context.GetOrCreatePinName(node.TargetPin.IncomingPin);
                context.Append(targetName);
            }
            else
            {
                // Default to this
                context.Append("this");
            }
        }

        // Add index if needed
        if (node.IsIndexer)
        {
            // IsIndexer implies IndexPin is not null (VariableNode.IndexPin).
            context.Append($"[{context.GetPinIncomingValue(node.IndexPin ?? throw new InvalidOperationException("An indexer node has no index pin."))}]");
        }
        else
        {
            context.Append($".{node.VariableName}");
        }

        context.AppendLine(";");
    }

    /// <summary>
    /// Translates <paramref name="node"/> by assigning its single input pin's value (its literal,
    /// or an incoming expression if connected) to the node's output pin.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Literal node to translate.</param>
    internal static void PureTranslateLiteralNode(IExecutionTranslationContext context, LiteralNode node)
    {
        context.AppendLine($"{context.GetOrCreatePinName(node.ValuePin)} = {context.GetPinIncomingValue(node.InputDataPins[0])};");
    }

    /// <summary>
    /// Translates <paramref name="node"/> by assigning a method-group expression (a delegate
    /// bound to a static or instance method) to the node's output pin.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Make-delegate node to translate.</param>
    internal static void PureTranslateMakeDelegateNode(IExecutionTranslationContext context, MakeDelegateNode node)
    {
        // Write assignment of return value
        string returnName = context.GetOrCreatePinName(node.OutputDataPins[0]);
        context.Append($"{returnName} = ");

        // Static: Write class name / target, default to own class name
        // Instance: Write target, default to this

        if (node.IsFromStaticMethod)
        {
            context.Append($"{node.MethodSpecifier.DeclaringType}.");
        }
        else
        {
            if (node.TargetPin.IncomingPin != null)
            {
                string targetName = context.GetOrCreatePinName(node.TargetPin.IncomingPin);
                context.Append($"{targetName}.");
            }
            else
            {
                // Default to thise
                context.Append("this.");
            }
        }

        // Write method name
        context.AppendLine($"{node.MethodSpecifier.Name};");
    }

    /// <summary>
    /// Translates <paramref name="node"/> by assigning `typeof(&lt;type&gt;)` to the node's output
    /// pin, using <see cref="object"/> as the type when the node's input type pin has not
    /// inferred a type.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Type-of node to translate.</param>
    internal static void PureTranslateTypeOfNode(IExecutionTranslationContext context, TypeOfNode node)
    {
        context.AppendLine($"{context.GetOrCreatePinName(node.TypePin)} = typeof({node.InputTypePin.InferredType?.Value?.FullCodeNameUnbound ?? "System.Object"});");
    }

    /// <summary>
    /// Translates <paramref name="node"/> into an array-creation expression assigned to the node's
    /// output pin: a predefined-size allocation (`new T[size]`) when
    /// <see cref="MakeArrayNode.UsePredefinedSize"/> is set, otherwise an initializer list built
    /// from the node's input data pins.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Make-array node to translate.</param>
    internal static void PureTranslateMakeArrayNode(IExecutionTranslationContext context, MakeArrayNode node)
    {
        string arrayTypeName = node.ArrayType.FullCodeName;
        context.Append($"{context.GetOrCreatePinName(node.OutputDataPins[0])} = new ");

        // Use predefined size or initializer list
        if (node.UsePredefinedSize)
        {
            // The size replaces the trailing "[]" of the array type's name.
            const int ArraySuffixLength = 2;
            context.Append(arrayTypeName[..^ArraySuffixLength]);
            context.AppendLine($"[{context.GetPinIncomingValue(node.SizePin)}];");
        }
        else
        {
            context.Append(arrayTypeName);
            context.AppendLine();
            context.AppendLine("{");

            foreach (var inputDataPin in node.InputDataPins)
            {
                context.AppendLine($"{context.GetPinIncomingValue(inputDataPin)},");
            }

            context.AppendLine("};");
        }
    }
    /// <summary>
    /// Translates <paramref name="node"/> by assigning `default(&lt;type&gt;)` to the node's
    /// output pin.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Default node to translate.</param>
    internal static void PureTranslateDefaultNode(IExecutionTranslationContext context, DefaultNode node)
    {
        context.AppendLine($"{context.GetOrCreatePinName(node.DefaultValuePin)} = default({node.Type.FullCodeName});");
    }

    /// <summary>
    /// Translates <paramref name="node"/> by passing its single connection through: assigns the
    /// incoming value to the output data pin for a data reroute, or advances execution to the next
    /// state for an exec reroute. A type reroute has no runtime representation and is a no-op here
    /// (type reroutes only affect type inference).
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">Reroute node to translate.</param>
    /// <exception cref="NotImplementedException">
    /// <paramref name="node"/> does not reroute exactly one pin: exactly one of
    /// <see cref="RerouteNode.ExecRerouteCount"/>, <see cref="RerouteNode.TypeRerouteCount"/> and
    /// <see cref="RerouteNode.DataRerouteCount"/> must be 1 and the others 0.
    /// </exception>
    internal static void TranslateRerouteNode(IExecutionTranslationContext context, RerouteNode node)
    {
        if (node.ExecRerouteCount + node.TypeRerouteCount + node.DataRerouteCount != 1)
        {
            throw new NotImplementedException("Only implemented reroute nodes with exactly 1 type of pin.");
        }

        if (node.DataRerouteCount == 1)
        {
            context.AppendLine($"{context.GetOrCreatePinName(node.OutputDataPins[0])} = {context.GetPinIncomingValue(node.InputDataPins[0])};");
        }
        else if (node.ExecRerouteCount == 1)
        {
            context.WriteGotoOutputPinIfNecessary(node.OutputExecPins[0], node.InputExecPins[0]);
        }
    }
}
