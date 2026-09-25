#nullable enable
using System;
using System.Linq;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// Node representing the creation of a delegate (method pointer).
    /// </summary>
    [DataContract]
    public partial class MakeDelegateNode : Node
    {
        /// <summary>
        /// Specifier describing the method the delegate is created for.
        /// </summary>
        [ObservableProperty]
        [DataMember]
        public partial MethodSpecifier MethodSpecifier { get; private set; }

        /// <summary>
        /// The target this delegate is for ("this").
        /// Accessing this for static methods (IsFromStaticMethod==true)
        /// will throw an exception.
        /// </summary>
        public NodeInputDataPin TargetPin
        {
            get => InputDataPins[0];
        }

        /// <summary>
        /// Whether the delegate is for a static method.
        /// </summary>
        public bool IsFromStaticMethod
        {
            get => MethodSpecifier.Modifiers.HasFlag(MethodModifiers.Static);
        }

        /// <summary>
        /// Adds this node to <paramref name="graph"/> and gives it a target pin (unless the method is
        /// static) and its delegate-value output pin, typed <see cref="Action"/> or <see cref="Func{TResult}"/>
        /// (with the method's parameter and return types as generic arguments) as appropriate.
        /// </summary>
        /// <param name="graph">Graph the node belongs to.</param>
        /// <param name="methodSpecifier">Specifier for the method the delegate is created for.</param>
        /// <exception cref="NotImplementedException">
        /// <paramref name="methodSpecifier"/> has more than one return type (multiple return values
        /// have no <see cref="Func{TResult}"/> equivalent).
        /// </exception>
        public MakeDelegateNode(NodeGraph graph, MethodSpecifier methodSpecifier)
            : base(graph)
        {
            MethodSpecifier = methodSpecifier;

            if (!IsFromStaticMethod)
            {
                AddInputDataPin("Target", methodSpecifier.DeclaringType);
            }

            TypeSpecifier delegateType;

            if (methodSpecifier.ReturnTypes.Count == 0)
            {
                delegateType = new TypeSpecifier("System.Action", false, false, methodSpecifier.ArgumentTypes);
            }
            else if (methodSpecifier.ReturnTypes.Count == 1)
            {
                delegateType = new TypeSpecifier("System.Func", false, false, methodSpecifier.ArgumentTypes.Concat(methodSpecifier.ReturnTypes).ToList());
            }
            else
            {
                throw new NotImplementedException("Only 0 and 1 return types are supported right now.");
            }

            AddOutputDataPin(delegateType.ShortName, delegateType);
        }

        /// <summary>
        /// Returns "Make Delegate from " followed by the declaring type and method name (for a static
        /// method) or just the method name.
        /// </summary>
        /// <returns>The node's display string.</returns>
        public override string ToString()
        {
            if (IsFromStaticMethod)
            {
                return $"Make Delegate from {MethodSpecifier.DeclaringType} {MethodSpecifier.Name}";
            }
            else
            {
                return $"Make Delegate from {MethodSpecifier.Name}";
            }
        }
    }
}
