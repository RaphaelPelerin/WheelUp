using UnityEngine;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Palette partagée par tous les écrans, calquée sur le logo : son rouge, ses noirs neutres,
    /// son blanc cassé. Les gris ont perdu leur teinte bleue — à côté de la tuile du logo, qui est
    /// d'un noir neutre, elle tirait le menu vers le froid.
    ///
    /// Les couleurs qui portent une information plutôt que l'identité restent en dehors : rareté
    /// des coffres, peintures de moto, zones de la jauge de wheeling, or des pièces.
    /// </summary>
    public class UITheme
    {
        /// <summary>
        /// Rouge de la marque, relevé sur le "UP" du logo (#FD1823). Tout ce qui doit porter la
        /// couleur du jeu part d'ici, y compris hors du menu.
        /// </summary>
        public static readonly Color Brand = new Color(0.992f, 0.094f, 0.137f);

        /// <summary>
        /// Noir de la tuile du logo (#0C0C0E). La barre latérale l'adopte : la tuile s'y fond au
        /// lieu de s'y découper.
        /// </summary>
        public static readonly Color BrandBlack = new Color(0.047f, 0.047f, 0.055f);

        /// <summary>
        /// Échelle typographique du jeu. Rien ne descend sous <see cref="FontLabel"/> : un corps
        /// plus petit est illisible sur un téléphone tenu à bout de bras, manette ou guidon en main.
        /// L'information qui ne tient pas en gros ne rapetisse pas — elle passe derrière un bouton
        /// « i » et s'affiche en plein écran (voir <see cref="InfoPopup"/>).
        /// </summary>
        public const int FontDisplay = 46;
        public const int FontTitle = 34;
        public const int FontHeading = 28;
        public const int FontBody = 24;
        public const int FontLabel = 22;

        public readonly Color Background = new Color(0.073f, 0.073f, 0.082f);
        public readonly Color Panel = new Color(0.127f, 0.127f, 0.137f);
        public readonly Color PanelAlt = new Color(0.19f, 0.19f, 0.2f);
        public readonly Color Accent = Brand;
        public readonly Color Text = Color.white;
        public readonly Color TextMuted = new Color(0.74f, 0.74f, 0.75f);

        public readonly Color SidebarBackground = BrandBlack;
        public readonly Color NavTextInactive = new Color(0.58f, 0.58f, 0.59f);
        public readonly Color NavTextLocked = new Color(0.36f, 0.36f, 0.37f);
        public readonly Color Coin = new Color(1f, 0.8f, 0.28f);
    }
}
