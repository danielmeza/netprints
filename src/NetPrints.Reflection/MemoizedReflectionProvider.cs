using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Reflection
{
    /// <summary>
    /// <see cref="IReflectionProvider"/> decorator that memoizes every query against the wrapped
    /// provider (see <see cref="Memoization"/>), so repeated identical queries (eg. re-rendering the
    /// same node suggestions) do not re-run reflection. Call <see cref="Reset"/> after the wrapped
    /// provider's underlying data changes (eg. the compilation was rebuilt), since results are cached
    /// for the lifetime of this instance otherwise.
    /// </summary>
    public class MemoizedReflectionProvider : IReflectionProvider
    {
        private readonly IReflectionProvider provider;

        private Func<TypeSpecifier, IEnumerable<ConstructorSpecifier>> memoizedGetConstructors;
        private Func<TypeSpecifier, IEnumerable<string>> memoizedGetEnumNames;
        private Func<MethodSpecifier, string> memoizedGetMethodDocumentation;
        private Func<MethodSpecifier, int, string> memoizedGetMethodParameterDocumentation;
        private Func<MethodSpecifier, int, string> memoizedGetMethodReturnDocumentation;
        private Func<IEnumerable<TypeSpecifier>> memoizedGetNonStaticTypes;
        private Func<TypeSpecifier, IEnumerable<MethodSpecifier>> memoizedGetOverridableMethodsForType;
        private Func<MethodSpecifier, IEnumerable<MethodSpecifier>> memoizedGetPublicMethodOverloads;
        private Func<TypeSpecifier, TypeSpecifier, bool> memoizedHasImplicitCast;
        private Func<TypeSpecifier, TypeSpecifier, bool> memoizedTypeSpecifierIsSubclassOf;
        private Func<ReflectionProviderMethodQuery, IEnumerable<MethodSpecifier>> memoizedGetMethods;
        private Func<ReflectionProviderVariableQuery, IEnumerable<VariableSpecifier>> memoizedGetVariables;

        /// <summary>
        /// Wraps <paramref name="reflectionProvider"/> and builds the initial memoized delegates.
        /// </summary>
        /// <param name="reflectionProvider">Provider to memoize queries against.</param>
        public MemoizedReflectionProvider(IReflectionProvider reflectionProvider)
        {
            provider = reflectionProvider;

            Reset();
        }

        /// <summary>
        /// Resets the memoization.
        /// </summary>
        public void Reset()
        {
            memoizedGetConstructors = provider.GetConstructors;
            memoizedGetConstructors = memoizedGetConstructors.Memoize();

            memoizedGetEnumNames = provider.GetEnumNames;
            memoizedGetEnumNames = memoizedGetEnumNames.Memoize();

            memoizedGetMethodDocumentation = provider.GetMethodDocumentation;
            memoizedGetMethodDocumentation = memoizedGetMethodDocumentation.Memoize();

            memoizedGetMethodParameterDocumentation = provider.GetMethodParameterDocumentation;
            memoizedGetMethodParameterDocumentation = memoizedGetMethodParameterDocumentation.Memoize();

            memoizedGetMethodReturnDocumentation = provider.GetMethodReturnDocumentation;
            memoizedGetMethodReturnDocumentation = memoizedGetMethodReturnDocumentation.Memoize();

            memoizedGetNonStaticTypes = provider.GetNonStaticTypes;
            memoizedGetNonStaticTypes = memoizedGetNonStaticTypes.Memoize();

            memoizedGetOverridableMethodsForType = provider.GetOverridableMethodsForType;
            memoizedGetOverridableMethodsForType = memoizedGetOverridableMethodsForType.Memoize();

            memoizedGetMethods = provider.GetMethods;
            memoizedGetMethods = memoizedGetMethods.Memoize();

            memoizedGetPublicMethodOverloads = provider.GetPublicMethodOverloads;
            memoizedGetPublicMethodOverloads = memoizedGetPublicMethodOverloads.Memoize();

            memoizedGetVariables = provider.GetVariables;
            memoizedGetVariables = memoizedGetVariables.Memoize();

            memoizedHasImplicitCast = provider.HasImplicitCast;
            memoizedHasImplicitCast = memoizedHasImplicitCast.Memoize();

            memoizedTypeSpecifierIsSubclassOf = provider.TypeSpecifierIsSubclassOf;
            memoizedTypeSpecifierIsSubclassOf = memoizedTypeSpecifierIsSubclassOf.Memoize();
        }

        /// <inheritdoc/>
        public IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier)
            => memoizedGetConstructors(typeSpecifier);

        /// <inheritdoc/>
        public IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier)
            => memoizedGetEnumNames(typeSpecifier);

        /// <inheritdoc/>
        public string GetMethodDocumentation(MethodSpecifier methodSpecifier)
            => memoizedGetMethodDocumentation(methodSpecifier);

        /// <inheritdoc/>
        public string GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex)
            => memoizedGetMethodParameterDocumentation(methodSpecifier, parameterIndex);

        /// <inheritdoc/>
        public string GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex)
            => memoizedGetMethodReturnDocumentation(methodSpecifier, returnIndex);

        /// <inheritdoc/>
        public IEnumerable<TypeSpecifier> GetNonStaticTypes()
            => memoizedGetNonStaticTypes();

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier)
            => memoizedGetOverridableMethodsForType(typeSpecifier);

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier)
            => memoizedGetPublicMethodOverloads(methodSpecifier);

        /// <inheritdoc/>
        public IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query)
            => memoizedGetMethods(query);

        /// <inheritdoc/>
        public IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query)
            => memoizedGetVariables(query);

        /// <inheritdoc/>
        public bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType)
            => memoizedHasImplicitCast(fromType, toType);

        /// <inheritdoc/>
        public bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b)
            => memoizedTypeSpecifierIsSubclassOf(a, b);
    }
}
