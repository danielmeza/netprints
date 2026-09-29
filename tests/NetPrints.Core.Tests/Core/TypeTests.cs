using System.Collections.Generic;
using NetPrints.Core;
using Xunit;

namespace NetPrintsUnitTests
{
    public class TypeTests
    {
        [Fact]
        public void TestEquality()
        {
            TypeSpecifier typeA = new TypeSpecifier("TypeA");
            TypeSpecifier typeB = new TypeSpecifier("TypeB");
            TypeSpecifier sameAsTypeA = new TypeSpecifier("TypeA");

            Assert.NotEqual(typeA, typeB);
            Assert.Equal(typeA, sameAsTypeA);
            Assert.NotEqual(sameAsTypeA, typeB);
        }

        // A bound TypeSpecifier and an unbound GenericType parameter are never equal, in either
        // direction, even when the generic type's name happens to match the specifier's (T103a):
        // GenericType.Equals(TypeSpecifier) and TypeSpecifier.Equals(GenericType) used to both return
        // true unconditionally, disagreeing with GetHashCode and giving every TypeSpecifier the same
        // "equal" GenericType instance regardless of name. GraphUtil.CanConnectNodePins checks
        // generic/concrete pin compatibility directly instead of relying on this equality.
        [Fact]
        public void TypeSpecifierAndGenericTypeAreNeverEqual()
        {
            TypeSpecifier typeA = new TypeSpecifier("TypeA", false, false, new BaseType[] { });
            GenericType genType1 = new GenericType("T1");
            GenericType genType2 = new GenericType("TypeA");

            Assert.NotEqual<BaseType>(typeA, genType1);
            Assert.NotEqual<BaseType>(typeA, genType2);
            Assert.NotEqual<BaseType>(genType1, typeA);
            Assert.NotEqual<BaseType>(genType2, typeA);
            Assert.NotEqual(genType1, genType2);
        }

        // GetHashCode consistency (T103a): two values Equals says are equal must return the same hash
        // code. Since a TypeSpecifier and a GenericType are never equal (above), this only needs to hold
        // within each type -- pinned here so a future change to either Equals cannot reintroduce the
        // cross-type mismatch (different hash, claimed-equal) the old placeholder had.
        [Fact]
        public void EqualValuesHaveTheSameHashCode()
        {
            TypeSpecifier typeA = TypeSpecifier.FromType<List<int>>();
            TypeSpecifier sameAsTypeA = TypeSpecifier.FromType<List<int>>();
            Assert.Equal(typeA.GetHashCode(), sameAsTypeA.GetHashCode());

            GenericType genType = new GenericType("T");
            GenericType sameGenType = new GenericType("T");
            Assert.Equal(genType.GetHashCode(), sameGenType.GetHashCode());
        }

        [Fact]
        public void TestTypeConversionEquality()
        {
            TypeSpecifier typeInt = TypeSpecifier.FromType<int>();

            Assert.Equal(typeInt, TypeSpecifier.FromType(typeof(int)));
            Assert.Equal(TypeSpecifier.FromType(typeof(int)), typeInt);

            Assert.NotEqual(TypeSpecifier.FromType(typeof(string)), typeInt);
            Assert.NotEqual(typeInt, TypeSpecifier.FromType(typeof(string)));
        }

        [Fact]
        public void TestGenericTypeConversionEquality()
        {
            TypeSpecifier typeInt = TypeSpecifier.FromType<List<int>>();

            Assert.Equal(typeInt, TypeSpecifier.FromType<List<int>>());
            Assert.Equal(TypeSpecifier.FromType<List<int>>(), typeInt);

            Assert.NotEqual(typeInt, TypeSpecifier.FromType<List<string>>());
            Assert.NotEqual(TypeSpecifier.FromType<List<string>>(), typeInt);

            Assert.NotEqual(typeInt, TypeSpecifier.FromType<Stack<string>>());
            Assert.NotEqual(TypeSpecifier.FromType<Stack<string>>(), typeInt);
        }
    }
}
