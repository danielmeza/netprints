#nullable enable
using System.Collections.Generic;

namespace NetPrints.Graph
{
    /// <summary>
    /// A node whose list of pins the user grows and shrinks with the +/- buttons. The editor asks for this role
    /// instead of testing node types, so an extension node gets the buttons by implementing it.
    /// </summary>
    public interface IPinListEditor
    {
        /// <summary>Gets a value indicating whether the +/- buttons are shown for this node.</summary>
        bool ShowsPinButtons { get; }

        /// <summary>Gets the tooltip of the "+" button, describing what it adds.</summary>
        string AddPinLabel { get; }

        /// <summary>Gets the tooltip of the "-" button, describing what it removes.</summary>
        string RemovePinLabel { get; }

        /// <summary>Gets a value indicating whether there is a pin to remove.</summary>
        bool CanRemovePin { get; }

        /// <summary>Gets a value indicating whether the edit changes the signature of the method that holds the node.</summary>
        bool ChangesMethodSignature { get; }

        /// <summary>Gets, for a signature change, where each argument of the callers comes from after an add; null keeps the order.</summary>
        IReadOnlyList<int>? SlotsAfterAdd { get; }

        /// <summary>Gets, for a signature change, where each argument of the callers comes from after a remove; null keeps the order.</summary>
        IReadOnlyList<int>? SlotsAfterRemove { get; }

        /// <summary>Adds one pin at the end of the list.</summary>
        void AddPin();

        /// <summary>Removes the last pin of the list, if there is one.</summary>
        void RemovePin();
    }
}
