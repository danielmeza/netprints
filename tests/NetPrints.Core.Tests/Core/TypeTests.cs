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

        [Fact]
        public void TestGenericEquality()
        {
            TypeSpecifier typeA = new TypeSpecifier("TypeA", false, false, new BaseType[] { });
            GenericType genType1 = new GenericType("T1");
            GenericType genType2 = new GenericType("T2");

            Assert.Equal<BaseType>(typeA, genType1);
            Assert.Equal<BaseType>(typeA, genType2);
            Assert.NotEqual(genType1, genType2);
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
