using System.Collections.Generic;
using NetPrints.Core;
using NetPrints.Graph;
using Xunit;

namespace NetPrints.Tests.Graph
{
    /// <summary>R13: <see cref="MemberKey"/> compares its parameter types by value.</summary>
    public class MemberKeyTests
    {
        private static readonly TypeSpecifier Owner = TypeSpecifier.FromType<MemberKeyTests>();

        private static MemberKey Key(string name, params BaseType[] parameters) =>
            new(MemberKind.Method, Owner, name, new List<BaseType>(parameters));

        [Fact]
        public void KeysWithTheSameParameterTypesInDifferentListsAreEqual()
        {
            MemberKey first = Key("Run", TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<string>());
            MemberKey second = Key("Run", TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<string>());

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.Single(new HashSet<MemberKey> { first, second });
        }

        [Fact]
        public void KeysThatDifferInParameterTypesOrOrderAreNotEqual()
        {
            MemberKey key = Key("Run", TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<string>());

            Assert.NotEqual(key, Key("Run", TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<int>()));
            Assert.NotEqual(key, Key("Run", TypeSpecifier.FromType<int>()));
            Assert.NotEqual(key, Key("Other", TypeSpecifier.FromType<int>(), TypeSpecifier.FromType<string>()));
        }

        [Fact]
        public void TheDefaultKeyEqualsItselfAndHashesWithoutThrowing()
        {
            MemberKey key = default;

            Assert.Equal(default, key);
            Assert.Equal(key.GetHashCode(), default(MemberKey).GetHashCode());
            Assert.NotEqual(key, Key("Run"));
        }
    }
}
