using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Surface tactile de l'écran d'ouverture. Elle sépare trois gestes sur la même zone :
    /// un appui maintenu (qui charge l'ouverture), un glissement (qui fait tourner le coffre) et
    /// un appui bref (qui fait avancer la révélation des lots).
    ///
    /// Le seuil de glissement est celui de l'EventSystem : tant qu'il n'est pas franchi, le geste
    /// reste un maintien, ce qui évite qu'un doigt légèrement tremblant annule la charge.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestTouchSurface : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Doigt posé : le maintien commence.</summary>
        public Action Pressed;
        /// <summary>Doigt levé sans avoir glissé : le geste était un appui bref.</summary>
        public Action Tapped;
        /// <summary>Le geste est devenu un glissement, ou le doigt s'est levé : le maintien s'arrête.</summary>
        public Action HoldEnded;
        /// <summary>Déplacement du doigt, en unités de référence du Canvas et non en pixels écran.</summary>
        public Action<Vector2> Dragged;

        public bool IsHeld { get; private set; }

        Canvas canvas;
        bool dragging;

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsHeld = true;
            dragging = false;
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!IsHeld) return;

            IsHeld = false;
            HoldEnded?.Invoke();

            // Un glissement n'est pas un appui : sans ce filtre, tourner le coffre ferait aussi
            // sauter une révélation à chaque fois qu'on lève le doigt.
            if (!dragging) Tapped?.Invoke();
            dragging = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            IsHeld = false;
            HoldEnded?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Le Canvas met l'UI à l'échelle de la résolution : diviser par son facteur rend le
            // geste identique sur un écran de portable et sur un moniteur.
            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Dragged?.Invoke(eventData.delta / scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
        }
    }
}
