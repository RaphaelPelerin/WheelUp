using UnityEngine;
using UnityEngine.EventSystems;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Zone invisible plein écran placée derrière les boutons : glisser le doigt (ou la souris)
    /// hors des boutons fait tourner la caméra. Les boutons, dessinés au-dessus, gardent la priorité,
    /// et un second doigt peut tourner la caméra pendant que le premier tient GAZ ou LEVER.
    /// </summary>
    public class CameraDragZone : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const int NoPointer = int.MinValue;

        public MotoCameraRig rig;

        // Un seul doigt pilote la caméra à la fois.
        int activePointer = NoPointer;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (rig == null || activePointer != NoPointer) return;
            activePointer = eventData.pointerId;
            rig.SetLookDragging(true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (rig == null || eventData.pointerId != activePointer) return;
            rig.AddLookInput(eventData.delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointer) return;
            ReleasePointer();
        }

        void OnDisable()
        {
            if (activePointer != NoPointer) ReleasePointer();
        }

        void ReleasePointer()
        {
            activePointer = NoPointer;
            if (rig != null) rig.SetLookDragging(false);
        }
    }
}
