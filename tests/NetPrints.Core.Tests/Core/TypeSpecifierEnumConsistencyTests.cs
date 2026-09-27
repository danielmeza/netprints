using System;
using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrintsUnitTests
{
    /// <summary>
    /// Pins that every construction path for a <see cref="TypeSpecifier"/> agrees on
    /// <see cref="TypeSpecifier.IsEnum"/> for the same underlying type, including array-of-enum types
    /// (<see cref="MakeArrayNode"/> once copied its element type's <c>IsEnum</c> onto the array type
    /// itself, disagreeing with <see cref="MakeArrayTypeNode"/> and with reflection, for which no array
    /// type is itself an enum).
    /// </summary>
    public class TypeSpecifierEnumConsistencyTests
    {
        [Fact]
        public void FromTypeMarksAnEnumButNotAnArrayOfEnums()
        {
            TypeSpecifier enumType = TypeSpecifier.FromType<DayOfWeek>();
            TypeSpecifier arrayOfEnumType = TypeSpecifier.FromType(typeof(DayOfWeek[]));

            Assert.True(enumType.IsEnum);
            Assert.False(arrayOfEnumType.IsEnum);
        }

        [Fact]
        public void NodeMappingContextRoundTripPreservesIsEnumForAnEnumAndItsArray()
        {
            var context = new NodeMappingContext(new ClassGraph { Name = "C" });
            TypeSpecifier enumType = TypeSpecifier.FromType<DayOfWeek>();
            TypeSpecifier arrayOfEnumType = TypeSpecifier.FromType(typeof(DayOfWeek[]));

            var roundTrippedEnum = (TypeSpecifier)context.FromRef(context.ToRef(enumType));
            var roundTrippedArray = (TypeSpecifier)context.FromRef(context.ToRef(arrayOfEnumType));

            Assert.Equal(enumType.IsEnum, roundTrippedEnum.IsEnum);
            Assert.Equal(arrayOfEnumType.IsEnum, roundTrippedArray.IsEnum);
        }

        [Fact]
        public void MakeArrayNodeAndMakeArrayTypeNodeAgreeOnIsEnumForAnArrayOfEnums()
        {
            var graph = new MethodGraph("M");
            TypeSpecifier enumType = TypeSpecifier.FromType<DayOfWeek>();

            var elementSource = new TypeNode(graph, enumType);

            var makeArray = new MakeArrayNode(graph);
            GraphUtil.ConnectTypePins(elementSource.OutputTypePins[0], makeArray.ElementTypePin);

            var makeArrayType = new MakeArrayTypeNode(graph);
            GraphUtil.ConnectTypePins(elementSource.OutputTypePins[0], makeArrayType.InputTypePins[0]);

            var makeArrayTypeResult = (TypeSpecifier)makeArrayType.OutputTypePins[0].InferredType.RequireValue();

            // Neither array type is itself an enum, matching reflection's own array-of-enum type.
            Assert.False(makeArray.ArrayType.IsEnum);
            Assert.False(makeArrayTypeResult.IsEnum);

            // The two construction paths must agree, or TypeSpecifier.Equals would have to choose a side.
            Assert.Equal(makeArray.ArrayType, makeArrayTypeResult);
            Assert.Equal(TypeSpecifier.FromType(typeof(DayOfWeek[])), makeArray.ArrayType);
        }
    }
}
