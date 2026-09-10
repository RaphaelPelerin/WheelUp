using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Construit et pilote le menu principal : barre latérale de navigation (Jouer / En ligne /
    /// Customiser / Coffres / Boutique / Paramètres) et zone de contenu qui bascule entre les
    /// panneaux disponibles. Tout est généré par code au démarrage de la scène MainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        const float SidebarWidth = 460f;
        const float NavRowHeight = 98f;
        const float NavStartY = -220f;

        /// <summary>
        /// Un onglet de la barre latérale. Le panneau et le rafraîchissement sont portés par l'entrée
        /// elle-même : la bascule d'onglet n'a plus à connaître l'ordre des rubriques, qui changeait
        /// le sens de tous les tests dès qu'on en insérait une.
        /// </summary>
        class NavTab
        {
            public string Label;
            public bool Locked;
            public Func<GameObject> Panel;
            public Action Refresh;
        }

        readonly UITheme theme = new UITheme();
        readonly GarageMenu garageMenu = new GarageMenu();
        readonly PlayMenu playMenu = new PlayMenu();
        readonly SettingsMenu settingsMenu = new SettingsMenu();
        readonly ChestsMenu chestsMenu = new ChestsMenu();
        readonly ShopMenu shopMenu = new ShopMenu();

        NavTab[] tabs;
        NavItemWidget[] navItems;
        TextMeshProUGUI coinsLabel;

        // Masqués pendant l'ouverture d'un coffre : la séquence occupe tout l'écran.
        GameObject sidebarObject;
        GameObject contentObject;
        GameObject coinPillObject;
        ChestOpeningScreen openingScreen;

        void Awake()
        {
            SettingsManager.Apply();
            Build();
            SelectTab(0);
        }

        // Le badge est le seul affichage du solde : il doit suivre chaque achat et chaque gain de coffre.
        void OnEnable() => EconomyManager.Changed += RefreshCoins;

        void OnDisable() => EconomyManager.Changed -= RefreshCoins;

        void Build()
        {
            BuildTabs();

            var canvas = UIFactory.CreateRootCanvas("MainCanvas");
            var root = canvas.transform;

            UIFactory.AddPanel(root, "Background", theme.Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var sidebar = UIFactory.AddPanel(root, "Sidebar", theme.SidebarBackground,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(SidebarWidth, 0));
            sidebarObject = sidebar.gameObject;

            BuildLogo(sidebar.transform);
            BuildNavItems(sidebar.transform);
            BuildCoinPill(root);

            // Marge haute réservée au badge de pièces, marge basse pour ne pas coller au bord.
            var content = UIFactory.AddPanel(root, "Content", Color.clear,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(SidebarWidth, 40), new Vector2(0, -110));
            contentObject = content.gameObject;
            var contentArea = content.rectTransform;

            BuildBikeStagePlaceholder(contentArea);

            garageMenu.Build(contentArea, theme);
            playMenu.Build(contentArea, theme);
            settingsMenu.Build(contentArea, theme);
            chestsMenu.Build(contentArea, theme);
            shopMenu.Build(contentArea, theme);

            // Construit en dernier : l'écran d'ouverture doit recouvrir tout le reste.
            openingScreen = ChestOpeningScreen.Create(root, theme);
            chestsMenu.OpeningRequested = ShowChestOpening;
        }

        /// <summary>Bascule sur l'écran d'ouverture, puis rend la main au menu une fois refermé.</summary>
        void ShowChestOpening(ChestInfo chest, List<ChestReward> rewards)
        {
            SetMenuVisible(false);

            openingScreen.Play(chest, rewards, () =>
            {
                SetMenuVisible(true);
                chestsMenu.RefreshOnShow();
                RefreshCoins();
            });
        }

        void SetMenuVisible(bool visible)
        {
            if (sidebarObject != null) sidebarObject.SetActive(visible);
            if (contentObject != null) contentObject.SetActive(visible);
            if (coinPillObject != null) coinPillObject.SetActive(visible);
        }

        /// <summary>
        /// Ordre de la barre latérale. Les rubriques verrouillées n'ont pas de panneau : elles
        /// occupent leur place pour annoncer ce qui arrive, sans être sélectionnables.
        /// </summary>
        void BuildTabs()
        {
            tabs = new[]
            {
                new NavTab { Label = "JOUER", Panel = () => playMenu.Root, Refresh = playMenu.RefreshOnShow },
                new NavTab { Label = "EN LIGNE", Locked = true },
                new NavTab { Label = "CUSTOMISER", Panel = () => garageMenu.Root, Refresh = garageMenu.RefreshOnShow },
                new NavTab { Label = "COFFRES", Panel = () => chestsMenu.Root, Refresh = chestsMenu.RefreshOnShow },
                new NavTab { Label = "BOUTIQUE", Panel = () => shopMenu.Root, Refresh = shopMenu.RefreshOnShow },
                new NavTab { Label = "PARAMÈTRES", Panel = () => settingsMenu.Root },
            };
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
            navItems = new NavItemWidget[tabs.Length];

            for (int i = 0; i < tabs.Length; i++)
            {
                int capturedIndex = i;
                float yTop = NavStartY - i * NavRowHeight;
                navItems[i] = UIFactory.AddNavItem(sidebar, "Nav_" + i, tabs[i].Label, theme,
                    new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, yTop - NavRowHeight), new Vector2(0, yTop),
                    () => SelectTab(capturedIndex), tabs[i].Locked);
            }
        }

        void BuildCoinPill(Transform root)
        {
            var pill = UIFactory.AddPanel(root, "CoinPill", theme.PanelAlt,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230, -100), new Vector2(-30, -30), rounded: true);
            pill.raycastTarget = false;
            coinPillObject = pill.gameObject;

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
        }

        void SelectTab(int index)
        {
            if (tabs[index].Locked) return;

            for (int i = 0; i < tabs.Length; i++)
            {
                var panel = tabs[i].Panel?.Invoke();
                if (panel != null) panel.SetActive(i == index);

                SetNavVisual(i, i == index);
            }

            tabs[index].Refresh?.Invoke();
            RefreshCoins();
        }

        void SetNavVisual(int index, bool selected)
        {
            var item = navItems[index];
            item.Indicator.gameObject.SetActive(selected && !tabs[index].Locked);
            if (tabs[index].Locked) return;

            item.Label.color = selected ? theme.Text : theme.NavTextInactive;
        }

        void RefreshCoins()
        {
            if (coinsLabel != null) coinsLabel.text = EconomyManager.Coins.ToString();
        }
    }
}
