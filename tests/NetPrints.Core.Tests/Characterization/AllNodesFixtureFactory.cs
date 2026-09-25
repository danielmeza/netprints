using System;
using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// Builds the "AllNodes" legacy fixture through the current (pre-P1) Core API: a single class
    /// <c>AllNodes.Everything</c> that uses every one of the 23 built-in node kinds (document-format.md
    /// §1.5, excluding the new <c>eventEntry</c> kind) at least once, plus generic types, editable pin
    /// renames, unconnected literal values of every supported primitive kind and both reference kinds.
    /// </summary>
    public static class AllNodesFixtureFactory
    {
        public const string RegenerateVariable = "NETPRINTS_REGENERATE_SAMPLES";

        private const double GridCellSize = 28;

        /// <summary>
        /// A grid-position generator local to one <see cref="CreateAllNodes"/> call (not shared,
        /// static, mutable state: xUnit v3 runs test classes in parallel by default).
        /// </summary>
        private sealed class PositionCursor
        {
            private double nextX = GridCellSize * 2;
            private double nextY = GridCellSize * 2;

            public void Place(Node node)
            {
                node.PositionX = nextX;
                node.PositionY = nextY;

                nextX += GridCellSize * 8;
                if (nextX > GridCellSize * 80)
                {
                    nextX = GridCellSize * 2;
                    nextY += GridCellSize * 8;
                }
            }
        }

        /// <summary>
        /// Creates the AllNodes project: an executable whose class <c>Everything</c> exercises every
        /// built-in node kind.
        /// </summary>
        /// <param name="projectPath">Path of the <c>.netpp</c> file the project will be saved to.</param>
        public static Project CreateAllNodes(string projectPath)
        {
            var cursor = new PositionCursor();

            Project project = Project.CreateNew("AllNodes", "AllNodes");
            project.Path = projectPath;
            project.OutputBinaryType = BinaryType.SharedLibrary;

            var cls = new ClassGraph()
            {
                Name = "Everything",
                Namespace = "AllNodes",
                Visibility = MemberVisibility.Public,
                Project = project,
            };

            cursor.Place(cls.ReturnNode);
            // classReturn: a class interface pin (§1.5 classReturn).
            cls.ReturnNode.AddInterfacePin();

            // A declared generic class parameter, used below to build a generic type (List<T>) in a
            // variable's type graph (GraphUtil.CreateNestedTypeNode only resolves nested type nodes for
            // *unbound* generic arguments; a closed generic such as List<string> is not supported there).
            var classGenericArgument = new GenericType("T");
            cls.DeclaredGenericArguments.Add(classGenericArgument);

            // A fixed (not project-path-derived, so the factory output stays reproducible) assembly
            // and source directory reference (removed again in T063 once the legacy reference model
            // is gone). Neither path needs to exist: CompilationReference stores it without validating.
            project.References.Add(new AssemblyReference("ExternalLibrary.dll"));
            project.References.Add(new SourceDirectoryReference("ExternalSources"));

            AddItemsVariable(cls, project, classGenericArgument, cursor);
            AddConstructor(cls, project, cursor);
            AddMainMethod(cls, project, cursor);

            project.Classes.Add(cls);

            return project;
        }

        /// <summary>
        /// A static class variable with a getter and setter (variableGetter, variableSetter) whose type
        /// graph builds a generic <c>List&lt;string&gt;</c> (a generic type built in its type graph).
        /// </summary>
        private static void AddItemsVariable(ClassGraph cls, Project project, GenericType classGenericArgument, PositionCursor cursor)
        {
            TypeSpecifier itemsType = TypeSpecifier.FromType(typeof(List<>));
            itemsType.GenericArguments[0] = classGenericArgument;

            var itemsVar = new Variable(cls, "Items", itemsType, null, null, VariableModifiers.Static);

            var getItems = new MethodGraph("get_Items")
            {
                Class = cls,
                Project = project,
                Visibility = MemberVisibility.Public,
                Modifiers = MethodModifiers.Static,
            };
            cursor.Place(getItems.EntryNode);
            cursor.Place(getItems.MainReturnNode);

            var getReturnTypeNode = new TypeNode(getItems, itemsType);
            cursor.Place(getReturnTypeNode);
            getItems.MainReturnNode.AddReturnType();
            GraphUtil.ConnectTypePins(getReturnTypeNode.OutputTypePins[0], getItems.MainReturnNode.InputTypePins[0]);

            var getItemsNode = new VariableGetterNode(getItems, itemsVar.Specifier);
            cursor.Place(getItemsNode);
            GraphUtil.ConnectExecPins(getItems.EntryNode.InitialExecutionPin, getItems.MainReturnNode.ReturnPin);
            GraphUtil.ConnectDataPins(getItemsNode.ValuePin, getItems.MainReturnNode.InputDataPins[0]);

            var setItems = new MethodGraph("set_Items")
            {
                Class = cls,
                Project = project,
                Visibility = MemberVisibility.Public,
                Modifiers = MethodModifiers.Static,
            };
            cursor.Place(setItems.EntryNode);
            cursor.Place(setItems.MainReturnNode);

            var setArgTypeNode = new TypeNode(setItems, itemsType);
            cursor.Place(setArgTypeNode);
            ((MethodEntryNode)setItems.EntryNode).AddArgument();
            GraphUtil.ConnectTypePins(setArgTypeNode.OutputTypePins[0], setItems.EntryNode.InputTypePins[0]);

            var setItemsNode = new VariableSetterNode(setItems, itemsVar.Specifier);
            cursor.Place(setItemsNode);
            GraphUtil.ConnectExecPins(setItems.EntryNode.InitialExecutionPin, setItemsNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(setItemsNode.OutputExecPins[0], setItems.MainReturnNode.ReturnPin);
            GraphUtil.ConnectDataPins(setItems.EntryNode.OutputDataPins[0], setItemsNode.NewValuePin);

            itemsVar.GetterMethod = getItems;
            itemsVar.SetterMethod = setItems;

            cls.Variables.Add(itemsVar);
        }

        /// <summary>A constructor (constructorEntry).</summary>
        private static void AddConstructor(ClassGraph cls, Project project, PositionCursor cursor)
        {
            var ctor = new ConstructorGraph
            {
                Class = cls,
                Project = project,
                Visibility = MemberVisibility.Public,
            };
            cursor.Place(ctor.EntryNode);

            cls.Constructors.Add(ctor);
        }

        private static void AddMainMethod(ClassGraph cls, Project project, PositionCursor cursor)
        {
            var main = new MethodGraph("Main")
            {
                Class = cls,
                Project = project,
                Visibility = MemberVisibility.Public,
                Modifiers = MethodModifiers.Static,
            };
            cursor.Place(main.EntryNode);
            cursor.Place(main.MainReturnNode);

            // methodEntry: 2 arguments, 1 generic argument.
            var argTypeNodeA = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(argTypeNodeA);
            var argTypeNodeB = new TypeNode(main, TypeSpecifier.FromType<string>());
            cursor.Place(argTypeNodeB);

            var entry = (MethodEntryNode)main.EntryNode;
            entry.AddArgument();
            entry.AddArgument();
            GraphUtil.ConnectTypePins(argTypeNodeA.OutputTypePins[0], entry.InputTypePins[0]);
            GraphUtil.ConnectTypePins(argTypeNodeB.OutputTypePins[0], entry.InputTypePins[1]);
            entry.AddGenericArgument();

            // A renamed editable pin (an argument pin renamed by the user).
            entry.OutputDataPins[0].Name = "first";

            // return: 2 return values.
            var returnTypeNodeA = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(returnTypeNodeA);
            var returnTypeNodeB = new TypeNode(main, TypeSpecifier.FromType<string>());
            cursor.Place(returnTypeNodeB);
            main.MainReturnNode.AddReturnType();
            main.MainReturnNode.AddReturnType();
            GraphUtil.ConnectTypePins(returnTypeNodeA.OutputTypePins[0], main.MainReturnNode.InputTypePins[0]);
            GraphUtil.ConnectTypePins(returnTypeNodeB.OutputTypePins[0], main.MainReturnNode.InputTypePins[1]);

            // A renamed editable pin (a return pin renamed by the user).
            main.MainReturnNode.InputDataPins[0].Name = "resultCode";

            main.MainReturnNode.InputDataPins[0].UnconnectedValue = 0;
            main.MainReturnNode.InputDataPins[1].UnconnectedValue = "";

            // ifElse, driving the reachable exec chain to the return node. The condition is an
            // unconnected value (not a connected literal node): LiteralNode.OnInputTypeChanged
            // (unmodified) disconnects its value pin whenever the method-graph relaxation pass
            // recomputes its constructed type, which happens on every load, including one with no
            // generic arguments (GenericsHelper.ConstructWithTypePins always returns a new
            // TypeSpecifier instance, compared by reference); see implementation-notes.md.
            var ifElse = new IfElseNode(main);
            cursor.Place(ifElse);
            ifElse.ConditionPin.UnconnectedValue = true;
            GraphUtil.ConnectExecPins(main.EntryNode.InitialExecutionPin, ifElse.ExecutionPin);

            // forLoop on the true branch, straight back to the return node.
            var forLoop = new ForLoopNode(main);
            cursor.Place(forLoop);
            forLoop.MaxIndexPin.UnconnectedValue = 10;
            GraphUtil.ConnectExecPins(ifElse.TruePin, forLoop.ExecutionPin);
            GraphUtil.ConnectExecPins(forLoop.CompletedPin, main.MainReturnNode.ReturnPin);

            // await, unreferenced (its task pin would need a real Task-returning connection to be
            // reachable; presence of the node kind is what T002 requires).
            var awaitNode = new AwaitNode(main);
            cursor.Place(awaitNode);

            // throw on the false branch (terminal, no outgoing exec pin).
            var throwNode = new ThrowNode(main);
            cursor.Place(throwNode);
            var exceptionConstructor = new ConstructorNode(main, new ConstructorSpecifier(Array.Empty<MethodParameter>(), TypeSpecifier.FromType<Exception>()));
            cursor.Place(exceptionConstructor);
            GraphUtil.ConnectDataPins(exceptionConstructor.OutputDataPins[0], throwNode.ExceptionPin);
            GraphUtil.ConnectExecPins(ifElse.FalsePin, throwNode.InputExecPins[0]);

            // ternary, unreferenced (not required to be reachable to appear in the document).
            var ternary = new TernaryNode(main);
            cursor.Place(ternary);
            var ternaryTypeNode = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(ternaryTypeNode);
            GraphUtil.ConnectTypePins(ternaryTypeNode.OutputTypePins[0], ternary.TypePin);
            ternary.ConditionPin.UnconnectedValue = true;
            ternary.TrueObjectPin.UnconnectedValue = 1;
            ternary.FalseObjectPin.UnconnectedValue = 2;

            // explicitCast, unreferenced.
            var explicitCast = new ExplicitCastNode(main);
            cursor.Place(explicitCast);
            var castTypeNode = new TypeNode(main, TypeSpecifier.FromType<string>());
            cursor.Place(castTypeNode);
            GraphUtil.ConnectTypePins(castTypeNode.OutputTypePins[0], explicitCast.CastTypePin);

            // callMethod and makeDelegate, both unreferenced (their string argument would need a
            // real connection or unconnected value to be safely reachable).
            var writeLine = new MethodSpecifier("WriteLine",
                new[] { new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null) },
                Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public,
                TypeSpecifier.FromType(typeof(Console)), Array.Empty<BaseType>());
            var callMethod = new CallMethodNode(main, writeLine);
            cursor.Place(callMethod);
            var makeDelegate = new MakeDelegateNode(main, writeLine);
            cursor.Place(makeDelegate);

            // type: a standalone type node (generic).
            var listOfIntType = TypeSpecifier.FromType(typeof(List<>));
            listOfIntType.GenericArguments[0] = TypeSpecifier.FromType<int>();
            var standaloneTypeNode = new TypeNode(main, listOfIntType);
            cursor.Place(standaloneTypeNode);

            // makeArrayType.
            var makeArrayType = new MakeArrayTypeNode(main);
            cursor.Place(makeArrayType);
            var arrayElementType = new TypeNode(main, TypeSpecifier.FromType<double>());
            cursor.Place(arrayElementType);
            GraphUtil.ConnectTypePins(arrayElementType.OutputTypePins[0], makeArrayType.InputTypePins[0]);

            // makeArray: one with a predefined size, one with 3 element pins.
            var makeArrayPredefined = new MakeArrayNode(main);
            cursor.Place(makeArrayPredefined);
            var predefinedElementType = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(predefinedElementType);
            GraphUtil.ConnectTypePins(predefinedElementType.OutputTypePins[0], makeArrayPredefined.ElementTypePin);
            makeArrayPredefined.UsePredefinedSize = true;
            makeArrayPredefined.SizePin.UnconnectedValue = 3;

            var makeArrayElements = new MakeArrayNode(main);
            cursor.Place(makeArrayElements);
            var elementsElementType = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(elementsElementType);
            GraphUtil.ConnectTypePins(elementsElementType.OutputTypePins[0], makeArrayElements.ElementTypePin);
            makeArrayElements.AddElementPin();
            makeArrayElements.AddElementPin();
            makeArrayElements.AddElementPin();

            // typeOf.
            var typeOf = new TypeOfNode(main);
            cursor.Place(typeOf);
            var typeOfTypeNode = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(typeOfTypeNode);
            GraphUtil.ConnectTypePins(typeOfTypeNode.OutputTypePins[0], typeOf.InputTypePin);

            // default.
            var defaultNode = new DefaultNode(main);
            cursor.Place(defaultNode);
            var defaultTypeNode = new TypeNode(main, TypeSpecifier.FromType<int>());
            cursor.Place(defaultTypeNode);
            GraphUtil.ConnectTypePins(defaultTypeNode.OutputTypePins[0], defaultNode.TypePin);

            // reroute: data, exec and type.
            var dataReroute = RerouteNode.MakeData(main, new[] { Tuple.Create<BaseType, BaseType>(TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<int>()) });
            cursor.Place(dataReroute);
            var execReroute = RerouteNode.MakeExecution(main, 1);
            cursor.Place(execReroute);
            var typeReroute = RerouteNode.MakeType(main, 1);
            cursor.Place(typeReroute);

            // Unconnected literal values of every supported primitive kind, plus an enum.
            cursor.Place(LiteralNode.WithValue(main, true));
            cursor.Place(LiteralNode.WithValue(main, 42));
            cursor.Place(LiteralNode.WithValue(main, 42L));
            cursor.Place(LiteralNode.WithValue(main, double.NaN));
            cursor.Place(LiteralNode.WithValue(main, 'x'));
            cursor.Place(LiteralNode.WithValue(main, "Quotes \" and a\nnewline"));

            // decimal: NodeInputDataPin.UsesUnconnectedValue (unmodified) gates on TypeSpecifier.IsPrimitive,
            // which does not include System.Decimal, so an unconnected value cannot be set on today's
            // model; the node kind is still exercised (implementation-notes.md).
            cursor.Place(new LiteralNode(main, TypeSpecifier.FromType<decimal>()));

            var enumLiteral = new LiteralNode(main, new TypeSpecifier("System.DayOfWeek", isEnum: true));
            cursor.Place(enumLiteral);
            enumLiteral.InputValuePin.UnconnectedValue = "Monday";

            cls.Methods.Add(main);
        }
    }
}
