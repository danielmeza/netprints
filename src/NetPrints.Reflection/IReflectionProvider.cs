using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Reflection
{
    /// <summary>
    /// Marker interface for a query passed to <see cref="IReflectionProvider.GetMethods"/> or
    /// <see cref="IReflectionProvider.GetVariables"/>: <see cref="ReflectionProviderMethodQuery"/> or
    /// <see cref="ReflectionProviderVariableQuery"/>.
    /// </summary>
    public interface IReflectionProviderQuery
    {
    }

    /// <summary>
    /// Fluent, mutable filter for <see cref="IReflectionProvider.GetMethods"/>: every set field
    /// narrows the search, an unset (<see langword="null"/>) field matches anything. Also usable as a
    /// value-equality key (eg. for caching, see <see cref="MemoizedReflectionProvider"/>).
    /// </summary>
    public class ReflectionProviderMethodQuery : IReflectionProviderQuery, IEqualityComparer<ReflectionProviderMethodQuery>
    {
        /// <summary>
        /// Declaring type to search methods on, or <see langword="null"/> for any type.
        /// </summary>
        public TypeSpecifier? Type { get; set; }

        /// <summary>
        /// Whether to match only static (<see langword="true"/>) or only instance
        /// (<see langword="false"/>) methods, or either (<see langword="null"/>).
        /// </summary>
        public bool? Static { get; set; }

        /// <summary>
        /// Type the method must be visible from, or <see langword="null"/> for no visibility filter.
        /// </summary>
        public TypeSpecifier? VisibleFrom { get; set; }

        /// <summary>
        /// Return type to match, or <see langword="null"/> for any return type.
        /// </summary>
        public TypeSpecifier? ReturnType { get; set; }

        /// <summary>
        /// A type at least one of the method's arguments must match, or <see langword="null"/> for no
        /// argument-type filter.
        /// </summary>
        public TypeSpecifier? ArgumentType { get; set; }

        /// <summary>
        /// Whether to match only generic (<see langword="true"/>) or only non-generic
        /// (<see langword="false"/>) methods, or either (<see langword="null"/>).
        /// </summary>
        public bool? HasGenericArguments { get; set; }

        /// <summary>
        /// Sets <see cref="Type"/> and returns this query.
        /// </summary>
        /// <param name="type">Declaring type to search methods on.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithType(TypeSpecifier type)
        {
            Type = type;
            return this;
        }

        /// <summary>
        /// Sets <see cref="Static"/> and returns this query.
        /// </summary>
        /// <param name="isStatic">Whether to match only static methods.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithStatic(bool isStatic)
        {
            Static = isStatic;
            return this;
        }

        /// <summary>
        /// Sets <see cref="VisibleFrom"/> and returns this query.
        /// </summary>
        /// <param name="visibleFrom">Type the method must be visible from.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithVisibleFrom(TypeSpecifier visibleFrom)
        {
            VisibleFrom = visibleFrom;
            return this;
        }

        /// <summary>
        /// Sets <see cref="ReturnType"/> and returns this query.
        /// </summary>
        /// <param name="returnType">Return type to match.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithReturnType(TypeSpecifier returnType)
        {
            ReturnType = returnType;
            return this;
        }

        /// <summary>
        /// Sets <see cref="ArgumentType"/> and returns this query.
        /// </summary>
        /// <param name="argumentType">A type at least one of the method's arguments must match.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithArgumentType(TypeSpecifier argumentType)
        {
            ArgumentType = argumentType;
            return this;
        }

        /// <summary>
        /// Sets <see cref="HasGenericArguments"/> and returns this query.
        /// </summary>
        /// <param name="hasGenericArguments">Whether to match only generic methods.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderMethodQuery WithHasGenericArguments(bool hasGenericArguments)
        {
            HasGenericArguments = hasGenericArguments;
            return this;
        }

        /// <summary>
        /// Compares two queries field by field.
        /// </summary>
        /// <param name="x">First query, or <see langword="null"/>.</param>
        /// <param name="y">Second query, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if both are <see langword="null"/>, or neither is and every field is equal.</returns>
        public bool Equals(ReflectionProviderMethodQuery? x, ReflectionProviderMethodQuery? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.Type == y.Type && x.Static == y.Static && x.VisibleFrom == y.VisibleFrom
                && x.ReturnType == y.ReturnType && x.ArgumentType == y.ArgumentType && x.HasGenericArguments == y.HasGenericArguments;
        }

        /// <summary>
        /// Returns a hash combining every field, consistent with <see cref="Equals(ReflectionProviderMethodQuery, ReflectionProviderMethodQuery)"/>.
        /// </summary>
        /// <param name="obj">Query to hash.</param>
        /// <returns>A hash code for the query.</returns>
        public int GetHashCode(ReflectionProviderMethodQuery obj)
        {
            return HashCode.Combine(Type, Static, VisibleFrom, ReturnType, ArgumentType, HasGenericArguments);
        }

        /// <summary>
        /// Same as <see cref="Equals(ReflectionProviderMethodQuery, ReflectionProviderMethodQuery)"/>,
        /// with a reference-equality shortcut.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is the same instance, or a query with every field equal.</returns>
        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj)
                || (obj is ReflectionProviderMethodQuery query && Equals(this, query));
        }

        /// <summary>
        /// Same as <see cref="GetHashCode(ReflectionProviderMethodQuery)"/> for this instance.
        /// </summary>
        /// <returns>A hash code for this query.</returns>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }
    }

    /// <summary>
    /// Fluent, mutable filter for <see cref="IReflectionProvider.GetVariables"/>: every set field
    /// narrows the search, an unset (<see langword="null"/>) field matches anything. Also usable as a
    /// value-equality key (eg. for caching, see <see cref="MemoizedReflectionProvider"/>).
    /// </summary>
    public class ReflectionProviderVariableQuery : IReflectionProviderQuery, IEqualityComparer<ReflectionProviderVariableQuery>
    {
        /// <summary>
        /// Declaring type to search variables on, or <see langword="null"/> for any type.
        /// </summary>
        public TypeSpecifier? Type { get; set; }

        /// <summary>
        /// Whether to match only static (<see langword="true"/>) or only instance
        /// (<see langword="false"/>) variables, or either (<see langword="null"/>).
        /// </summary>
        public bool? Static { get; set; }

        /// <summary>
        /// Type the variable must be visible from, or <see langword="null"/> for no visibility filter.
        /// </summary>
        public TypeSpecifier? VisibleFrom { get; set; }

        /// <summary>
        /// Type to match the variable's own type against, or <see langword="null"/> for no type filter.
        /// </summary>
        public TypeSpecifier? VariableType { get; set; }

        /// <summary>
        /// When <see cref="VariableType"/> is set, whether the variable's type must derive from it
        /// (<see langword="true"/>) rather than match it exactly (<see langword="false"/>, the default).
        /// </summary>
        public bool VariableTypeDerivesFrom { get; set; } = false;

        /// <summary>
        /// Sets <see cref="Type"/> and returns this query.
        /// </summary>
        /// <param name="type">Declaring type to search variables on.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderVariableQuery WithType(TypeSpecifier type)
        {
            Type = type;
            return this;
        }

        /// <summary>
        /// Sets <see cref="Static"/> and returns this query.
        /// </summary>
        /// <param name="isStatic">Whether to match only static variables.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderVariableQuery WithStatic(bool isStatic)
        {
            Static = isStatic;
            return this;
        }

        /// <summary>
        /// Sets <see cref="VisibleFrom"/> and returns this query.
        /// </summary>
        /// <param name="visibleFrom">Type the variable must be visible from.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderVariableQuery WithVisibleFrom(TypeSpecifier visibleFrom)
        {
            VisibleFrom = visibleFrom;
            return this;
        }

        /// <summary>
        /// Sets <see cref="VariableType"/> and <see cref="VariableTypeDerivesFrom"/> and returns this query.
        /// </summary>
        /// <param name="type">Type to match the variable's own type against.</param>
        /// <param name="derivesFrom">Whether the variable's type must derive from <paramref name="type"/> rather than match it exactly.</param>
        /// <returns>This query.</returns>
        public ReflectionProviderVariableQuery WithVariableType(TypeSpecifier type, bool derivesFrom = false)
        {
            VariableType = type;
            VariableTypeDerivesFrom = derivesFrom;
            return this;
        }

        /// <summary>
        /// Compares two queries field by field.
        /// </summary>
        /// <param name="x">First query, or <see langword="null"/>.</param>
        /// <param name="y">Second query, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if both are <see langword="null"/>, or neither is and every field is equal.</returns>
        public bool Equals(ReflectionProviderVariableQuery? x, ReflectionProviderVariableQuery? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.Type == y.Type && x.Static == y.Static && x.VisibleFrom == y.VisibleFrom
                && x.VariableType == y.VariableType && x.VariableTypeDerivesFrom == y.VariableTypeDerivesFrom;
        }

        /// <summary>
        /// Returns a hash combining every field, consistent with <see cref="Equals(ReflectionProviderVariableQuery, ReflectionProviderVariableQuery)"/>.
        /// </summary>
        /// <param name="obj">Query to hash.</param>
        /// <returns>A hash code for the query.</returns>
        public int GetHashCode(ReflectionProviderVariableQuery obj)
        {
            return HashCode.Combine(Type, Static, VisibleFrom, VariableType, VariableTypeDerivesFrom);
        }

        /// <summary>
        /// Same as <see cref="Equals(ReflectionProviderVariableQuery, ReflectionProviderVariableQuery)"/>,
        /// with a reference-equality shortcut.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is the same instance, or a query with every field equal.</returns>
        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj)
                || (obj is ReflectionProviderVariableQuery query && Equals(this, query));
        }

        /// <summary>
        /// Same as <see cref="GetHashCode(ReflectionProviderVariableQuery)"/> for this instance.
        /// </summary>
        /// <returns>A hash code for this query.</returns>
        public override int GetHashCode()
        {
            return GetHashCode(this);
        }
    }

    /// <summary>
    /// Interface for reflecting on types, methods etc.
    /// </summary>
    public interface IReflectionProvider
    {
        /// <summary>
        /// Returns whether <paramref name="a"/> is a subclass of (or implements) <paramref name="b"/>.
        /// </summary>
        /// <param name="a">Candidate subclass.</param>
        /// <param name="b">Candidate base class or interface.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> derives from or implements <paramref name="b"/>.</returns>
        bool TypeSpecifierIsSubclassOf(TypeSpecifier a, TypeSpecifier b);

        /// <summary>
        /// Returns whether a value of <paramref name="fromType"/> has an implicit conversion to
        /// <paramref name="toType"/>.
        /// </summary>
        /// <param name="fromType">Source type.</param>
        /// <param name="toType">Target type.</param>
        /// <returns><see langword="true"/> if the implicit conversion exists.</returns>
        bool HasImplicitCast(TypeSpecifier fromType, TypeSpecifier toType);

        /// <summary>
        /// Returns every known type that is not static.
        /// </summary>
        /// <returns>Every known non-static type.</returns>
        IEnumerable<TypeSpecifier> GetNonStaticTypes();

        /// <summary>
        /// Returns the methods of <paramref name="typeSpecifier"/> (and its base types) that a derived
        /// class can override.
        /// </summary>
        /// <param name="typeSpecifier">Type to get overridable methods for.</param>
        /// <returns>Overridable methods of the type.</returns>
        IEnumerable<MethodSpecifier> GetOverridableMethodsForType(TypeSpecifier typeSpecifier);

        /// <summary>
        /// Returns every public overload of <paramref name="methodSpecifier"/>'s method (same
        /// declaring type and name, any parameters).
        /// </summary>
        /// <param name="methodSpecifier">Method to find overloads of.</param>
        /// <returns>Every public overload, including <paramref name="methodSpecifier"/> itself.</returns>
        IEnumerable<MethodSpecifier> GetPublicMethodOverloads(MethodSpecifier methodSpecifier);

        /// <summary>
        /// Returns the constructors of <paramref name="typeSpecifier"/>.
        /// </summary>
        /// <param name="typeSpecifier">Type to get constructors for.</param>
        /// <returns>The type's constructors.</returns>
        IEnumerable<ConstructorSpecifier> GetConstructors(TypeSpecifier typeSpecifier);

        /// <summary>
        /// Returns the member names of <paramref name="typeSpecifier"/>, an enum type.
        /// </summary>
        /// <param name="typeSpecifier">Enum type to get member names for.</param>
        /// <returns>The enum's member names.</returns>
        IEnumerable<string> GetEnumNames(TypeSpecifier typeSpecifier);

        /// <summary>
        /// Returns the methods matching <paramref name="query"/>.
        /// </summary>
        /// <param name="query">Filter narrowing the search.</param>
        /// <returns>The matching methods.</returns>
        IEnumerable<MethodSpecifier> GetMethods(ReflectionProviderMethodQuery query);

        /// <summary>
        /// Returns the fields and properties matching <paramref name="query"/>.
        /// </summary>
        /// <param name="query">Filter narrowing the search.</param>
        /// <returns>The matching variables.</returns>
        IEnumerable<VariableSpecifier> GetVariables(ReflectionProviderVariableQuery query);

        // Documentation
        /// <summary>
        /// Returns the XML documentation <c>&lt;summary&gt;</c> text for <paramref name="methodSpecifier"/>'s
        /// method, read from its assembly's .xml documentation file, or <see langword="null"/> if the
        /// method, its containing assembly, its documentation file, or the summary itself cannot be found.
        /// </summary>
        /// <param name="methodSpecifier">Method to get documentation for.</param>
        /// <returns>The method's documentation summary text, or <see langword="null"/>.</returns>
        string? GetMethodDocumentation(MethodSpecifier methodSpecifier);

        /// <summary>
        /// Returns the XML documentation <c>&lt;param&gt;</c> text for one of
        /// <paramref name="methodSpecifier"/>'s parameters, read from its assembly's .xml
        /// documentation file, or <see langword="null"/> if the method, its containing assembly, its
        /// documentation file, or that parameter's tag cannot be found.
        /// </summary>
        /// <param name="methodSpecifier">Method the parameter belongs to.</param>
        /// <param name="parameterIndex">Index of the parameter into <see cref="MethodSpecifier.Parameters"/>.</param>
        /// <returns>The parameter's documentation text, or <see langword="null"/>.</returns>
        string? GetMethodParameterDocumentation(MethodSpecifier methodSpecifier, int parameterIndex);

        /// <summary>
        /// Returns the XML documentation <c>&lt;returns&gt;</c> text for one of
        /// <paramref name="methodSpecifier"/>'s return values, read from its assembly's .xml
        /// documentation file, or <see langword="null"/> if the method, its containing assembly, its
        /// documentation file, or the return tag cannot be found.
        /// </summary>
        /// <param name="methodSpecifier">Method the return value belongs to.</param>
        /// <param name="returnIndex">Index of the return value into <see cref="MethodSpecifier.ReturnTypes"/>.</param>
        /// <returns>The return value's documentation text, or <see langword="null"/>.</returns>
        string? GetMethodReturnDocumentation(MethodSpecifier methodSpecifier, int returnIndex);
    }
}
