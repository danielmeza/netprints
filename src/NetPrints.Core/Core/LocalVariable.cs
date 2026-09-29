#nullable enable
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis.CSharp;

namespace NetPrints.Core
{
    /// <summary>
    /// Whether a <see cref="VariableSpecifier"/> describes a class-level field or property, or a
    /// method- or constructor-local variable (US5, data-model.md §3).
    /// </summary>
    public enum VariableScope
    {
        /// <summary>A class-level field or property.</summary>
        Member = 0,

        /// <summary>A method- or constructor-local variable.</summary>
        Local = 1,
    }

    /// <summary>
    /// A variable local to one method or constructor graph (US5, data-model.md §3): declared at the
    /// top of the generated method body and read/written by <see cref="Graph.VariableGetterNode"/>/
    /// <see cref="Graph.VariableSetterNode"/> nodes whose <see cref="Graph.VariableNode.Variable"/> has
    /// <see cref="VariableSpecifier.Scope"/> set to <see cref="VariableScope.Local"/>.
    /// </summary>
    public sealed partial class LocalVariable : ModelObject
    {
        /// <summary>
        /// Name of the local variable.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// Specifier for the type of the local variable.
        /// </summary>
        [ObservableProperty]
        public partial TypeSpecifier Type { get; set; }

        /// <summary>
        /// Creates a local variable.
        /// </summary>
        /// <param name="name">Name of the local variable; must be a valid C# identifier.</param>
        /// <param name="type">Specifier for the type of the local variable.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid C# identifier.</exception>
        public LocalVariable(string name, TypeSpecifier type)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(type);

            if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            {
                throw new ArgumentException($"'{name}' is not a valid C# identifier.", nameof(name));
            }

            Name = name;
            Type = type;
        }

        /// <summary>
        /// A fresh <see cref="VariableSpecifier"/> snapshotting this local: <see cref="VariableSpecifier.Scope"/>
        /// is <see cref="VariableScope.Local"/>, <see cref="VariableSpecifier.DeclaringType"/> is
        /// <see langword="null"/> (a local has no declaring type), visibilities are
        /// <see cref="MemberVisibility.Private"/> and modifiers are <see cref="VariableModifiers.None"/>.
        /// Recomputed on every access; not cached.
        /// </summary>
        /// <returns>The specifier.</returns>
        public VariableSpecifier ToSpecifier() =>
            new VariableSpecifier(Name, Type, MemberVisibility.Private, MemberVisibility.Private,
                declaringType: null, VariableModifiers.None)
            {
                Scope = VariableScope.Local,
            };
    }
}
