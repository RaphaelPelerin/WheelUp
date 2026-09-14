using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Gabarit des commandes de conduite, partagé par le HUD et par l'éditeur de disposition des Paramètres :
    /// les deux doivent poser chaque bouton au même endroit et à la même taille, sinon ce que le joueur règle ne
    /// serait pas ce qu'il retrouve en roulant.
    ///
    /// Tout est en unités du canvas (référence 1920 x 1080), mesuré depuis le coin bas-gauche de la zone sûre.
    /// Une commande reste à sa place par défaut tant que le joueur ne l'a pas déplacée. Déplacée, sa place est
    /// gardée en fraction de la zone sûre (<see cref="SettingsManager.TryGetControlPosition"/>), pour suivre un
    /// changement d'écran, puis ramenée dans les bornes : jamais contre le bord, jamais sous VUE et PAUSE.
    /// </summary>
    public class ControlLayout
    {
        public const float Gap = 20f;
        public const float SteerButtonSize = 250f;
        public const float JoystickSize = 330f;
        public const float JoystickKnobSize = 140f;
        public const float PedalWidth = 230f;
        public const float PedalHeight = 190f;
        // Rangée du haut : VUE et PAUSE, assez hauts pour un doigt (35 pt sur iPhone).
        public const float TopButtonWidth = 190f;
        public const float TopButtonHeight = 88f;
        // Cumul des prouesses en haut à gauche ; le compteur en direct est à droite, sous VUE et PAUSE.
        public const float TotalsHeight = 58f;
        const float LiveScoreHeight = StuntScoreHud.LiveHeight;
        // Hauteur minimale de la barre de la jauge : en dessous, l'angle ne se lirait plus.
        const float GaugeMinBar = 120f;
        // Marge au bord de la zone sûre : jamais moins que MinMargin, et sinon cette part du petit côté de
        // l'écran. Sur un téléphone à coins arrondis, 24 unités (2 mm) frôlent l'arrondi et la barre d'accueil.
        const float MinMargin = 24f;
        const float MarginShare = 0.045f;
        // Couloir laissé libre entre les deux piles de boutons : la route doit rester visible entre les pouces.
        const float MinCorridor = 200f;

        /// <summary>Zone sûre utilisable, en unités de canvas.</summary>
        public Vector2 SafeSize { get; private set; }

        /// <summary>Marge aux bords de la zone sûre.</summary>
        public float Margin { get; private set; }

        /// <summary>Facteur appliqué à tout le gabarit pour qu'il tienne dans l'écran.</summary>
        public float Scale { get; private set; }

        /// <summary>
        /// Cale le gabarit sur l'écran réel. La marge suit la taille de l'écran, pour que rien ne colle au bord
        /// ni aux coins arrondis ; et si le gabarit complet ne tient pas dans la zone sûre (écran étroit, format
        /// inhabituel), tout est réduit d'un même facteur. Sans cela, les piles de boutons finissent par se
        /// chevaucher, recouvrir la jauge et déborder de l'écran.
        /// </summary>
        public static ControlLayout Measure()
        {
            Vector2 safe = UsableSafeAreaSize();
            float margin = Mathf.Max(MinMargin, Mathf.Min(safe.x, safe.y) * MarginShare);

            float steerWidth = Mathf.Max(SteerButtonSize * 2f + Gap, JoystickSize);
            float steerHeight = Mathf.Max(SteerButtonSize, JoystickSize);
            float pedalsWidth = PedalWidth * 2f + Gap;
            float pedalsHeight = PedalHeight * 2f + Gap;

            float needWidth = 2f * margin + steerWidth + pedalsWidth + MinCorridor;
            // Colonne de gauche : cumul, jauge (titre, barre, libellés) puis direction.
            float leftHeight = TotalsHeight + Gap
                + WheelieGauge.SpaceAbove + GaugeMinBar + WheelieGauge.SpaceBelow + Gap + steerHeight;
            // Colonne de droite : VUE et PAUSE, les points en direct, puis les pédales.
            float rightHeight = TopButtonHeight + Gap + LiveScoreHeight + Gap + pedalsHeight;
            float needHeight = 2f * margin + Mathf.Max(leftHeight, rightHeight);

            return new ControlLayout
            {
                SafeSize = safe,
                Margin = margin,
                Scale = Mathf.Clamp(Mathf.Min(safe.x / needWidth, safe.y / needHeight), 0.5f, 1f),
            };
        }

        /// <summary>
        /// Zone sûre utilisable, en unités de canvas : celle du système, moins le retrait des coins arrondis que
        /// <see cref="SafeArea"/> applique sur un écran découpé.
        /// </summary>
        static Vector2 UsableSafeAreaSize()
        {
            Vector2 safe = UIFactory.SafeAreaSize();
            bool cutout = Screen.safeArea.width < Screen.width || Screen.safeArea.height < Screen.height;
            if (cutout) safe -= Vector2.one * (2f * SafeArea.DefaultCornerInset);
            return safe;
        }

        /// <summary>Mesure du gabarit ramenée à l'échelle retenue pour cet écran.</summary>
        public float S(float value) => value * Scale;

        /// <summary>Hauteur de la bande du haut (VUE, PAUSE, vitesse) : aucune commande n'y monte.</summary>
        public float TopBand => Margin + S(TopButtonHeight) + S(Gap);

        /// <summary>Distance du haut de la zone sûre au haut de la jauge d'angle, sous le cumul des prouesses.</summary>
        public float GaugeTop => Margin + TotalsHeight + S(Gap);

        /// <summary>Distance du bas de la zone sûre au bas de la jauge, qui laisse la place à la direction par défaut.</summary>
        public float GaugeBottom(SteeringControl steering) =>
            Margin + S(steering == SteeringControl.Joystick ? JoystickSize : SteerButtonSize) + S(Gap);

        /// <summary>Libellé du bouton, le même en jeu et dans l'éditeur.</summary>
        public static string Label(DrivingControl control)
        {
            switch (control)
            {
                case DrivingControl.SteerLeft: return "«";
                case DrivingControl.SteerRight: return "»";
                case DrivingControl.Joystick: return "DIRECTION";
                case DrivingControl.Throttle: return "GAZ";
                case DrivingControl.Lift: return "LEVER";
                case DrivingControl.FrontBrake: return "FREIN AV";
                default: return "FREIN";
            }
        }

        /// <summary>Corps du libellé, avant mise à l'échelle : les flèches, seules sur leur bouton, sont plus grosses.</summary>
        public static int FontSize(DrivingControl control) =>
            control == DrivingControl.SteerLeft || control == DrivingControl.SteerRight ? UITheme.FontTitle : UITheme.FontHeading;

        public Vector2 Size(DrivingControl control)
        {
            switch (control)
            {
                case DrivingControl.SteerLeft:
                case DrivingControl.SteerRight:
                    return Vector2.one * S(SteerButtonSize);
                case DrivingControl.Joystick:
                    // Toute une moitié d'écran en largeur : c'est la course du doigt qui donne la finesse du braquage.
                    return new Vector2(Mathf.Max(S(JoystickSize), SafeSize.x * 0.5f - Margin - S(Gap)), S(JoystickSize));
                default:
                    return new Vector2(S(PedalWidth), S(PedalHeight));
            }
        }

        /// <summary>
        /// Place d'origine : la direction en bas à gauche ; en bas à droite, les quatre pédales en carré, les freins
        /// dans la colonne de gauche (FREIN AV en haut, FREIN en bas), LEVER et GAZ dans celle de droite, GAZ dans
        /// le coin, là où le pouce se pose. Chaque frein est sur la même rangée que la commande qu'il contre.
        /// </summary>
        public Vector2 DefaultCenter(DrivingControl control)
        {
            float steer = S(SteerButtonSize);
            float width = S(PedalWidth);
            float height = S(PedalHeight);
            float gap = S(Gap);

            float rightColumn = SafeSize.x - Margin - width * 0.5f;
            float leftColumn = rightColumn - width - gap;
            float bottomRow = Margin + height * 0.5f;
            float topRow = bottomRow + height + gap;

            switch (control)
            {
                case DrivingControl.SteerLeft:
                    return new Vector2(Margin + steer * 0.5f, Margin + steer * 0.5f);
                case DrivingControl.SteerRight:
                    return new Vector2(Margin + steer * 1.5f + gap, Margin + steer * 0.5f);
                case DrivingControl.Joystick:
                    return Vector2.one * Margin + Size(control) * 0.5f;
                case DrivingControl.Throttle:
                    return new Vector2(rightColumn, bottomRow);
                case DrivingControl.Lift:
                    return new Vector2(rightColumn, topRow);
                case DrivingControl.FrontBrake:
                    return new Vector2(leftColumn, topRow);
                default:
                    return new Vector2(leftColumn, bottomRow);
            }
        }

        /// <summary>Place effective : celle choisie par le joueur, ramenée dans les bornes, sinon celle d'origine.</summary>
        public Vector2 Center(DrivingControl control)
        {
            if (!SettingsManager.TryGetControlPosition(control, out Vector2 fraction)) return DefaultCenter(control);
            return Clamp(control, Vector2.Scale(fraction, SafeSize));
        }

        /// <summary>Ramène un centre dans les bornes permises : à la marge des bords, sous la bande du haut.</summary>
        public Vector2 Clamp(DrivingControl control, Vector2 center)
        {
            Vector2 half = Size(control) * 0.5f;
            float minX = Margin + half.x;
            float maxX = SafeSize.x - Margin - half.x;
            float minY = Margin + half.y;
            float maxY = SafeSize.y - TopBand - half.y;

            return new Vector2(
                minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : SafeSize.x * 0.5f,
                minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : minY);
        }

        /// <summary>Centre exprimé en fraction de la zone sûre : la forme sous laquelle il est enregistré.</summary>
        public Vector2 ToFraction(Vector2 center) =>
            new Vector2(center.x / Mathf.Max(1f, SafeSize.x), center.y / Mathf.Max(1f, SafeSize.y));

        public Rect RectOf(DrivingControl control, Vector2 center)
        {
            Vector2 size = Size(control);
            return new Rect(center - size * 0.5f, size);
        }

        /// <summary>
        /// Pose un RectTransform, enfant de la zone sûre, sur la commande. L'ancre est ponctuelle et exprimée en
        /// fraction : la taille est connue tout de suite (les coins arrondis en ont besoin) et le bouton suit la
        /// zone sûre si elle bouge.
        /// </summary>
        public void Place(RectTransform rect, DrivingControl control, Vector2 center)
        {
            Vector2 anchor = ToFraction(center);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Size(control);
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
