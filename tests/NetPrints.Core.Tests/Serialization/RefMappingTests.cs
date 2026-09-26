using System;
using System.Linq;
using NetPrints.Core;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using Xunit;

namespace NetPrints.Tests.Serialization
{
    /// <summary><see cref="NodeMappingContext"/>'s <c>ToRef</c>/<c>FromRef</c> round trips.</summary>
    public class RefMappingTests
    {
        private static NodeMappingContext NewContext() => new NodeMappingContext(new ClassGraph { Name = "C" });

        [Fact]
        public void ClosedTypeRoundTrips()
        {
            var context = NewContext();
            var type = new TypeSpecifier("System.Int32");

            TypeRef typeRef = context.ToRef(type);

            Assert.Equal("System.Int32", typeRef.Name);
            Assert.False(typeRef.Generic);
            Assert.Null(typeRef.Args);
            Assert.Equal(type, context.FromRef(typeRef));
        }

        [Fact]
        public void GenericTypeRoundTrips()
        {
            var context = NewContext();
            var type = new GenericType("T");

            TypeRef typeRef = context.ToRef(type);

            Assert.True(typeRef.Generic);
            Assert.Equal("T", typeRef.Name);
            var roundTripped = Assert.IsType<GenericType>(context.FromRef(typeRef));
            Assert.Equal("T", roundTripped.Name);
        }

        [Fact]
        public void EnumAndInterfaceFlagsRoundTrip()
        {
            var context = NewContext();
            var type = new TypeSpecifier("System.DayOfWeek", isEnum: true, isInterface: false);

            TypeRef typeRef = context.ToRef(type);
            Assert.True(typeRef.IsEnum);
            Assert.False(typeRef.IsInterface);

            var roundTripped = (TypeSpecifier)context.FromRef(typeRef);
            Assert.True(roundTripped.IsEnum);
        }

        [Fact]
        public void GenericArgumentsRoundTrip()
        {
            var context = NewContext();
            var listOfString = new TypeSpecifier("System.Collections.Generic.List`1", genericArguments: [new TypeSpecifier("System.String")]);

            TypeRef typeRef = context.ToRef(listOfString);

            Assert.NotNull(typeRef.Args);
            Assert.Single(typeRef.Args!);
            Assert.Equal("System.String", typeRef.Args![0].Name);

            var roundTripped = (TypeSpecifier)context.FromRef(typeRef);
            Assert.Equal(listOfString, roundTripped);
            Assert.Equal("System.String", ((TypeSpecifier)roundTripped.GenericArguments[0]).Name);
        }

        [Fact]
        public void MethodRefRoundTrips()
        {
            var context = NewContext();
            var declaringType = new TypeSpecifier("System.Console");
            var parameter = new MethodParameter("value", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, false, null);
            var method = new MethodSpecifier("WriteLine", [parameter], [], MethodModifiers.Static, MemberVisibility.Public, declaringType, []);

            MethodRef methodRef = context.ToRef(method);

            Assert.Equal("WriteLine", methodRef.Name);
            Assert.Equal("System.Console", methodRef.DeclaringType.Name);
            Assert.NotNull(methodRef.Parameters);
            Assert.Single(methodRef.Parameters!);
            Assert.Null(methodRef.ReturnTypes);
            Assert.Null(methodRef.GenericArgs);

            MethodSpecifier roundTripped = context.FromRef(methodRef);
            Assert.Equal(method, roundTripped);
        }

        [Fact]
        public void MethodRefWithExplicitDefaultValueRoundTrips()
        {
            var context = NewContext();
            var parameter = new MethodParameter("count", TypeSpecifier.FromType<int>(), MethodParameterPassType.Default, true, 5);
            var method = new MethodSpecifier("M", [parameter], [], MethodModifiers.None, MemberVisibility.Public,
                new TypeSpecifier("C"), []);

            MethodRef methodRef = context.ToRef(method);
            Assert.NotNull(methodRef.Parameters![0].Default);
            Assert.Equal("System.Int32", methodRef.Parameters![0].Default!.Type);
            Assert.Equal("5", methodRef.Parameters![0].Default!.Value);

            MethodSpecifier roundTripped = context.FromRef(methodRef);
            Assert.True(roundTripped.Parameters[0].HasExplicitDefaultValue);
            Assert.Equal(5, roundTripped.Parameters[0].ExplicitDefaultValue);
        }

        [Fact]
        public void MethodRefWithExplicitNullDefaultValueRoundTrips()
        {
            var context = NewContext();
            var parameter = new MethodParameter("s", TypeSpecifier.FromType<string>(), MethodParameterPassType.Default, true, null);
            var method = new MethodSpecifier("M", [parameter], [], MethodModifiers.None, MemberVisibility.Public,
                new TypeSpecifier("C"), []);

            MethodRef methodRef = context.ToRef(method);
            Assert.NotNull(methodRef.Parameters![0].Default);
            Assert.Null(methodRef.Parameters![0].Default!.Value);

            MethodSpecifier roundTripped = context.FromRef(methodRef);
            Assert.True(roundTripped.Parameters[0].HasExplicitDefaultValue);
            Assert.Null(roundTripped.Parameters[0].ExplicitDefaultValue);
        }

        [Fact]
        public void ConstructorRefRoundTrips()
        {
            var context = NewContext();
            var declaringType = TypeSpecifier.FromType<Exception>();
            var constructor = new ConstructorSpecifier([], declaringType);

            ConstructorRef constructorRef = context.ToRef(constructor);
            Assert.Equal(declaringType.Name, constructorRef.DeclaringType.Name);
            Assert.Null(constructorRef.Parameters);

            ConstructorSpecifier roundTripped = context.FromRef(constructorRef);
            Assert.Equal(declaringType, roundTripped.DeclaringType);
            Assert.Empty(roundTripped.Arguments);
        }

        [Fact]
        public void VariableRefRoundTrips()
        {
            var context = NewContext();
            var declaringType = new TypeSpecifier("C");
            var variable = new VariableSpecifier("Items", TypeSpecifier.FromType<int>(), MemberVisibility.Public,
                MemberVisibility.Private, declaringType, VariableModifiers.Static);

            VariableRef variableRef = context.ToRef(variable);

            Assert.Equal("Items", variableRef.Name);
            Assert.Equal(VariableScope.Member, variableRef.Scope);
            Assert.NotNull(variableRef.DeclaringType);

            VariableSpecifier roundTripped = context.FromRef(variableRef);
            Assert.Equal(variable.Name, roundTripped.Name);
            Assert.Equal(variable.Type, roundTripped.Type);
            Assert.Equal(variable.GetterVisibility, roundTripped.GetterVisibility);
            Assert.Equal(variable.SetterVisibility, roundTripped.SetterVisibility);
            Assert.Equal(variable.Modifiers, roundTripped.Modifiers);
        }

        [Fact]
        public void FromRefThrowsForAVariableRefWithNoDeclaringType()
        {
            var context = NewContext();
            var variableRef = new VariableRef("local", new TypeRef("System.Int32"), null,
                MemberVisibility.Private, MemberVisibility.Private, MemberVisibility.Private, VariableModifiers.None);

            Assert.Throws<NetPrints.Serialization.DocumentFormatException>(() => context.FromRef(variableRef));
        }

        [Fact]
        public void ToValueAndFromValueRoundTripThroughNull()
        {
            var context = NewContext();

            Assert.Null(context.ToValue(null, "n0/in.data.value"));
            Assert.Null(context.FromValue(null));
        }

        [Fact]
        public void ToValueDelegatesToTypedValueConverter()
        {
            var context = NewContext();

            TypedValue? value = context.ToValue(42, "n0/in.data.value");

            Assert.NotNull(value);
            Assert.Equal("System.Int32", value!.Type);
            Assert.Equal("42", value.Value);
            Assert.Equal(42, context.FromValue(value));
        }
    }
}
