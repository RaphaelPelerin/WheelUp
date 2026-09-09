using TMPro;
using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Construit et pilote le menu principal : barre latérale de navigation (Jouer / En ligne / Customiser /
    /// Paramètres / Boutique / Spin chanceux) et zone de contenu qui bascule entre les panneaux disponibles.
    /// Tout est généré par code au démarrage de la scène MainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        const float SidebarWidth = 460f;
        const float NavRowHeight = 98f;
        const float NavStartY = -220f;

        static readonly string[] NavLabels = { "JOUER", "EN LIGNE", "CUSTOMISER", "PARAMÈTRES", "BOUTIQUE", "SPIN CHANCEUX" };
        static readonly bool[] NavLocked = { false, true, false, false, true, true };

        readonly UITheme theme = new UITheme();
        readonly GarageMenu garageMenu = new GarageMenu();
        readonly PlayMenu playMenu = new PlayMenu();
        readonly SettingsMenu settingsMenu = new SettingsMenu();

        NavItemWidget[] navItems;
        TextMeshProUGUI coinsLabel;

        void Awake()
        {
            SettingsManager.Apply();
            Build();
            SelectTab(0);
        }

        void Build()
        {
            var canvas = UIFactory.CreateRootCanvas("MainCanvas");
            var root = canvas.transform;

            UIFactory.AddPanel(root, "Background", theme.Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var sidebar = UIFactory.AddPanel(root, "Sidebar", theme.SidebarBackground,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(SidebarWidth, 0));

            BuildLogo(sidebar.transform);
            BuildNavItems(sidebar.transform);
            BuildCoinPill(root);

            // Marge haute réservée au badge de pièces, marge basse pour ne pas coller au bord.
            var content = UIFactory.AddPanel(root, "Content", Color.clear,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(SidebarWidth, 40), new Vector2(0, -110));
            var contentArea = content.rectTransform;

            BuildBikeStagePlaceholder(contentArea);

            garageMenu.Build(contentArea, theme);
            playMenu.Build(contentArea, theme);
            settingsMenu.Build(contentArea, theme);
        }

        void BuildLogo(Transform sidebar)
        {
            var container = UIFactory.CreateUIObject("LogoBadge", sidebar);
            UIFactory.SetRect(container, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -150), new Vector2(300, -30));
            container.localRotation = Quaternion.Euler(0, 0, -2.5f);

            var top = UIFactory.AddPanel(container, "LogoTop", theme.Accent,
                new Vector2(0, 0.52f), new Vector2(1, 1), Vector2.zero, Vector2.zero, rounded: true);
            top.raycastTarget = false;
            UIFactory.AddText(top.transform, "Label", "WHEEL", 26, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);

            var bottom = UIFactory.AddPanel(container, "LogoBottom", new Color(0.08f, 0.09f, 0.11f),
                new Vector2(0, 0), new Vector2(1, 0.48f), Vector2.zero, Vector2.zero, rounded: true);
            bottom.raycastTarget = false;
            UIFactory.AddText(bottom.transform, "Label", "UP", 26, theme.Accent, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);
        }

        void BuildNavItems(Transform sidebar)
        {
            navItems = new NavItemWidget[NavLabels.Length];

            for (int i = 0; i < NavLabels.Length; i++)
            {
                int capturedIndex = i;
                float yTop = NavStartY - i * NavRowHeight;
                navItems[i] = UIFactory.AddNavItem(sidebar, "Nav_" + i, NavLabels[i], theme,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, yTop - NavRowHeight), new Vector2(0, yTop),
                    () => SelectTab(capturedIndex), NavLocked[i]);
            }
        }

        void BuildCoinPill(Transform root)
        {
            var pill = UIFactory.AddPanel(root, "CoinPill", theme.PanelAlt,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -100), new Vector2(-30, -30), rounded: true);
            pill.raycastTarget = false;

            var dot = UIFactory.AddPanel(pill.transform, "CoinDot", theme.Coin,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -14), new Vector2(44, 14), rounded: true);
            dot.raycastTarget = false;

            coinsLabel = UIFactory.AddText(pill.transform, "CoinsValue", "0", 22, theme.Text, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(58, 0), new Vector2(-16, 0), FontStyles.Bold);
        }

        void BuildBikeStagePlaceholder(RectTransform contentArea)
        {
            var stage = UIFactory.AddPanel(contentArea, "BikeStage",
                new Color(theme.Panel.r, theme.Panel.g, theme.Panel.b, 0.35f),
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(30, 30), new Vector2(-30, -30), rounded: true);
            stage.raycastTarget = false;

            var ground = UIFactory.AddPanel(stage.transform, "Ground", new Color(0f, 0f, 0f, 0.3f),
                new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f), new Vector2(-260, -26), new Vector2(260, 26), rounded: true);
            ground.raycastTarget = false;

            UIFactory.AddText(stage.transform, "PlaceholderTitle", "TA MOTO ARRIVE ICI", 26, theme.TextMuted,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f),
                new Vector2(-320, 40), new Vector2(320, 90), FontStyles.Bold | FontStyles.Italic);

            UIFactory.AddText(stage.transform, "PlaceholderHint", "Choisis ta machine dans Customiser", 16, theme.NavTextInactive,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f),
                new Vector2(-320, 8), new Vector2(320, 38));
        }

        void SelectTab(int index)
        {
            if (NavLocked[index]) return;

            garageMenu.Root.SetActive(index == 2);
            playMenu.Root.SetActive(index == 0);
            settingsMenu.Root.SetActive(index == 3);

            if (index == 0) playMenu.RefreshOnShow();
            else if (index == 2) garageMenu.RefreshOnShow();

            for (int i = 0; i < navItems.Length; i++)
            {
                SetNavVisual(i, i == index);
            }

            RefreshCoins();
        }

        void SetNavVisual(int index, bool selected)
        {
            var item = navItems[index];
            item.Indicator.gameObject.SetActive(selected && !NavLocked[index]);
            if (NavLocked[index]) return;

            item.Label.color = selected ? theme.Text : theme.NavTextInactive;
        }

        void RefreshCoins()
        {
            if (coinsLabel != null) coinsLabel.text = EconomyManager.Coins.ToString();
        }
    }
}
