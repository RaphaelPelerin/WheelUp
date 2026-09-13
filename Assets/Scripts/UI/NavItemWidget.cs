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

        /// <summary>
        /// Pastille de notification posée à gauche du libellé, masquée par défaut. Seuls les onglets
        /// qui ont quelque chose à réclamer l'allument (voir <see cref="SetBadge"/>).
        /// </summary>
        public GameObject Badge;
        public TextMeshProUGUI BadgeLabel;

        /// <summary>Affiche la pastille avec un compte, ou l'éteint si le compte est nul.</summary>
        public void SetBadge(int count)
        {
            if (Badge == null) return;

            Badge.SetActive(count > 0);
            if (count > 0 && BadgeLabel != null) BadgeLabel.text = count.ToString();
        }
    }
}
