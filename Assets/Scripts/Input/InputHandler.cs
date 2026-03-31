using UnityEngine.EventSystems;
using Blokfit.Pieces;
using UnityEngine;

namespace Blokfit.Input
{
    public class InputHandler : MonoBehaviour
    {
        public DragState CurrentDrag { get; private set; }
        public bool IsDragging => CurrentDrag != null;
        public bool IsBlocked { get; set; }

        public bool BeginDrag(PieceBehaviour piece, PointerEventData eventData)
        {
            if (IsBlocked) return false;
            if (IsDragging) return false;

            CurrentDrag = new DragState(piece, eventData.pointerId);
            return true;
        }

        public void EndDrag()
        {
            CurrentDrag = null;
        }
    }
}