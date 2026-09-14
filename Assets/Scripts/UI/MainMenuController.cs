using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
        const float SidebarWidth = 370f;

        /// <summary>
        /// Respiration ajoutée à gauche de la barre, en plus de la zone sûre : en paysage, la Dynamic
        /// Island borde immédiatement le rectangle sûr et les rubriques semblaient collées à la
        /// découpe. Seul le contenu recule ; la bande colorée court toujours jusqu'au bord physique.
        /// </summary>
        const float SidebarGutter = 48f;

        // Le logo est un dessin large : son cadre l'est aussi, et NavStartY descend d'autant.
        // Le rapport 3:2 est celui du dessin : le changer déformerait le logo au lieu de le réduire.
        const float LogoMarginTop = 24f;
        const float LogoWidth = 210f;
        const float LogoHeight = 140f;

        // Badge de secours dessiné par code : plus large que haut, d'où ses propres mesures.
        const float FallbackLogoWidth = 189f;
        const float FallbackLogoHeight = 84f;

        // Badge de niveau, dans le coin haut droit, à gauche du solde de pièces.
        const float LevelBadgeWidth = 210f;
        // Largeur du badge de pièces (200) plus la gouttière qui les sépare.
        const float LevelBadgeRight = 242f;

        const float NavRowHeight = 76f;
        // Sous le cadre du logo (LogoMarginTop + LogoHeight = 164), plus la même respiration qu'avant.
        const float NavStartY = -210f;

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

            /// <summary>Compte affiché sur la pastille de l'onglet, ou null si l'onglet ne notifie rien.</summary>
            public Func<int> Badge;
        }

        readonly UITheme theme = new UITheme();
        readonly GarageMenu garageMenu = new GarageMenu();
        readonly PlayMenu playMenu = new PlayMenu();
        readonly SettingsMenu settingsMenu = new SettingsMenu();
        readonly ChestsMenu chestsMenu = new ChestsMenu();
        readonly ShopMenu shopMenu = new ShopMenu();
        readonly MissionsMenu missionsMenu = new MissionsMenu();

        NavTab[] tabs;
        NavItemWidget[] navItems;
        TextMeshProUGUI coinsLabel;

        // Masqués pendant l'ouverture d'un coffre : la séquence occupe tout l'écran.
        GameObject sidebarObject;
        GameObject contentObject;
        GameObject coinPillObject;
        GameObject levelBadgeObject;
        GameObject sidebarBleedObject;
        ChestOpeningScreen openingScreen;

        SafeArea safeArea;
        Image sidebarBleed;
        RectTransform logoBadge;
        Image xpFill;
        Image levelClaimDot;
        TextMeshProUGUI xpLabel;
        MotoChoiceScreen motoChoiceScreen;
        Transform canvasRoot;

        void Awake()
        {
            SettingsManager.Apply();
            Build();
            SelectTab(0);

            // Les récompenses de palier ne s'imposent plus à l'arrivée : elles attendent sur la route
            // des paliers, que le joueur ouvre quand il veut. Le point doré du badge de niveau est ce
            // qui l'avertit qu'il a quelque chose à y chercher.
        }

        // Le badge est le seul affichage du solde : il doit suivre chaque achat et chaque gain de coffre.
        void OnEnable()
        {
            EconomyManager.Changed += RefreshCoins;
            MissionManager.Changed += RefreshBadges;
            AchievementManager.Changed += RefreshBadges;
            LevelManager.Changed += RefreshLevel;
        }

        void OnDisable()
        {
            EconomyManager.Changed -= RefreshCoins;
            MissionManager.Changed -= RefreshBadges;
            AchievementManager.Changed -= RefreshBadges;
            LevelManager.Changed -= RefreshLevel;
        }

        void Build()
        {
            BuildTabs();

            var canvas = UIFactory.CreateRootCanvas("MainCanvas");
            var root = canvas.transform;
            canvasRoot = root;

            // Les deux aplats de fond restent hors de la zone sûre : sur iPhone, la couleur doit
            // courir sous la Dynamic Island et jusque dans les coins arrondis, sinon le menu laisse
            // apparaître un liseré le long de la découpe.
            UIFactory.AddPanel(root, "Background", theme.Background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            sidebarBleed = UIFactory.AddPanel(root, "SidebarBleed", theme.SidebarBackground,
                new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(SidebarWidth, 0));
            sidebarBleed.raycastTarget = false;
            sidebarBleedObject = sidebarBleed.gameObject;

            // Tout ce qui se lit ou se touche vit à l'intérieur de la zone sûre.
            safeArea = UIFactory.AddSafeArea(root, "SafeArea");
            safeArea.Changed += ApplySafeAreaBleed;
            var safeRoot = safeArea.transform;

            // La barre latérale n'est plus qu'un cadre de placement : sa couleur est portée par la
            // bande pleine largeur derrière elle, qui la prolonge jusqu'au bord physique de l'écran.
            var sidebar = UIFactory.AddPanel(safeRoot, "Sidebar", Color.clear,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(SidebarGutter, 0), new Vector2(SidebarGutter + SidebarWidth, 0));
            sidebarObject = sidebar.gameObject;

            BuildLogo(sidebar.transform);
            BuildNavItems(sidebar.transform);
            BuildCoinPill(safeRoot);
            BuildLevelBadge(safeRoot);

            // Marge haute réservée au badge de pièces, marge basse pour ne pas coller au bord.
            var content = UIFactory.AddPanel(safeRoot, "Content", Color.clear,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(SidebarGutter + SidebarWidth, 40), new Vector2(0, -110));
            contentObject = content.gameObject;
            var contentArea = content.rectTransform;

            garageMenu.Build(contentArea, theme);
            playMenu.Build(contentArea, theme);
            settingsMenu.Build(contentArea, theme);
            chestsMenu.Build(contentArea, theme);
            shopMenu.Build(contentArea, theme);
            missionsMenu.Build(contentArea, theme);

            // Construit en dernier : l'écran d'ouverture doit recouvrir tout le reste. Il est posé sur
            // le canvas et non dans la zone sûre : son fond opaque doit masquer le menu jusqu'aux bords.
            openingScreen = ChestOpeningScreen.Create(root, theme);
            motoChoiceScreen = MotoChoiceScreen.Create(root, theme);
            chestsMenu.OpeningRequested = ShowChestOpening;

            // Les deux écrans passent par la même séquence : un coffre offert par une mission doit
            // s'ouvrir exactement comme un coffre acheté.
            missionsMenu.OpeningRequested = ShowChestOpening;
            playMenu.GarageRequested = () => SelectTabByLabel("CUSTOMISER");

            ApplySafeAreaBleed();
            RefreshLevel();
            RefreshBadges();
        }

        /// <summary>
        /// Étire la bande de la barre latérale du bord physique de l'écran jusqu'au bord droit de la
        /// barre, qui vit, lui, dans la zone sûre. Sans ce rattrapage, la marge laissée à la Dynamic
        /// Island afficherait la couleur du fond général et la barre semblerait flotter ; recalculé à
        /// chaque bascule paysage gauche / paysage droit, puisque la découpe change de côté.
        /// </summary>
        void ApplySafeAreaBleed()
        {
            if (sidebarBleed == null || safeArea == null) return;

            float insetLeft = safeArea.Insets.x;
            sidebarBleed.rectTransform.offsetMin = Vector2.zero;
            sidebarBleed.rectTransform.offsetMax = new Vector2(insetLeft + SidebarGutter + SidebarWidth, 0f);

            // Le logo se centre sur la bande visible et non sur la barre seule : la bande déborde à
            // gauche de tout ce que l'encoche et la gouttière ont fait reculer, et un logo centré sur la
            // barre paraissait poussé vers la droite d'autant.
            if (logoBadge != null)
            {
                logoBadge.anchoredPosition = new Vector2(-(insetLeft + SidebarGutter) / 2f, logoBadge.anchoredPosition.y);
            }
        }

        void OnDestroy()
        {
            if (safeArea != null) safeArea.Changed -= ApplySafeAreaBleed;
        }

        /// <summary>Bascule sur l'écran d'ouverture, puis rend la main au menu une fois refermé.</summary>
        void ShowChestOpening(ChestInfo chest, List<ChestReward> rewards)
        {
            SetMenuVisible(false);

            openingScreen.Play(chest, rewards, () =>
            {
                SetMenuVisible(true);
                chestsMenu.RefreshOnShow();

                // Le coffre vient peut-être de valider une mission « ouvre un coffre » et de faire
                // avancer le succès qui les compte : l'onglet rafraîchit ses trois pages d'un coup.
                missionsMenu.RefreshOnShow();
                RefreshCoins();
                RefreshBadges();

                // Le palier vient d'être encaissé : le point doré du badge doit s'éteindre, ou rester
                // allumé s'il en reste d'autres derrière.
                RefreshLevel();
            });
        }

        void SetMenuVisible(bool visible)
        {
            if (sidebarObject != null) sidebarObject.SetActive(visible);
            if (sidebarBleedObject != null) sidebarBleedObject.SetActive(visible);
            if (contentObject != null) contentObject.SetActive(visible);
            if (coinPillObject != null) coinPillObject.SetActive(visible);
            if (levelBadgeObject != null) levelBadgeObject.SetActive(visible);
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

                // Juste après Jouer : les missions se consultent avant de rouler, pour savoir quoi
                // viser, et se relisent au retour. Les enterrer plus bas les rendrait invisibles.
                // L'onglet porte aussi les succès, sur une de ses trois pages : toute la progression
                // tient ainsi derrière une seule rubrique, et la barre latérale reste courte.
                new NavTab
                {
                    Label = "MISSIONS",
                    Panel = () => missionsMenu.Root,
                    Refresh = missionsMenu.RefreshOnShow,
                    // Trois sources de notification : les missions bouclées à encaisser, les coffres
                    // de complétion et ceux des succès. Une seule pastille les additionne, parce que
                    // le joueur n'a besoin de savoir qu'une chose : il a quelque chose à aller prendre.
                    Badge = () => MissionManager.PendingClaims()
                        + MissionManager.PendingRewards()
                        + AchievementManager.PendingRewards(),
                },

                new NavTab { Label = "EN LIGNE", Locked = true },
                new NavTab { Label = "CUSTOMISER", Panel = () => garageMenu.Root, Refresh = garageMenu.RefreshOnShow },
                new NavTab { Label = "COFFRES", Panel = () => chestsMenu.Root, Refresh = chestsMenu.RefreshOnShow },
                new NavTab { Label = "BOUTIQUE", Panel = () => shopMenu.Root, Refresh = shopMenu.RefreshOnShow },
                new NavTab { Label = "PARAMÈTRES", Panel = () => settingsMenu.Root },
            };
        }

        /// <summary>
        /// En-tête de la barre : la tuile de marque (moto + nom) livrée par la direction
        /// artistique. Si l'image n'est pas là — dépôt cloné sans les binaires, import Unity pas
        /// encore passé — on retombe sur le badge dessiné par code plutôt que de laisser un trou.
        /// </summary>
        void BuildLogo(Transform sidebar)
        {
            var logo = UIFactory.AddBrandLogo(sidebar, "LogoBadge", new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-LogoWidth / 2f, -LogoMarginTop - LogoHeight),
                new Vector2(LogoWidth / 2f, -LogoMarginTop));

            if (logo != null)
            {
                logoBadge = logo.rectTransform;
                return;
            }

            Debug.LogWarning("Logo absent de Resources/" + UIFactory.LogoResourcePath + " : badge de secours affiché.");
            var container = UIFactory.CreateUIObject("LogoBadge", sidebar);
            logoBadge = container;
            BuildFallbackLogo(container);
        }


        /// <summary>Badge de secours dessiné par code : deux pavés "WHEEL" / "UP" légèrement inclinés.</summary>
        void BuildFallbackLogo(RectTransform container)
        {
            UIFactory.SetRect(container, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-FallbackLogoWidth / 2f, -LogoMarginTop - FallbackLogoHeight),
                new Vector2(FallbackLogoWidth / 2f, -LogoMarginTop));
            container.localRotation = Quaternion.Euler(0, 0, -2.5f);

            var top = UIFactory.AddPanel(container, "LogoTop", theme.Accent,
                new Vector2(0, 0.52f), new Vector2(1, 1), Vector2.zero, Vector2.zero, rounded: true);
            top.raycastTarget = false;
            UIFactory.AddText(top.transform, "Label", "WHEEL", 26, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);

            var bottom = UIFactory.AddPanel(container, "LogoBottom", new Color(0.09f, 0.09f, 0.1f),
                new Vector2(0, 0), new Vector2(1, 0.48f), Vector2.zero, Vector2.zero, rounded: true);
            bottom.raycastTarget = false;
            UIFactory.AddText(bottom.transform, "Label", "UP", 26, theme.Accent, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);
        }

        /// <summary>
        /// Badge de niveau, posé à gauche du solde de pièces : un disque portant le chiffre, la
        /// mention « NIVEAU » et une barre d'avancement. Tout le badge est le bouton qui ouvre la
        /// route des paliers — viser au pouce une barre de douze unités de haut serait un supplice.
        ///
        /// Il siégeait dans la barre latérale, entre le logo et les rubriques. Le coin haut droit est
        /// sa place : c'est déjà là que se lit ce que le joueur possède, et le niveau appartient à
        /// cette famille-là bien plus qu'à la navigation, dont il occupait une ligne sans jamais y
        /// mener nulle part.
        /// </summary>
        void BuildLevelBadge(Transform root)
        {
            var button = UIFactory.AddButton(root, "LevelBadge", string.Empty, theme.PanelAlt, theme.Text,
                UITheme.FontLabel, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-LevelBadgeRight - LevelBadgeWidth, -100), new Vector2(-LevelBadgeRight, -30),
                () => LevelPopup.Show(canvasRoot, theme, ClaimLevel));
            levelBadgeObject = button.gameObject;

            Transform badge = button.transform;

            var disc = UIFactory.AddPanel(badge, "LevelDisc", theme.Accent,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(11, -26), new Vector2(63, 26),
                rounded: true);
            disc.raycastTarget = false;

            xpLabel = UIFactory.AddText(disc.transform, "LevelValue", "", UITheme.FontBody, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                FontStyles.Bold | FontStyles.Italic);

            UIFactory.AddText(badge, "LevelCaption", "NIVEAU", UITheme.FontLabel, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(74, -40), new Vector2(-14, -10), FontStyles.Bold | FontStyles.Italic);

            var track = UIFactory.AddPanel(badge, "XpTrack", theme.Background,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(74, 18), new Vector2(-16, 30),
                rounded: true);
            track.raycastTarget = false;

            xpFill = UIFactory.AddPanel(track.transform, "XpFill", theme.Accent,
                Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, rounded: true);
            xpFill.raycastTarget = false;

            // Point doré mordant sur le disque : depuis que les récompenses ne s'imposent plus à
            // l'arrivée, c'est la seule chose qui dise au joueur qu'un palier l'attend sur la route.
            // Sans lui, il pourrait monter cinq niveaux sans jamais penser à ouvrir la page.
            levelClaimDot = UIFactory.AddPanel(badge, "ClaimDot", theme.Coin,
                new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(48, 8), new Vector2(72, 32),
                rounded: true);
            levelClaimDot.raycastTarget = false;
        }

        void RefreshLevel()
        {
            if (xpLabel != null) xpLabel.text = LevelManager.Level.ToString();
            if (levelClaimDot != null) levelClaimDot.gameObject.SetActive(LevelManager.HasPendingRewards);
            if (xpFill != null) xpFill.rectTransform.anchorMax = new Vector2(LevelManager.Ratio, 1f);
        }

        /// <summary>
        /// Honore ce que les niveaux doivent, une récompense à la fois : à l'entrée dans le menu, puis
        /// après chaque écran refermé.
        ///
        /// Monter de niveau arrive en conduisant, et on ne coupe pas une partie pour présenter un
        /// choix de moto : la récompense attend ici. Rien n'est consommé avant que son écran ne soit
        /// allé au bout, donc fermer l'application au milieu la rend au prochain démarrage.
        /// </summary>
        /// <summary>
        /// Remet la récompense d'un palier, puis l'enregistre comme encaissée. Appelé par la route
        /// des paliers : elle sait quel palier le joueur réclame, mais pas comment présenter une moto
        /// ou un coffre — ces deux écrans vivent ici.
        ///
        /// Le palier n'est marqué qu'une fois la récompense réellement remise : pour une moto, à la
        /// fermeture de l'écran de choix, donc après que le joueur a choisi. Fermer l'application
        /// devant l'écran de choix lui rend son palier intact.
        /// </summary>
        void ClaimLevel(int level)
        {
            // Vérifié avant de remettre quoi que ce soit : sans ce garde, un appel répété sur un
            // palier déjà encaissé re-tirerait un coffre à chaque fois, et Claim refuserait juste
            // d'avancer le compteur.
            if (level != LevelManager.NextClaimable) return;

            var reward = LevelRewardCatalog.RewardFor(level);
            if (reward == null) return;

            if (reward.IsMotoChoice)
            {
                if (motoChoiceScreen == null)
                {
                    Debug.LogWarning($"[MainMenu] Palier {level} : pas d'écran de choix de moto, palier laissé dû.");
                    return;
                }

                SetMenuVisible(false);
                motoChoiceScreen.Play(reward.MotoTier, () =>
                {
                    SetMenuVisible(true);
                    LevelManager.Claim(level);
                    garageMenu.RefreshOnShow();
                    RefreshLevel();
                });
                return;
            }

            var chest = ChestCatalog.Find(reward.ChestId);
            var rewards = chest != null ? ChestManager.Grant(chest) : null;

            // Les lots sont déjà crédités par Grant : l'écran d'ouverture ne fait que les montrer. Le
            // palier est donc encaissé dès maintenant — y compris si le coffre est inconnu ou vide,
            // sinon il bloquerait indéfiniment tous ceux qui le suivent, qu'on ne récupère que dans
            // l'ordre.
            LevelManager.Claim(level);
            RefreshLevel();

            if (rewards == null || rewards.Count == 0) return;

            ShowChestOpening(chest, rewards);
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

            // Le jeton frappé au logo remplace le disque uni. Repli sur le disque si la ressource
            // manque : le solde doit rester lisible, une icône absente ne doit pas le décaler.
            if (UIFactory.AddCoinIcon(pill.transform, "CoinDot",
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, -16), new Vector2(46, 16)) == null)
            {
                var dot = UIFactory.AddPanel(pill.transform, "CoinDot", theme.Coin,
                    new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -14), new Vector2(44, 14), rounded: true);
                dot.raycastTarget = false;
            }

            coinsLabel = UIFactory.AddText(pill.transform, "CoinsValue", "0", 22, theme.Text, TextAnchor.MiddleLeft,
                Vector2.zero, Vector2.one, new Vector2(58, 0), new Vector2(-16, 0), FontStyles.Bold);
        }

        /// <summary>
        /// Bascule sur l'onglet portant ce libellé. Utilisé par « Changer de moto », qui doit
        /// atteindre le garage sans connaître sa place : insérer une rubrique dans la barre ne doit
        /// pas casser silencieusement un raccourci d'un autre écran.
        /// </summary>
        void SelectTabByLabel(string label)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i].Label != label) continue;

                SelectTab(i);
                return;
            }

            Debug.LogWarning($"Onglet « {label} » introuvable : le raccourci ne mène nulle part.");
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
            RefreshBadges();
        }

        /// <summary>
        /// Met à jour les pastilles de la barre latérale. Recalculé à chaque bascule d'onglet et à
        /// chaque changement du tableau de missions : un coffre gagné pendant la partie doit se voir
        /// dès le retour au menu, sans qu'il faille passer par l'onglet pour s'en apercevoir.
        /// </summary>
        void RefreshBadges()
        {
            if (navItems == null) return;

            for (int i = 0; i < tabs.Length; i++)
            {
                if (navItems[i] == null) continue;

                navItems[i].SetBadge(tabs[i].Badge?.Invoke() ?? 0);
            }
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
