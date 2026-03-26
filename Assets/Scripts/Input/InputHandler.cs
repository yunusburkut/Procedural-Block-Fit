using UnityEngine;
using UnityEngine.EventSystems;
using Blokfit.Pieces;

namespace Blokfit.Input
{

    public class InputHandler : MonoBehaviour
    {
        public DragState CurrentDrag { get; private set; }
        public bool IsDragging => CurrentDrag != null;

        public bool BeginDrag(PieceBehaviour piece, PointerEventData eventData, Vector2 pointerOffset)
        {
            if (IsDragging) return false;

            CurrentDrag = new DragState(
                piece,
                eventData.pointerId,
                piece.transform.position,
                pointerOffset
            );
            return true;
        }

        public void EndDrag()
        {
            CurrentDrag = null;
        }
    }
}
