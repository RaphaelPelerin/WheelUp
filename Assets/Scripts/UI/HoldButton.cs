using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace WheelingMoto.UI
{
    /// <summary>Bouton tactile "maintenir" (accélérer/freiner/tourner/cabrer) : Button.onClick ne réagit qu'au relâchement, il faut ici le maintien.</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public UnityEvent OnPressed = new UnityEvent();
        public UnityEvent OnReleased = new UnityEvent();

        bool isPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            isPressed = true;
            OnPressed.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isPressed) Release();
        }

        void OnDisable()
        {
            Release();
        }

        void Release()
        {
            if (!isPressed) return;
            isPressed = false;
            OnReleased.Invoke();
        }
    }
}
