using System;
using System.IO;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using NetPrints.Translator;
using Xunit;

namespace NetPrints.Tests.Translator
{
    /// <summary>
    /// US8 (FR-073): the method of a custom event entry has the entry's arguments as parameters, in order. The
    /// golden <c>EventArguments.Combat.cs</c> was added on purpose in T082; no other golden changes.
    /// </summary>
    public class EventArgumentsGoldenTests
    {
        private static ClassGraph BuildCombat()
        {
            var cls = new ClassGraph { Name = "Combat", Namespace = "EventArguments", Visibility = MemberVisibility.Public };
            var events = new EventGraph("Events") { Class = cls };
            cls.EventGraphs.Add(events);
            new EventEntryNode(events, "OnReset");
            new EventEntryNode(events, "OnHit").SetArguments(
            [
                new EventArgument("amount", TypeSpecifier.FromType<int>()),
                new EventArgument("source", TypeSpecifier.FromType<string>()),
                new EventArgument("critical", TypeSpecifier.FromType<bool>()),
            ]);
            return cls;
        }

        [Fact]
        public void TheEntryArgumentsAreTheMethodParametersInOrder()
        {
            string translated = new ClassTranslator(TranslationEnvironment.BuiltIn).TranslateClass(BuildCombat());

            string goldenPath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.Core.Tests", "Fixtures", "Golden", "EventArguments.Combat.cs");
            if (Environment.GetEnvironmentVariable(GoldenCSharpTests.UpdateSnapshotsVariable) == "1")
            {
                File.WriteAllText(goldenPath, translated);
            }

            Assert.True(File.Exists(goldenPath), $"Missing golden file {goldenPath}; regenerate with {GoldenCSharpTests.UpdateSnapshotsVariable}=1");
            Assert.Equal(File.ReadAllText(goldenPath), translated);
        }
    }
}
