using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Extensibility;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Characterization
{
    /// <summary>
    /// Bug fix (implementation-notes.md "InitialIndexPin default-value bug"): the golden
    /// <c>AllNodes.Everything.cs</c> captured invalid C# (<c>varIndex = ;</c>) undetected for a long time
    /// because <see cref="GoldenCSharpTests"/>, <c>RoundTripTests</c> and <c>EmitterTests</c> only
    /// string-compare a translation against the golden file, never compile either one. This Roslyn-compiles
    /// (<see cref="ExtensionTestSupport.Compile"/>) every <c>Fixtures/Golden/*.cs</c> body that is
    /// standalone-compilable, to catch a future golden capturing invalid C# the same way.
    /// <c>EventGraphs.GameEvents.cs</c> is exercised separately below because it needs its hand-written
    /// <c>EventBase.cs</c> companion.
    /// </summary>
    public class GoldenCompileTests
    {
        private static string GoldenDir =>
            Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden");

        public static IEnumerable<object[]> StandaloneCompilableGoldens()
        {
            yield return new object[] { "HelloWorld.Program.cs" };
            yield return new object[] { "Locals.cs" };
            yield return new object[] { "AllNodes.Everything.cs" };
            yield return new object[] { "EventArguments.Combat.cs" };
        }

        [Theory]
        [MemberData(nameof(StandaloneCompilableGoldens))]
        public void GoldenCompilesWithNoErrors(string goldenFileName)
        {
            string source = File.ReadAllText(Path.Combine(GoldenDir, goldenFileName));
            string folder = ExtensionTestSupport.NewTempDirectory();

            ExtensionTestSupport.Compile(folder, Path.GetFileNameWithoutExtension(goldenFileName), source);
        }

        [Fact]
        public void EventGraphsGoldenCompilesWithItsEventBaseCompanion()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string gameEvents = File.ReadAllText(Path.Combine(GoldenDir, "EventGraphs.GameEvents.cs"));
            string eventBase = File.ReadAllText(Path.Combine(root, "tests", "NetPrints.Core.Tests", "Fixtures", "EventGraphs", "EventBase.cs"));
            string folder = ExtensionTestSupport.NewTempDirectory();

            ExtensionTestSupport.Compile(folder, "EventGraphs.GameEvents", gameEvents + eventBase);
        }

        /// <summary>
        /// Regression guard for the fixed bug: a minimal class around a <see cref="ForLoopNode"/> whose
        /// <see cref="ForLoopNode.InitialIndexPin"/> is left unconnected, translated and Roslyn-compiled.
        /// Before the fix this failed to compile (CS1525, the emitted <c>idx = ;</c>) even though no
        /// existing golden or fixture caught it; the four <c>AllNodes.Everything</c> golden tests only
        /// caught it as a string mismatch once the golden itself was corrected (see the "Bug fixed" entry
        /// in implementation-notes.md).
        /// </summary>
        [Fact]
        public void ForLoopWithUnconnectedInitialIndexCompiles()
        {
            ClassGraph cls = new ClassGraph()
            {
                Name = "ForLoopFixture",
                Namespace = "GoldenCompileTests",
            };

            MethodGraph runMethod = new MethodGraph("Run")
            {
                Class = cls,
                Visibility = MemberVisibility.Public,
            };

            LiteralNode maxIndexLiteralNode = LiteralNode.WithValue(runMethod, 10);
            ForLoopNode forLoopNode = new ForLoopNode(runMethod);

            GraphUtil.ConnectExecPins(runMethod.EntryNode.InitialExecutionPin, forLoopNode.ExecutionPin);
            GraphUtil.ConnectExecPins(forLoopNode.CompletedPin, runMethod.ReturnNodes.First().ReturnPin);
            GraphUtil.ConnectDataPins(maxIndexLiteralNode.ValuePin, forLoopNode.MaxIndexPin);

            cls.Methods.Add(runMethod);

            string translated = new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(cls);
            string folder = ExtensionTestSupport.NewTempDirectory();

            ExtensionTestSupport.Compile(folder, "ForLoopFixture", translated);
        }
    }
}
