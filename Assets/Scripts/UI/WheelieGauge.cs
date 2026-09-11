using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Jauge verticale discrète affichant l'angle d'équilibre en temps réel : celui du wheeling, ou celui de la
    /// roue avant dès que l'arrière décolle. Trois zones : montée (sous l'équilibre), équilibre (vert) et
    /// critique (rouge, clignotante). Les bornes sont lues sur le contrôleur, donc tout réglage d'angle s'y reflète.
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
        GameObject wheelieZones;
        GameObject stoppieZones;
        RectTransform marker;
        Image markerImage;
        TextMeshProUGUI titleText;
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
            wheelieZones = AddZones(root, "WheelieZones", controller.wheelieSweetMin, controller.wheelieSweetMax, controller.wheelieFallAngle);
            stoppieZones = AddZones(root, "StoppieZones", controller.stoppieSweetMin, controller.stoppieSweetMax, controller.stoppieFallAngle);
            stoppieZones.SetActive(false);

            marker = UIFactory.CreateUIObject("Marker", root);
            markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.raycastTarget = false;
            SetMarker(0f);

            titleText = UIFactory.AddText(root, "TitleText", "", 13, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-70, 8), new Vector2(70, 28));
            angleText = UIFactory.AddText(root, "AngleText", "0°", 18, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60, -30), new Vector2(60, -8));
            statusText = UIFactory.AddText(root, "StatusText", "", 14, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-60, -50), new Vector2(60, -30));
        }

        public void Tick()
        {
            if (controller == null) return;

            // La roue avant prend la main dès que l'arrière décolle : le wheeling est alors forcément à plat.
            bool stoppie = controller.StoppieAngle > 0f;
            if (stoppieZones.activeSelf != stoppie)
            {
                stoppieZones.SetActive(stoppie);
                wheelieZones.SetActive(!stoppie);
            }

            float angle = stoppie ? controller.StoppieAngle : controller.WheelieAngle;
            float fallAngle = stoppie ? controller.stoppieFallAngle : controller.wheelieFallAngle;
            SetMarker(Mathf.Clamp01(angle / Mathf.Max(1f, fallAngle)));

            WheelieZone zone = stoppie ? controller.CurrentStoppieZone : controller.CurrentWheelieZone;
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
                    // Sur la roue avant, rien ne rattrape la moto au-delà de l'équilibre.
                    status = stoppie ? "CHUTE !" : "FREINE !";
                    break;
                default:
                    accent = Color.white;
                    status = "";
                    break;
            }

            markerImage.color = accent;
            angleText.color = accent;
            statusText.color = accent;
            titleText.color = accent;
            titleText.text = stoppie ? "ROUE AVANT" : "WHEELING";
            angleText.text = $"{Mathf.RoundToInt(angle)}°";
            statusText.text = status;
            frame.color = zone == WheelieZone.Critical && blinkOn ? FrameAlertColor : FrameColor;
            group.alpha = zone == WheelieZone.Flat ? IdleAlpha : 1f;
        }

        void SetMarker(float normalized)
        {
            UIFactory.SetRect(marker, new Vector2(0f, normalized), new Vector2(1f, normalized), new Vector2(-8f, -3f), new Vector2(8f, 3f));
        }

        /// <summary>Bandes montée / équilibre / critique, à l'échelle de l'angle de chute.</summary>
        static GameObject AddZones(Transform parent, string name, float sweetMinAngle, float sweetMaxAngle, float fallAngle)
        {
            var zones = UIFactory.CreateUIObject(name, parent);
            UIFactory.SetRect(zones, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            float fall = Mathf.Max(1f, fallAngle);
            float sweetMin = Mathf.Clamp01(sweetMinAngle / fall);
            float sweetMax = Mathf.Clamp01(sweetMaxAngle / fall);
            AddBand(zones, "RisingZone", RisingZoneColor, 0f, sweetMin, 0f);
            AddBand(zones, "BalanceZone", BalanceZoneColor, sweetMin, sweetMax, 0f);
            AddBand(zones, "CriticalZone", CriticalZoneColor, sweetMax, 1f, 0f);
            return zones.gameObject;
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
