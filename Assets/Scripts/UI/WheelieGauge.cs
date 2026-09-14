using TMPro;
using UnityEngine;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Jauge verticale discrète affichant l'angle d'équilibre en temps réel : celui du wheeling, ou celui de la
    /// roue avant dès que l'arrière décolle. Trois zones : montée (sous l'équilibre), équilibre (vert) et
    /// critique (rouge, clignotante). Les bornes sont lues sur le contrôleur, donc tout réglage d'angle s'y reflète.
    ///
    /// La barre (<see cref="WheelieBar"/>) reprend le trait du cadran du compteur, et ses libellés la typographie des
    /// autres instruments du HUD : capitales espacées, chiffres en gras, liseré sombre plutôt que fond.
    /// </summary>
    public class WheelieGauge
    {
        const float BlinkRate = 7f;
        // Moto à plat, la jauge s'efface à demi ; elle revient en plein, en fondu, dès que la roue se lève.
        const float IdleAlpha = 0.45f;
        const float FadeSpeed = 5f;

        /// <summary>Largeur de la jauge, repère et graduations compris, en unités de canvas.</summary>
        public const float Width = WheelieBar.DrawnWidth;
        /// <summary>Place prise par le titre au-dessus de la barre.</summary>
        public const float SpaceAbove = 42f;
        /// <summary>Place prise par l'angle et le statut sous la barre.</summary>
        public const float SpaceBelow = 82f;

        static readonly string[] AngleStrings = new string[181];

        MotorcycleController controller;
        CanvasGroup group;
        WheelieBar bar;
        TextMeshProUGUI titleText;
        TextMeshProUGUI angleText;
        TextMeshProUGUI statusText;
        int shownAngle = -1;
        bool shownStoppie;

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
            UIFactory.SetRect(root, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(left, bottom + SpaceBelow), new Vector2(left + Width, -top - SpaceAbove));
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = IdleAlpha;

            var barRect = UIFactory.CreateUIObject("Bar", root);
            UIFactory.SetRect(barRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bar = barRect.gameObject.AddComponent<WheelieBar>();
            bar.raycastTarget = false;

            // Alignés sur le bord gauche de la jauge, donc sur la marge : ils ne débordent jamais de l'écran du côté
            // de la Dynamic Island, quelle que soit la longueur du texte. Sans fond, détachés par un liseré sombre
            // comme les points de prouesse et la vitesse.
            titleText = StuntScoreHud.Outlined(UIFactory.AddText(root, "TitleText", "", UITheme.FontLabel, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(280f, SpaceAbove), FontStyles.Bold), 0.14f);
            titleText.characterSpacing = 8f;
            angleText = StuntScoreHud.Outlined(UIFactory.AddText(root, "AngleText", "0°", UITheme.FontTitle, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -46f), new Vector2(180f, -4f), FontStyles.Bold), 0.2f);
            statusText = StuntScoreHud.Outlined(UIFactory.AddText(root, "StatusText", "", UITheme.FontLabel, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, -SpaceBelow), new Vector2(280f, -48f), FontStyles.Bold), 0.14f);
            statusText.characterSpacing = 8f;

            ShowMode(false);
        }

        public void Tick()
        {
            if (controller == null) return;

            // La roue avant prend la main dès que l'arrière décolle : le wheeling est alors forcément à plat.
            bool stoppie = controller.StoppieAngle > 0f;
            if (stoppie != shownStoppie) ShowMode(stoppie);

            // Relues à chaque image : la fiche technique de la moto, qui fixe l'équilibre du wheeling, peut n'être
            // appliquée qu'après la construction du HUD. La barre ne se redessine que si elles ont changé.
            if (stoppie) bar.SetZones(controller.stoppieSweetMin, controller.stoppieSweetMax, controller.stoppieFallAngle);
            else bar.SetZones(controller.wheelieSweetMin, controller.wheelieSweetMax, controller.wheelieFallAngle);

            float angle = stoppie ? controller.StoppieAngle : controller.WheelieAngle;
            float fallAngle = stoppie ? controller.stoppieFallAngle : controller.wheelieFallAngle;
            bar.Value = angle / Mathf.Max(1f, fallAngle);

            WheelieZone zone = stoppie ? controller.CurrentStoppieZone : controller.CurrentWheelieZone;
            bool blinkOn = Mathf.Repeat(Time.unscaledTime * BlinkRate, 1f) < 0.5f;

            Color accent;
            string status;
            switch (zone)
            {
                case WheelieZone.Balance:
                    accent = WheelieBar.BalanceColor;
                    status = "ÉQUILIBRE";
                    break;
                case WheelieZone.Critical:
                    accent = blinkOn ? WheelieBar.CriticalColor : Color.white;
                    // Sur la roue avant, rien ne rattrape la moto au-delà de l'équilibre.
                    status = stoppie ? "CHUTE !" : "FREIN !";
                    break;
                default:
                    accent = Color.white;
                    status = "";
                    break;
            }

            bar.Alert = zone == WheelieZone.Critical && blinkOn;
            angleText.color = accent;
            statusText.color = accent;
            titleText.color = accent;
            statusText.text = status;

            int rounded = Mathf.Clamp(Mathf.RoundToInt(angle), 0, AngleStrings.Length - 1);
            if (rounded != shownAngle)
            {
                shownAngle = rounded;
                angleText.text = AngleStrings[rounded] ??= rounded + "°";
            }

            float wanted = zone == WheelieZone.Flat ? IdleAlpha : 1f;
            group.alpha = Mathf.MoveTowards(group.alpha, wanted, FadeSpeed * Time.unscaledDeltaTime);
        }

        void ShowMode(bool stoppie)
        {
            shownStoppie = stoppie;
            titleText.text = stoppie ? "ROUE AVANT" : "WHEELING";
        }
    }
}
