using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Joystick de direction, au choix dans les Paramètres à la place des flèches : on le pousse à gauche ou à
    /// droite, la direction suit l'écart ; relâché, il revient au milieu et la moto se redresse.
    /// Seul l'axe horizontal compte. Un seul doigt le pilote : les autres restent libres pour GAZ, LEVER ou FREIN.
    /// </summary>
    public class SteerJoystick : MonoBehaviour, IInitializePotentialDragHandler, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;
        const int CircleTextureSize = 128;

        [Tooltip("Zone morte au centre, en fraction de la course.")]
        public float deadZone = 0.12f;
        [Tooltip("Vitesse de retour de la manette au centre une fois relâchée.")]
        public float returnSpeed = 14f;

        static Sprite circleSprite;

        RectTransform area;
        RectTransform knob;
        Action<float> onSteer;
        Action<bool> onHeld;
        int activePointer = NoPointer;
        float knobOffset;

        /// <summary>Disque blanc lissé, dessiné à la volée : depuis Unity 6, les sprites d'UI intégrés ne sont plus exposés.</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null) circleSprite = CreateCircleSprite(CircleTextureSize);
                return circleSprite;
            }
        }

        public void Init(RectTransform knobRect, Action<float> steer, Action<bool> held)
        {
            area = (RectTransform)transform;
            knob = knobRect;
            onSteer = steer;
            onHeld = held;
        }

        float Travel => Mathf.Max(1f, (area.rect.width - knob.rect.width) * 0.5f);

        // Réagit dès le premier pixel, sans le seuil de glissement de l'EventSystem.
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointer != NoPointer) return;
            activePointer = eventData.pointerId;
            onHeld?.Invoke(true);
            Follow(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Follow(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == activePointer) Release();
        }

        void OnDisable()
        {
            if (activePointer != NoPointer) Release();
        }

        void Follow(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local)) return;

            knobOffset = Mathf.Clamp(local.x - area.rect.center.x, -Travel, Travel);
            SetKnob(knobOffset);

            float x = knobOffset / Travel;
            onSteer?.Invoke(Mathf.Sign(x) * Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(x)));
        }

        void Release()
        {
            // Direction remise à zéro tout de suite ; la manette, elle, glisse jusqu'au centre.
            activePointer = NoPointer;
            onHeld?.Invoke(false);
            onSteer?.Invoke(0f);
        }

        void Update()
        {
            if (activePointer != NoPointer || knobOffset == 0f) return;

            knobOffset = Mathf.Lerp(knobOffset, 0f, 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime));
            if (Mathf.Abs(knobOffset) < 0.5f) knobOffset = 0f;
            SetKnob(knobOffset);
        }

        void SetKnob(float x) => knob.anchoredPosition = new Vector2(x, 0f);

        static Sprite CreateCircleSprite(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            float radius = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    float alpha = Mathf.Clamp01(radius - distance);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "Circle";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
