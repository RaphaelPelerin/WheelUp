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

        /// <summary>Largeur de la barre, en unités de canvas.</summary>
        public const float Width = 36f;
        /// <summary>Place prise par le titre au-dessus de la barre.</summary>
        public const float SpaceAbove = 42f;
        /// <summary>Place prise par l'angle et le statut sous la barre.</summary>
        public const float SpaceBelow = 82f;
        // Le repère de l'angle déborde de la barre de chaque côté : la barre est décalée d'autant du bord.
        const float MarkerOverhang = 8f;

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

        /// <summary>
        /// Construit la jauge le long du bord gauche de <paramref name="parent"/> (la zone sûre du HUD). La barre
        /// s'étire sur toute la hauteur libre entre <paramref name="top"/> et <paramref name="bottom"/>, titre et
        /// libellés compris : elle s'adapte à la hauteur de l'écran sans jamais chevaucher ses voisins.
        /// </summary>
        /// <param name="left">Retrait depuis le bord gauche, en unités de canvas.</param>
        /// <param name="top">Distance du haut de la zone réservée (titre compris) au haut du parent.</param>
        /// <param name="bottom">Distance du bas de la zone réservée (libellés compris) au bas du parent.</param>
        public void Build(Transform parent, UITheme theme, MotorcycleController target, float left, float top, float bottom)
        {
            controller = target;

            var root = UIFactory.CreateUIObject("WheelieGauge", parent);
            float barLeft = left + MarkerOverhang;
            UIFactory.SetRect(root, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(barLeft, bottom + SpaceBelow), new Vector2(barLeft + Width, -top - SpaceAbove));
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

            // Les trois libellés de la jauge sont passés au corps commun : leurs boîtes s'élargissent
            // d'autant, sans quoi le texte serait rogné au lieu d'être simplement plus gros.
            // Alignés sur le bord gauche de la jauge, donc sur la marge : ils ne débordent jamais de l'écran
            // du côté de la Dynamic Island, quelle que soit la longueur du texte.
            titleText = UIFactory.AddText(root, "TitleText", "", UITheme.FontLabel, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(-MarkerOverhang, 8), new Vector2(280, SpaceAbove));
            angleText = UIFactory.AddText(root, "AngleText", "0°", UITheme.FontBody, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-MarkerOverhang, -44), new Vector2(180, -8));
            statusText = UIFactory.AddText(root, "StatusText", "", UITheme.FontLabel, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(-MarkerOverhang, -SpaceBelow), new Vector2(280, -46));
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
                    status = stoppie ? "CHUTE !" : "FREIN AR !";
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
