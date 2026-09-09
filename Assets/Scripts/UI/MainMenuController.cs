using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Construit et pilote le menu principal : bandeau d'onglets tactile (Garage / Jouer / Paramètres)
    /// et bascule entre les trois panneaux. Tout est généré par code au démarrage de la scène MainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        readonly UITheme theme = new UITheme();
        readonly GarageMenu garageMenu = new GarageMenu();
        readonly PlayMenu playMenu = new PlayMenu();
        readonly SettingsMenu settingsMenu = new SettingsMenu();

        Button tabGarage;
        Button tabPlay;
        Button tabSettings;

        void Awake()
        {
            SettingsManager.Apply();
            Build();
            SelectTab(1);
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("MainCanvas");
            var root = canvas.transform;

            UIFactory.AddPanel(root, "Background", theme.Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Bandeau d'onglets tactile en bas de l'écran (ergonomie mobile, portée du pouce).
            var tabBar = UIFactory.AddPanel(root, "TabBar", theme.Panel,
                new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 170));

            UIFactory.AddText(root, "GameTitle", "WHEELING MOTO", 34, theme.Accent, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -110), new Vector2(-40, -20));

            float tabWidth = 1f / 3f;
            tabGarage = UIFactory.AddButton(tabBar.transform, "TabGarage", "GARAGE", theme.PanelAlt, theme.Text, 22,
                new Vector2(0f, 0f), new Vector2(tabWidth, 1f), new Vector2(6, 6), new Vector2(-6, -6), () => SelectTab(0));
            tabPlay = UIFactory.AddButton(tabBar.transform, "TabPlay", "JOUER", theme.PanelAlt, theme.Text, 22,
                new Vector2(tabWidth, 0f), new Vector2(tabWidth * 2f, 1f), new Vector2(6, 6), new Vector2(-6, -6), () => SelectTab(1));
            tabSettings = UIFactory.AddButton(tabBar.transform, "TabSettings", "PARAMÈTRES", theme.PanelAlt, theme.Text, 22,
                new Vector2(tabWidth * 2f, 0f), new Vector2(1f, 1f), new Vector2(6, 6), new Vector2(-6, -6), () => SelectTab(2));

            // Zone de contenu entre le titre et le bandeau d'onglets.
            var content = UIFactory.AddPanel(root, "Content", Color.clear,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 170), new Vector2(0, -140));
            var contentArea = content.rectTransform;

            garageMenu.Build(contentArea, theme);
            playMenu.Build(contentArea, theme);
            settingsMenu.Build(contentArea, theme);
        }

        void SelectTab(int index)
        {
            garageMenu.Root.SetActive(index == 0);
            playMenu.Root.SetActive(index == 1);
            settingsMenu.Root.SetActive(index == 2);

            if (index == 1)
            {
                playMenu.RefreshOnShow();
            }
            else if (index == 0)
            {
                garageMenu.RefreshOnShow();
            }

            SetTabVisual(tabGarage, index == 0);
            SetTabVisual(tabPlay, index == 1);
            SetTabVisual(tabSettings, index == 2);
        }

        void SetTabVisual(Button b, bool selected)
        {
            UIFactory.SetButtonColor(b, selected ? theme.Accent : theme.PanelAlt);
        }
    }
}
