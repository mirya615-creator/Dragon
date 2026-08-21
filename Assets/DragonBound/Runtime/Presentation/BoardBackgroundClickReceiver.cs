using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DragonBound.Presentation
{
    /// <summary>
    /// Receives clicks only where no cell or unit is on top of the shared board canvas.
    /// It is intentionally presentation-only and does not participate in gameplay input.
    /// </summary>
    public sealed class BoardBackgroundClickReceiver : MonoBehaviour, IPointerClickHandler
    {
        public event Action Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }
    }
}
