using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Jauge verticale discrète affichant l'angle de wheeling en temps réel, en trois zones :
    /// montée (sous l'équilibre), équilibre (vert) et critique (rouge, clignotante).
    /// Les bornes des zones sont lues sur le contrôleur, donc tout réglage d'angle s'y reflète.
    /// </summary>
    public class WheelieGauge
    {
        static readonly Color FrameColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color FrameAlertColor = new Color(0.95f, 0.15f, 0.15f, 0.9f);
        static readonly Color RisingZoneColor = new Color(0.45f, 0.62f, 0.85f, 0.45f);
        static readonly Color BalanceZoneColor = new Color(0.25f, 0.85f, 0.4f, 0.65f);
        static readonly Color CriticalZoneColor = new Color(0.95f, 0.2f, 0.2f, 0.65f);
        static readonly Color BalanceColor = new Color(0.3f, 0.95f, 0.45f, 1f);
        static readonly Color CriticalColor = new Color(1f, 0.25f, 0.25f, 1f);

        const float BlinkRate = 7f;
        const float IdleAlpha = 0.35f;

        MotorcycleController controller;
        CanvasGroup group;
        Image frame;
        RectTransform marker;
        Image markerImage;
        TextMeshProUGUI angleText;
        TextMeshProUGUI statusText;

        public void Build(Transform parent, UITheme theme, MotorcycleController target)
        {
            controller = target;

            // Coin haut-droit, sous le bouton MENU ; le bas-droit est occupé par GAZ / FREIN / LEVER.
            var root = UIFactory.CreateUIObject("WheelieGauge", parent);
            UIFactory.SetRect(root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-78, -360), new Vector2(-42, -100));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            frame = AddBand(root, "Frame", FrameColor, 0f, 1f, 4f);

            float fall = Mathf.Max(1f, controller.wheelieFallAngle);
            float sweetMin = Mathf.Clamp01(controller.wheelieSweetMin / fall);
            float sweetMax = Mathf.Clamp01(controller.wheelieSweetMax / fall);
            AddBand(root, "RisingZone", RisingZoneColor, 0f, sweetMin, 0f);
            AddBand(root, "BalanceZone", BalanceZoneColor, sweetMin, sweetMax, 0f);
            AddBand(root, "CriticalZone", CriticalZoneColor, sweetMax, 1f, 0f);

            marker = UIFactory.CreateUIObject("Marker", root);
            markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.raycastTarget = false;
            SetMarker(0f);

            angleText = UIFactory.AddText(root, "AngleText", "0°", 18, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60, -30), new Vector2(60, -8));
            statusText = UIFactory.AddText(root, "StatusText", "", 14, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60, -50), new Vector2(60, -30));
        }

        public void Tick()
        {
            if (controller == null) return;

            float angle = controller.WheelieAngle;
            SetMarker(Mathf.Clamp01(angle / Mathf.Max(1f, controller.wheelieFallAngle)));

            WheelieZone zone = controller.CurrentWheelieZone;
            bool blinkOn = Mathf.Repeat(Time.unscaledTime * BlinkRate, 1f) < 0.5f;

            Color accent;
            string status;
            switch (zone)
            {
                case WheelieZone.Balance:
                    accent = BalanceColor;
                    status = "ÉQUILIBRE";
                    break;
                case WheelieZone.Critical:
                    accent = blinkOn ? CriticalColor : Color.white;
                    status = "FREINE !";
                    break;
                default:
                    accent = Color.white;
                    status = "";
                    break;
            }

            markerImage.color = accent;
            angleText.color = accent;
            statusText.color = accent;
            angleText.text = $"{Mathf.RoundToInt(angle)}°";
            statusText.text = status;
            frame.color = zone == WheelieZone.Critical && blinkOn ? FrameAlertColor : FrameColor;
            group.alpha = zone == WheelieZone.Flat ? IdleAlpha : 1f;
        }

        void SetMarker(float normalized)
        {
            UIFactory.SetRect(marker, new Vector2(0f, normalized), new Vector2(1f, normalized), new Vector2(-8f, -3f), new Vector2(8f, 3f));
        }

        static Image AddBand(Transform parent, string name, Color color, float yMin, float yMax, float padding)
        {
            var band = UIFactory.AddPanel(parent, name, color, new Vector2(0f, yMin), new Vector2(1f, yMax),
                new Vector2(-padding, -padding), new Vector2(padding, padding));
            band.raycastTarget = false;
            return band;
        }
    }
}
