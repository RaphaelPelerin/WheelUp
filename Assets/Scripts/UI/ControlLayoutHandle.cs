using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Prise d'une commande dans l'éditeur de disposition : suit le doigt qui l'a saisie et rapporte sa position
    /// en fraction du parent (0,0 en bas à gauche), l'éditeur décidant seul où la commande se pose.
    /// Un seul doigt la tient à la fois ; un deuxième peut déplacer une autre commande en même temps.
    /// </summary>
    public class ControlLayoutHandle : MonoBehaviour, IInitializePotentialDragHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        Action<Vector2> onPressed;
        Action<Vector2> onDragged;
        Action onReleased;
        int activePointer = NoPointer;

        public void Init(Action<Vector2> pressed, Action<Vector2> dragged, Action released)
        {
            onPressed = pressed;
            onDragged = dragged;
            onReleased = released;
        }

        // Suit dès le premier pixel, sans le seuil de glissement de l'EventSystem.
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != NoPointer || !TryGetFraction(eventData, out Vector2 fraction)) return;
            activePointer = eventData.pointerId;
            onPressed?.Invoke(fraction);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer || !TryGetFraction(eventData, out Vector2 fraction)) return;
            onDragged?.Invoke(fraction);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Release();
        }

        void OnDisable()
        {
            if (activePointer != NoPointer) Release();
        }

        void Release()
        {
            activePointer = NoPointer;
            onReleased?.Invoke();
        }

        bool TryGetFraction(PointerEventData eventData, out Vector2 fraction)
        {
            fraction = default;
            var parent = transform.parent as RectTransform;
            if (parent == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 local)) return false;

            Rect rect = parent.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            fraction = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
            return true;
        }
    }
}
