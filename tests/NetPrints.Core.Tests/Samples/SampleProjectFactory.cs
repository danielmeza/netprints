using NetPrints.Core;
using NetPrints.Graph;
using System;
using System.IO;
using System.Linq;

namespace NetPrints.Tests.Samples
{
    /// <summary>
    /// Builds the checked-in sample projects through the Core API (FR-018).
    /// </summary>
    public static class SampleProjectFactory
    {
        public const string RegenerateVariable = "NETPRINTS_REGENERATE_SAMPLES";

        private const double GridCellSize = 28;

        /// <summary>
        /// Creates the HelloWorld project: an executable whose static <c>Program.Main</c> calls
        /// <c>System.Console.WriteLine("Hello, World!")</c>.
        /// </summary>
        /// <param name="projectPath">Path of the <c>.netpp</c> file the project will be saved to.</param>
        public static Project CreateHelloWorld(string projectPath)
        {
            Project project = Project.CreateNew("HelloWorld", "HelloWorld");
            project.Path = projectPath;
            project.OutputBinaryType = BinaryType.Executable;

            var cls = new ClassGraph()
            {
                Name = "Program",
                Namespace = "HelloWorld",
                Visibility = MemberVisibility.Public,
                Project = project,
            };

            cls.ReturnNode.PositionX = GridCellSize * 4;
            cls.ReturnNode.PositionY = GridCellSize * 4;

            var main = new MethodGraph("Main")
            {
                Class = cls,
                Visibility = MemberVisibility.Public,
                Modifiers = MethodModifiers.Static,
            };

            main.EntryNode.PositionX = GridCellSize * 4;
            main.EntryNode.PositionY = GridCellSize * 4;
            main.MainReturnNode.PositionX = GridCellSize * 30;
            main.MainReturnNode.PositionY = GridCellSize * 4;

            TypeSpecifier stringType = TypeSpecifier.FromType<string>();
            var writeLine = new MethodSpecifier("WriteLine",
                new[] { new MethodParameter("value", stringType, MethodParameterPassType.Default, false, null) },
                Array.Empty<BaseType>(), MethodModifiers.Static, MemberVisibility.Public,
                TypeSpecifier.FromType(typeof(Console)), Array.Empty<BaseType>());

            var writeLineNode = new CallMethodNode(main, writeLine)
            {
                PositionX = GridCellSize * 15,
                PositionY = GridCellSize * 4,
            };

            writeLineNode.ArgumentPins.Single().UnconnectedValue = "Hello, World!";

            GraphUtil.ConnectExecPins(main.EntryNode.InitialExecutionPin, writeLineNode.InputExecPins[0]);
            GraphUtil.ConnectExecPins(writeLineNode.OutputExecPins[0], main.MainReturnNode.ReturnPin);

            cls.Methods.Add(main);
            project.Classes.Add(cls);

            return project;
        }

        /// <summary>
        /// Finds the repository root (the directory containing <c>NetPrints.slnx</c>).
        /// </summary>
        public static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NetPrints.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}
