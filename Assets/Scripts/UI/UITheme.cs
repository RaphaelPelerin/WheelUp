using UnityEngine;

namespace WheelingMoto.UI
{
    /// <summary>Palette de couleurs partagée par tous les écrans du menu.</summary>
    public class UITheme
    {
        public readonly Color Background = new Color(0.06f, 0.07f, 0.09f);
        public readonly Color Panel = new Color(0.11f, 0.12f, 0.15f);
        public readonly Color PanelAlt = new Color(0.17f, 0.18f, 0.22f);
        public readonly Color Accent = new Color(0.96f, 0.36f, 0.13f);
        public readonly Color Text = Color.white;
        public readonly Color TextMuted = new Color(0.72f, 0.73f, 0.78f);
    }
}
