using UnityEngine.EventSystems;
using Blokfit.Pieces;
using UnityEngine;

namespace Blokfit.Input
{
    /// <summary>
    /// Central gatekeeper for drag input.
    /// Ensures only one piece is dragged at a time and allows blocking all input
    /// during non-interactive game states (e.g. level-complete screen).
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        /// <summary>Active drag, or null when idle.</summary>
        public DragState CurrentDrag { get; private set; }

        public bool IsDragging => CurrentDrag != null;

        /// <summary>When true, no new drags can begin (set by GameManager on level-complete).</summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// Attempts to start a drag for <paramref name="piece"/>.
        /// Returns false if input is blocked or another drag is already active.
        /// </summary>
        public bool BeginDrag(PieceBehaviour piece, PointerEventData eventData)
        {
            if (IsBlocked) return false;
            if (IsDragging) return false;

            CurrentDrag = new DragState(piece, eventData.pointerId);
            return true;
        }

        /// <summary>Clears the active drag state.</summary>
        public void EndDrag()
        {
            CurrentDrag = null;
        }
    }
}