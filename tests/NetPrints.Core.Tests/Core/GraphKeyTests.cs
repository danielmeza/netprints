using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Tests.Characterization;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>DF-T22, model part: <see cref="GraphKeys"/>, <see cref="ClassGraph.EnsureUniqueMemberIds"/>
    /// and <see cref="ClassGraph.AssignLegacyMemberIds"/>.</summary>
    public class GraphKeyTests
    {
        /// <summary>Returns a fixed member id for every 'm'-prefixed request (so several members can be
        /// forced to collide) while leaving node ids ('n') random, so building a graph does not
        /// exhaust a short, hand-written queue.</summary>
        private sealed class FixedMemberIdGenerator : IIdGenerator
        {
            private readonly string memberId;

            public FixedMemberIdGenerator(string memberId)
            {
                this.memberId = memberId;
            }

            public string NewId(char prefix) => prefix == 'm' ? memberId : RandomIdGenerator.Instance.NewId(prefix);
        }

        private static IEnumerable<NodeGraph> AllGraphsOf(ClassGraph cls)
        {
            yield return cls;

            foreach (Variable variable in cls.Variables)
            {
                yield return variable.TypeGraph;

                if (variable.GetterMethod is not null)
                {
                    yield return variable.GetterMethod;
                }

                if (variable.SetterMethod is not null)
                {
                    yield return variable.SetterMethod;
                }
            }

            foreach (var method in cls.Methods)
            {
                yield return method;
            }

            foreach (var constructor in cls.Constructors)
            {
                yield return constructor;
            }
        }

        [Fact]
        public void ForAndResolveRoundTripEveryGraphOfAllNodes()
        {
            string projectPath = Path.Combine(Path.GetTempPath(), "netprints-graphkeys-" + Guid.NewGuid().ToString("N"), "AllNodes.netpp");
            Project project = AllNodesFixtureFactory.CreateAllNodes(projectPath);
            ClassGraph cls = project.Classes.Single();

            foreach (NodeGraph graph in AllGraphsOf(cls))
            {
                string key = GraphKeys.For(graph);
                Assert.Same(graph, GraphKeys.Resolve(cls, key));
            }
        }

        [Fact]
        public void KeysAreUnchangedAfterReorderingMethods()
        {
            var cls = new ClassGraph { Name = "C" };
            var first = new MethodGraph("First") { Class = cls };
            var second = new MethodGraph("Second") { Class = cls };
            cls.Methods.Add(first);
            cls.Methods.Add(second);

            string firstKeyBefore = GraphKeys.For(first);
            string secondKeyBefore = GraphKeys.For(second);

            cls.Methods.Clear();
            cls.Methods.Add(second);
            cls.Methods.Add(first);

            Assert.Equal(firstKeyBefore, GraphKeys.For(first));
            Assert.Equal(secondKeyBefore, GraphKeys.For(second));
        }

        [Fact]
        public void AssignLegacyMemberIdsGivesTheSameIdsForTwoLoadsOfTheSameLegacyClass()
        {
            string fixturePath = Path.Combine(SampleProjectFactory.FindRepositoryRoot(),
                "tests", "NetPrints.Core.Tests", "Fixtures", "Legacy", "AllNodes", "AllNodes.netpp");

            Project? firstLoad = Project.LoadFromPath(fixturePath);
            Project? secondLoad = Project.LoadFromPath(fixturePath);
            Assert.NotNull(firstLoad);
            Assert.NotNull(secondLoad);

            ClassGraph firstClass = firstLoad.Classes.Single();
            ClassGraph secondClass = secondLoad.Classes.Single();

            firstClass.AssignLegacyMemberIds();
            secondClass.AssignLegacyMemberIds();

            var firstIds = firstClass.Members.Select(m => GetId(m)).ToList();
            var secondIds = secondClass.Members.Select(m => GetId(m)).ToList();

            Assert.Equal(firstIds, secondIds);
        }

        private static string GetId(object member) => member switch
        {
            Variable v => v.Id,
            MethodGraph m => m.Id,
            ConstructorGraph c => c.Id,
            _ => throw new InvalidOperationException($"Unknown member type '{member.GetType()}'."),
        };

        [Fact]
        public void EnsureUniqueMemberIdsRenamesOnlyTheLaterDuplicate()
        {
            var cls = new ClassGraph { Name = "C" };
            MethodGraph first;
            MethodGraph second;

            using (IdGeneration.Use(new FixedMemberIdGenerator("mdup0001")))
            {
                first = new MethodGraph("First") { Class = cls };
                second = new MethodGraph("Second") { Class = cls };
            }

            cls.Methods.Add(first);
            cls.Methods.Add(second);

            Assert.Equal(first.Id, second.Id);

            bool changed = cls.EnsureUniqueMemberIds();

            Assert.True(changed);
            Assert.Equal("mdup0001", first.Id);
            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public void EnsureUniqueMemberIdsReturnsFalseWhenNothingChanges()
        {
            var cls = new ClassGraph { Name = "C" };
            var first = new MethodGraph("First") { Class = cls };
            var second = new MethodGraph("Second") { Class = cls };
            cls.Methods.Add(first);
            cls.Methods.Add(second);

            Assert.False(cls.EnsureUniqueMemberIds());
        }

        [Fact]
        public void ForThrowsForADetachedGraph()
        {
            var method = new MethodGraph("Detached");

            Assert.Throws<InvalidOperationException>(() => GraphKeys.For(method));
        }
    }
}
