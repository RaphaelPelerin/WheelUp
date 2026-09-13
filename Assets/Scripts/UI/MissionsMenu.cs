using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Onglet Missions, en trois pages : les missions du jour, celles de la semaine, et les succès.
    /// Toute la progression du jeu tient derrière cette seule rubrique de la barre latérale.
    ///
    /// Les trois pages partagent exactement la même mise en page, fournie par
    /// <see cref="ProgressBoard"/> : un bandeau de récompense en tête, puis une liste défilante.
    /// Elles ne peuvent donc plus diverger, et une page de trois missions se lit comme une page de
    /// onze succès.
    ///
    /// Boucler une mission et encaisser ses pièces sont deux gestes séparés : le bandeau de jeu
    /// annonce l'objectif atteint, et le joueur vient chercher son dû ici.
    /// </summary>
    public class MissionsMenu
    {
        public GameObject Root { get; private set; }

        /// <summary>Levé quand un coffre vient d'être attribué : au menu de jouer la séquence.</summary>
        public Action<ChestInfo, List<ChestReward>> OpeningRequested;

        const float SideMargin = 30f;
        const float TabBarHeight = 72f;
        const float TabGap = 12f;

        /// <summary>Haut de la zone des pages : sous la bannière et sous la barre de sous-onglets.</summary>
        const float PageTop = -(Storefront.HeaderHeight + TabBarHeight + 12f);

        /// <summary>Une page de missions : son bandeau de coffre et ses lignes.</summary>
        class Section
        {
            public MissionScope Scope;
            public ProgressStrip Strip;
            public ProgressRow[] Rows;
        }

        UITheme theme;
        readonly List<Section> sections = new List<Section>();
        readonly AchievementsMenu achievements = new AchievementsMenu();

        Button[] tabButtons;
        GameObject[] pages;
        int currentPage;

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "MissionsPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            Storefront.BuildHeader(Root.transform, theme, "MISSIONS",
                "Boucle-les en roulant, viens encaisser ici.");

            // Zone commune aux trois pages : elles s'y superposent et se relaient à la sélection.
            var pageArea = UIFactory.AddPanel(Root.transform, "Pages", Color.clear,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(SideMargin, Storefront.BottomMargin), new Vector2(-SideMargin, PageTop));
            pageArea.raycastTarget = false;

            var dailyPage = BuildMissionPage(pageArea.transform, MissionScope.Daily);
            var weeklyPage = BuildMissionPage(pageArea.transform, MissionScope.Weekly);

            var achievementPage = UIFactory.AddPanel(pageArea.transform, "PageAchievements", Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            achievementPage.raycastTarget = false;
            achievements.Build(achievementPage.transform, theme);
            achievements.OpeningRequested = (chest, rewards) => OpeningRequested?.Invoke(chest, rewards);

            pages = new[] { dailyPage, weeklyPage, achievementPage.gameObject };

            BuildTabBar(Root.transform);
            SelectPage(currentPage);
        }

        /// <summary>
        /// Barre de sous-onglets, posée entre la bannière et les pages. Trois pastilles pleine
        /// largeur : à cette taille, elles restent atteignables au pouce sans viser.
        /// </summary>
        void BuildTabBar(Transform root)
        {
            string[] labels = { "DU JOUR", "DE LA SEMAINE", "SUCCÈS" };
            tabButtons = new Button[labels.Length];

            float top = -Storefront.HeaderHeight;

            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                float left = i / (float)labels.Length;
                float right = (i + 1) / (float)labels.Length;

                // La marge latérale ne vaut que pour les bords extérieurs de la barre ; entre deux
                // pastilles, c'est la demi-gouttière qui s'applique, sinon celle du milieu serait
                // amputée des deux côtés à la fois.
                float insetLeft = i == 0 ? SideMargin : TabGap / 2f;
                float insetRight = i == labels.Length - 1 ? SideMargin : TabGap / 2f;

                tabButtons[i] = UIFactory.AddButton(root, "Tab_" + i, labels[i], theme.PanelAlt, theme.Text,
                    UITheme.FontLabel,
                    new Vector2(left, 1f), new Vector2(right, 1f),
                    new Vector2(insetLeft, top - TabBarHeight), new Vector2(-insetRight, top),
                    () => SelectPage(index));
            }
        }

        void SelectPage(int index)
        {
            currentPage = index;

            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].SetActive(i == index);
                UIFactory.SetButtonColor(tabButtons[i], i == index ? theme.Accent : theme.PanelAlt);
            }

            RefreshOnShow();
        }

        // ---------------------------------------------------------------- construction d'une page

        GameObject BuildMissionPage(Transform pageArea, MissionScope scope)
        {
            var page = UIFactory.AddPanel(pageArea, "Page_" + scope, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            page.raycastTarget = false;

            int slots = MissionCatalog.Slots(scope);
            var section = new Section
            {
                Scope = scope,
                Rows = new ProgressRow[slots],
            };

            section.Strip = ProgressBoard.BuildStrip(page.transform, theme, () => OnChestPressed(scope));

            var content = ProgressBoard.BuildScroll(page.transform, "Missions_" + scope, slots);

            for (int i = 0; i < slots; i++)
            {
                int index = i;
                section.Rows[i] = ProgressBoard.BuildRow(content, theme, $"Row_{scope}_{i}", i,
                    () => OnClaimPressed(scope, index));
            }

            sections.Add(section);
            return page.gameObject;
        }

        // ---------------------------------------------------------------- rafraîchissement

        public void RefreshOnShow()
        {
            foreach (var section in sections) RefreshSection(section);
            achievements.RefreshOnShow();
        }

        void RefreshSection(Section section)
        {
            var missions = MissionManager.Missions(section.Scope);

            for (int i = 0; i < section.Rows.Length; i++)
            {
                var row = section.Rows[i];

                if (i >= missions.Count)
                {
                    row.Root.SetActive(false);
                    continue;
                }

                row.Root.SetActive(true);
                Fill(row, missions[i]);
            }

            RefreshStrip(section, missions.Count);
        }

        void Fill(ProgressRow row, ActiveMission mission)
        {
            row.Title.text = mission.Label;
            row.Detail.text = MissionCatalog.MetricLabel(mission.Metric);
            row.Fill.fillAmount = mission.Ratio;

            if (mission.Claimed)
            {
                row.Progress.text = $"Terminée · {mission.Coins} pièces encaissées";
                row.ActionLabel.text = "✓";
                row.ActionLabel.color = theme.NavTextInactive;
            }
            else if (mission.Claimable)
            {
                row.Progress.text = $"Terminée · {mission.Coins} pièces t'attendent";
                row.ActionLabel.text = "RÉCUPÉRER";
                row.ActionLabel.color = theme.Text;
            }
            else
            {
                row.Progress.text = mission.ProgressLabel;
                row.ActionLabel.text = $"+{mission.Coins}";
                row.ActionLabel.color = theme.Coin;
            }

            row.Action.interactable = mission.Claimable;
            UIFactory.SetButtonColor(row.Action, mission.Claimable ? theme.Accent : theme.PanelAlt);

            // Une mission encaissée s'efface au lieu de disparaître : le joueur doit voir que la
            // journée avance, pas se retrouver devant une liste qui rétrécit sans explication. Une
            // mission bouclée mais pas encore encaissée, elle, reste bien visible.
            row.Title.color = mission.Claimed ? theme.TextMuted : theme.Text;
            row.Fill.color = mission.Claimed ? theme.NavTextInactive : theme.Accent;
        }

        void RefreshStrip(Section section, int total)
        {
            var chest = MissionManager.ChestFor(section.Scope);
            int done = MissionManager.CompletedCount(section.Scope);
            bool ready = MissionManager.ChestReady(section.Scope);
            bool taken = MissionManager.ChestTaken(section.Scope);

            var strip = section.Strip;
            string chestName = chest != null ? chest.Name : "Coffre";

            strip.Title.text = taken ? $"{chestName} déjà récupérée" : $"{chestName} offerte";
            strip.Detail.text = $"{done} / {total} missions · {FormatCountdown(section.Scope)}";

            strip.ShowChest(chest);

            strip.Fill.fillAmount = total > 0 ? (float)done / total : 0f;
            strip.Fill.color = ready ? theme.Coin : theme.Accent;

            strip.Action.interactable = ready;
            strip.ActionLabel.text = taken ? "RÉCUPÉRÉE" : ready ? "OUVRIR" : "COFFRE";
            UIFactory.SetButtonColor(strip.Action, ready ? theme.Accent : theme.PanelAlt);
        }

        /// <summary>« renouvelées dans 6 h 12 ». Les secondes sont tues : l'écran n'est pas rafraîchi en continu.</summary>
        static string FormatCountdown(MissionScope scope)
        {
            var left = MissionManager.TimeUntilReset(scope);
            if (left.TotalMinutes < 1d) return "renouvellement imminent";

            if (left.TotalHours < 1d) return $"renouvelées dans {left.Minutes} min";
            if (left.TotalDays < 1d) return $"renouvelées dans {left.Hours} h {left.Minutes:00}";

            return $"renouvelées dans {(int)left.TotalDays} j {left.Hours} h";
        }

        // ---------------------------------------------------------------- actions

        /// <summary>
        /// Encaisse la mission d'une ligne. La mission est retrouvée par sa place et non capturée à
        /// la construction : les lignes survivent au renouvellement du tableau, pas les missions.
        /// </summary>
        void OnClaimPressed(MissionScope scope, int index)
        {
            var missions = MissionManager.Missions(scope);
            if (index >= missions.Count) return;

            if (MissionManager.Claim(missions[index])) RefreshOnShow();
        }

        void OnChestPressed(MissionScope scope)
        {
            var chest = MissionManager.TakeChest(scope);
            if (chest == null) return;

            // Offert : les lots sont attribués sans débit, puis révélés par l'écran d'ouverture.
            var rewards = ChestManager.Grant(chest);
            if (rewards == null || rewards.Count == 0) return;

            RefreshOnShow();
            OpeningRequested?.Invoke(chest, rewards);
        }
    }
}
