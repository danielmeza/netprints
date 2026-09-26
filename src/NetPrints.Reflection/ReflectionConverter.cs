using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Reflection
{
    /// <summary>
    /// Helper class for converting from Roslyn symbols to NetPrints specifiers.
    /// </summary>
    public static class ReflectionConverter
    {
        private static readonly Dictionary<Microsoft.CodeAnalysis.Accessibility, MemberVisibility> roslynToNetprintsVisibility = new Dictionary<Microsoft.CodeAnalysis.Accessibility, MemberVisibility>()
        {
            [Microsoft.CodeAnalysis.Accessibility.Private] = MemberVisibility.Private,
            [Microsoft.CodeAnalysis.Accessibility.Protected] = MemberVisibility.Protected,
            [Microsoft.CodeAnalysis.Accessibility.Public] = MemberVisibility.Public,
            [Microsoft.CodeAnalysis.Accessibility.Internal] = MemberVisibility.Internal,
        };

        /// <summary>
        /// Converts a Roslyn accessibility to the closest <see cref="MemberVisibility"/>. Combined
        /// accessibilities without a direct equivalent (eg. <c>internal protected</c>,
        /// <c>private protected</c>) fall back to <see cref="MemberVisibility.Public"/> (a TODO in the
        /// code notes this is not yet done correctly).
        /// </summary>
        /// <param name="accessibility">Roslyn accessibility to convert.</param>
        /// <returns>The closest <see cref="MemberVisibility"/>.</returns>
        public static MemberVisibility VisibilityFromAccessibility(Microsoft.CodeAnalysis.Accessibility accessibility)
        {
            if (roslynToNetprintsVisibility.TryGetValue(accessibility, out var visibility))
            {
                return visibility;
            }

            // TODO: Do this correctly (eg. internal protected, private protected etc.)
            // https://stackoverflow.com/a/585869/4332314
            return MemberVisibility.Public;
        }

        /// <summary>
        /// Converts a Roslyn type symbol to a <see cref="TypeSpecifier"/>: its namespace-qualified,
        /// nested-class ("+"-separated) name, whether it is an enum or interface, and its generic
        /// arguments (recursively converted; a generic argument that is itself a type parameter
        /// becomes a <see cref="GenericType"/> via <see cref="GenericTypeSpecifierFromSymbol"/>). An
        /// array type is represented as <see cref="Array"/> (a TODO notes this loses the element type).
        /// </summary>
        /// <param name="type">Type symbol to convert.</param>
        /// <returns>The equivalent type specifier.</returns>
        /// <exception cref="ArgumentException"><paramref name="type"/> is an unbound generic type (eg. <c>List&lt;&gt;</c>).</exception>
        public static TypeSpecifier TypeSpecifierFromSymbol(ITypeSymbol type)
        {
            string typeName;

            if (type is IArrayTypeSymbol)
            {
                // TODO: Get more interesting type?
                typeName = typeof(Array).FullName!; // Non-generic BCL type: FullName is never null.
            }
            else
            {
                // Get the nested name (represented by + between classes)
                // See https://stackoverflow.com/questions/2443244/having-a-in-the-class-name
                string nestedPrefix = "";
                ITypeSymbol? containingType = type.ContainingType;
                while (containingType != null)
                {
                    nestedPrefix = $"{containingType.Name}+{nestedPrefix}";
                    containingType = containingType.ContainingType;
                }

                typeName = nestedPrefix + type.Name.Split('`').First();
                if (type.ContainingNamespace != null && !type.ContainingNamespace.IsGlobalNamespace)
                {
                    typeName = type.ContainingNamespace + "." + typeName;
                }
            }

            TypeSpecifier typeSpecifier = new TypeSpecifier(typeName,
                    type.TypeKind == TypeKind.Enum,
                    type.TypeKind == TypeKind.Interface);

            if (type is INamedTypeSymbol namedType)
            {
                if (namedType.IsUnboundGenericType)
                {
                    throw new ArgumentException(nameof(type));
                }

                foreach (ITypeSymbol genType in namedType.TypeArguments)
                {
                    if (genType is ITypeParameterSymbol genTypeParam)
                    {
                        // TODO: Convert and add constraints
                        typeSpecifier.GenericArguments.Add(GenericTypeSpecifierFromSymbol(genTypeParam));
                    }
                    else
                    {
                        typeSpecifier.GenericArguments.Add(TypeSpecifierFromSymbol(genType));
                    }
                }
            }

            return typeSpecifier;
        }

        /// <summary>
        /// Converts a Roslyn type parameter symbol to a <see cref="GenericType"/>, by name. Constraints
        /// are not yet converted (a TODO in the code), so the result always has none.
        /// </summary>
        /// <param name="type">Type parameter symbol to convert.</param>
        /// <returns>The equivalent generic type, with no constraints.</returns>
        public static GenericType GenericTypeSpecifierFromSymbol(ITypeParameterSymbol type)
        {
            // TODO: Convert constraints
            GenericType genericType = new GenericType(type.Name);

            return genericType;
        }

        /// <summary>
        /// Converts a Roslyn type symbol to a <see cref="BaseType"/>: a <see cref="GenericType"/> (via
        /// <see cref="GenericTypeSpecifierFromSymbol"/>) if it is a type parameter, otherwise a
        /// <see cref="TypeSpecifier"/> (via <see cref="TypeSpecifierFromSymbol"/>).
        /// </summary>
        /// <param name="type">Type symbol to convert.</param>
        /// <returns>The equivalent base type.</returns>
        public static BaseType BaseTypeSpecifierFromSymbol(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol typeParam)
            {
                return GenericTypeSpecifierFromSymbol(typeParam);
            }
            else
            {
                return TypeSpecifierFromSymbol(type);
            }
        }

        /// <summary>
        /// Converts a Roslyn parameter symbol to its name and <see cref="BaseType"/>, via
        /// <see cref="BaseTypeSpecifierFromSymbol"/>.
        /// </summary>
        /// <param name="paramSymbol">Parameter symbol to convert.</param>
        /// <returns>The parameter's name and type.</returns>
        public static Named<BaseType> NamedBaseTypeSpecifierFromSymbol(IParameterSymbol paramSymbol)
        {
            return new Named<BaseType>(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type));
        }

        private static readonly Dictionary<RefKind, MethodParameterPassType> refKindToPassType = new Dictionary<RefKind, MethodParameterPassType>()
        {
            [RefKind.None] = MethodParameterPassType.Default,
            [RefKind.Ref] = MethodParameterPassType.Reference,
            [RefKind.Out] = MethodParameterPassType.Out,
            [RefKind.In] = MethodParameterPassType.In,
            // C# 12 `ref readonly` parameters (used across the .NET BCL) accept `in` arguments.
            [RefKind.RefReadOnlyParameter] = MethodParameterPassType.In,
        };

        /// <summary>
        /// Converts a Roslyn parameter symbol to a <see cref="MethodParameter"/>: its name, type (via
        /// <see cref="BaseTypeSpecifierFromSymbol"/>), pass type (mapped from <see cref="RefKind"/>,
        /// with <see cref="RefKind.RefReadOnlyParameter"/> treated as <see cref="MethodParameterPassType.In"/>),
        /// and explicit default value if it has one.
        /// </summary>
        /// <param name="paramSymbol">Parameter symbol to convert.</param>
        /// <returns>The equivalent method parameter.</returns>
        public static MethodParameter MethodParameterFromSymbol(in IParameterSymbol paramSymbol)
        {
            return new MethodParameter(paramSymbol.Name, BaseTypeSpecifierFromSymbol(paramSymbol.Type), refKindToPassType[paramSymbol.RefKind],
                paramSymbol.HasExplicitDefaultValue, paramSymbol.HasExplicitDefaultValue ? paramSymbol.ExplicitDefaultValue : null);
        }

        /// <summary>
        /// Converts a Roslyn method symbol to a <see cref="MethodSpecifier"/>: its name, visibility,
        /// modifiers (virtual, sealed, abstract, static, override, async), parameters, return type (or
        /// none for a <see langword="void"/> method) and generic arguments.
        /// </summary>
        /// <param name="method">Method symbol to convert.</param>
        /// <returns>The equivalent method specifier.</returns>
        public static MethodSpecifier MethodSpecifierFromSymbol(IMethodSymbol method)
        {
            MemberVisibility visibility = VisibilityFromAccessibility(method.DeclaredAccessibility);

            var modifiers = MethodModifiers.None;

            if (method.IsVirtual)
            {
                modifiers |= MethodModifiers.Virtual;
            }

            if (method.IsSealed)
            {
                modifiers |= MethodModifiers.Sealed;
            }

            if (method.IsAbstract)
            {
                modifiers |= MethodModifiers.Abstract;
            }

            if (method.IsStatic)
            {
                modifiers |= MethodModifiers.Static;
            }

            if (method.IsOverride)
            {
                modifiers |= MethodModifiers.Override;
            }

            if (method.IsAsync)
            {
                modifiers |= MethodModifiers.Async;
            }

            BaseType[] returnTypes = method.ReturnsVoid ?
                new BaseType[] { } :
                new BaseType[] { BaseTypeSpecifierFromSymbol(method.ReturnType) };

            MethodParameter[] parameters = method.Parameters.Select(
                p => MethodParameterFromSymbol(p)).ToArray();

            BaseType[] genericArgs = method.TypeParameters.Select(
                p => BaseTypeSpecifierFromSymbol(p)).ToArray();

            return new MethodSpecifier(
                method.Name,
                parameters,
                returnTypes,
                modifiers,
                visibility,
                TypeSpecifierFromSymbol(method.ContainingType),
                genericArgs);
        }

        /// <summary>
        /// Converts a Roslyn property symbol to a <see cref="VariableSpecifier"/>: its name, type,
        /// getter/setter visibility (each <see cref="MemberVisibility.Private"/> if that accessor does
        /// not exist), and modifiers (static, read-only; other modifiers are not yet converted, a TODO
        /// in the code).
        /// </summary>
        /// <param name="property">Property symbol to convert.</param>
        /// <returns>The equivalent variable specifier.</returns>
        public static VariableSpecifier VariableSpecifierFromSymbol(IPropertySymbol property)
        {
            var getterAccessibility = property.GetMethod?.DeclaredAccessibility;
            var setterAccessibility = property.SetMethod?.DeclaredAccessibility;

            var modifiers = new VariableModifiers();

            if (property.IsStatic)
            {
                modifiers |= VariableModifiers.Static;
            }

            if (property.IsReadOnly)
            {
                modifiers |= VariableModifiers.ReadOnly;
            }

            // TODO: More modifiers

            return new VariableSpecifier(
                property.Name,
                TypeSpecifierFromSymbol(property.Type),
                getterAccessibility.HasValue ? VisibilityFromAccessibility(getterAccessibility.Value) : MemberVisibility.Private,
                setterAccessibility.HasValue ? VisibilityFromAccessibility(setterAccessibility.Value) : MemberVisibility.Private,
                TypeSpecifierFromSymbol(property.ContainingType),
                modifiers);
        }

        /// <summary>
        /// Converts a Roslyn field symbol to a <see cref="VariableSpecifier"/>: its name, type,
        /// visibility (used for both getter and setter, since a field has one), and modifiers (static,
        /// const, read-only; other modifiers are not yet converted, a TODO in the code).
        /// </summary>
        /// <param name="field">Field symbol to convert.</param>
        /// <returns>The equivalent variable specifier.</returns>
        public static VariableSpecifier VariableSpecifierFromField(IFieldSymbol field)
        {
            var visibility = VisibilityFromAccessibility(field.DeclaredAccessibility);

            var modifiers = new VariableModifiers();

            if (field.IsStatic)
            {
                modifiers |= VariableModifiers.Static;
            }

            if (field.IsConst)
            {
                modifiers |= VariableModifiers.Const;
            }

            if (field.IsReadOnly)
            {
                modifiers |= VariableModifiers.ReadOnly;
            }

            // TODO: More modifiers

            return new VariableSpecifier(
                field.Name,
                TypeSpecifierFromSymbol(field.Type),
                visibility,
                visibility,
                TypeSpecifierFromSymbol(field.ContainingType),
                modifiers);
        }

        /// <summary>
        /// Converts a Roslyn constructor method symbol to a <see cref="ConstructorSpecifier"/>: its
        /// parameters and declaring type.
        /// </summary>
        /// <param name="constructorMethodSymbol">Constructor method symbol to convert.</param>
        /// <returns>The equivalent constructor specifier.</returns>
        public static ConstructorSpecifier ConstructorSpecifierFromSymbol(IMethodSymbol constructorMethodSymbol)
        {
            return new ConstructorSpecifier(
                constructorMethodSymbol.Parameters.Select(p => MethodParameterFromSymbol(p)),
                TypeSpecifierFromSymbol(constructorMethodSymbol.ContainingType));
        }
    }
}
