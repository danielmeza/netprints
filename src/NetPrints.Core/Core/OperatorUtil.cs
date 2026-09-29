#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NetPrints.Core
{
    /// <summary>
    /// How an operator method (named <c>op_*</c> per the .NET operator overload convention) is
    /// displayed and translated back to its C# operator syntax.
    /// </summary>
    public class OperatorInfo
    {
        /// <summary>
        /// Human-readable name shown in the editor (eg. "Add", "Greater than or equal").
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// The C# operator symbol (eg. "+", "&gt;=").
        /// </summary>
        public string Symbol { get; }

        /// <summary>
        /// Whether this is a unary operator (one operand) rather than binary (two operands).
        /// </summary>
        public bool Unary { get; }

        /// <summary>
        /// For a unary operator, whether the symbol is written after the operand (eg. postfix
        /// <c>x++</c>) rather than before it (eg. prefix <c>-x</c>). Unused for a binary operator.
        /// </summary>
        public bool UnaryRightPosition { get; }

        /// <summary>
        /// Creates an operator descriptor.
        /// </summary>
        /// <param name="displayName">Human-readable name shown in the editor.</param>
        /// <param name="symbol">The C# operator symbol.</param>
        /// <param name="unary">Whether this is a unary operator.</param>
        /// <param name="unaryRightPosition">For a unary operator, whether the symbol is written after the operand.</param>
        public OperatorInfo(string displayName, string symbol, bool unary, bool unaryRightPosition = false)
        {
            DisplayName = displayName;
            Symbol = symbol;
            Unary = unary;
            UnaryRightPosition = unaryRightPosition;
        }
    }

    /// <summary>
    /// Recognizes operator-overload methods (by their <c>op_*</c> name) and maps them to their
    /// <see cref="OperatorInfo"/>, so the translator can emit the operator syntax instead of a method
    /// call.
    /// </summary>
    public static class OperatorUtil
    {
        private const string OperatorPrefix = "op_";

        /// <summary>
        /// Mapping from operator method name to operator definitions (display name, symbol, arity, position).
        /// </summary>
        private static readonly Dictionary<string, OperatorInfo> operatorSymbols = new Dictionary<string, OperatorInfo>()
        {
            // Unary
            ["op_Increment"] = new OperatorInfo("Increment", "++", true, true),
            ["op_Decrement"] = new OperatorInfo("Decrement", "--", true, true),
            ["op_UnaryPlus"] = new OperatorInfo("Unary Plus", "+", true),
            ["op_UnaryNegation"] = new OperatorInfo("Unary Negation", "-", true),
            ["op_LogicalNot"] = new OperatorInfo("Not", "!", true),

            // Binary
            ["op_Addition"] = new OperatorInfo("Add", "+", false),
            ["op_Subtraction"] = new OperatorInfo("Subtract", "-", false),
            ["op_Multiply"] = new OperatorInfo("Multiply", "*", false),
            ["op_Division"] = new OperatorInfo("Divide", "/", false),
            ["op_Modulus"] = new OperatorInfo("Modulus", "%", false),
            ["op_GreaterThan"] = new OperatorInfo("Greater than", ">", false),
            ["op_GreaterThanOrEqual"] = new OperatorInfo("Greater than or equal", ">=", false),
            ["op_Equality"] = new OperatorInfo("Equal", "==", false),
            ["op_Inequality"] = new OperatorInfo("Not Equal", "!=", false),
            ["op_LessThan"] = new OperatorInfo("Less than", "<", false),
            ["op_LessThanOrEqual"] = new OperatorInfo("Less than or equal", "<=", false),
            ["op_BitwiseAnd"] = new OperatorInfo("Bitwise AND", "&", false),
            ["op_BitwiseOr"] = new OperatorInfo("Bitwise OR", "|", false),
            ["op_ExclusiveOr"] = new OperatorInfo("Bitwise XOR", "^", false),
            ["op_LeftShift"] = new OperatorInfo("Shift Left", "<<", false),
            ["op_RightShift"] = new OperatorInfo("Shift Right", ">>", false),

            // Custom (not part of .NET symbols)
            ["op_BitwiseNot"] = new OperatorInfo("Bitwise NOT", "~", true),
            ["op_LogicalAnd"] = new OperatorInfo("And", "&&", false),
            ["op_LogicalOr"] = new OperatorInfo("Or", "||", false),
        };

        /// <summary>
        /// Returns whether the method specifier is an operator.
        /// </summary>
        /// <param name="methodSpecifier">Method specifier to check.</param>
        /// <returns><see langword="true"/> if the method's name is a recognized <c>op_*</c> operator overload.</returns>
        public static bool IsOperator(MethodSpecifier methodSpecifier) =>
            operatorSymbols.ContainsKey(methodSpecifier.Name);

        /// <summary>
        /// Tries to get operator info for a method specifier.
        /// </summary>
        /// <param name="methodSpecifier">Method specifier to find operator info for.</param>
        /// <param name="operatorInfo">Operator info for the method specifier if found.</param>
        /// <returns><see langword="true"/> if <paramref name="methodSpecifier"/>'s name is a recognized <c>op_*</c> operator overload.</returns>
        public static bool TryGetOperatorInfo(MethodSpecifier methodSpecifier, [MaybeNullWhen(false)] out OperatorInfo operatorInfo) =>
            operatorSymbols.TryGetValue(methodSpecifier.Name, out operatorInfo);
    }
}
