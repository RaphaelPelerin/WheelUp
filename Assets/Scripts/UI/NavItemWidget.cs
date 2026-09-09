using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>Item de la barre latérale du menu principal (voir UIFactory.AddNavItem).</summary>
    public class NavItemWidget
    {
        public GameObject Root;
        public Button Button;
        public TextMeshProUGUI Label;
        public Image Indicator;
    }
}
