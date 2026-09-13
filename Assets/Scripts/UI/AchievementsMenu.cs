using System;
using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Page Succès de l'onglet Missions : la progression qui ne se réinitialise jamais. Chaque
    /// famille suit une grandeur depuis la première partie et se franchit en quatre paliers, du
    /// Bronze à la Légende.
    ///
    /// Elle emprunte sa mise en page à <see cref="ProgressBoard"/>, comme les deux pages de
    /// missions : même bandeau de récompense, même liste défilante, mêmes gabarits de ligne. Seul
    /// le contenu change — onze familles au lieu de trois missions, et une liste qui défile
    /// vraiment.
    ///
    /// Un seul aperçu 3D pour toute la page, dans le bandeau : onze cartes porteraient onze caméras
    /// de rendu, pour onze fois le même coffre.
    /// </summary>
    public class AchievementsMenu
    {
        /// <summary>Levé quand un coffre de succès vient d'être attribué : au menu de jouer la séquence.</summary>
        public Action<ChestInfo, List<ChestReward>> OpeningRequested;

        UITheme theme;
        ProgressStrip strip;
        readonly List<ProgressRow> rows = new List<ProgressRow>();

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            strip = ProgressBoard.BuildStrip(parent, theme, OnChestPressed);

            var all = AchievementCatalog.All;
            var content = ProgressBoard.BuildScroll(parent, "AchievementsScroll", all.Length);

            for (int i = 0; i < all.Length; i++)
            {
                // Aucune action sur la ligne : un palier de succès se franchit en jouant et se paie
                // sur-le-champ. Le bouton n'est là que pour afficher le gain, à la même place que le
                // « Récupérer » des missions, pour que les deux listes gardent la même silhouette.
                rows.Add(ProgressBoard.BuildRow(content, theme, "Row_" + all[i].Id, i, null));
            }
        }

        public void RefreshOnShow()
        {
            var all = AchievementCatalog.All;

            for (int i = 0; i < rows.Count && i < all.Length; i++) Fill(rows[i], all[i]);

            RefreshStrip();
        }

        void Fill(ProgressRow row, AchievementInfo info)
        {
            bool complete = AchievementManager.IsComplete(info);
            int tier = AchievementManager.CurrentTier(info);
            int rewarded = AchievementManager.RewardedTier(info);

            row.Title.text = complete
                ? $"{info.Name} — terminé"
                : $"{info.Name} — {AchievementCatalog.TierLabel(tier)}";

            row.Detail.text = info.Describe(tier);
            row.Fill.fillAmount = AchievementManager.Ratio(info);

            // Les paliers déjà franchis se lisent sur la ligne de progression : c'est ce qui dit au
            // joueur qu'il a avancé, même quand le palier en cours est encore loin.
            string progress = MissionCatalog.FormatValue(info.Metric, AchievementManager.Progress(info));

            row.Progress.text = complete
                ? $"{progress} · les {info.TierCount} paliers sont tombés"
                : $"{progress} / {MissionCatalog.FormatValue(info.Metric, info.Targets[tier])}"
                  + $"   ·   palier {rewarded} / {info.TierCount}";

            row.ActionLabel.text = complete ? "✓" : $"+{info.Coins[tier]}";
            row.ActionLabel.color = complete ? theme.NavTextInactive : theme.Coin;

            // Le bouton reste éteint : il porte une information, pas un geste.
            row.Action.interactable = false;
            UIFactory.SetButtonColor(row.Action, theme.PanelAlt);

            row.Title.color = complete ? theme.TextMuted : theme.Text;
            row.Fill.color = complete ? theme.NavTextInactive : theme.Accent;
        }

        void RefreshStrip()
        {
            var next = NextClaimable();
            int pending = AchievementManager.PendingRewards();

            AchievementManager.Totals(out int unlocked, out int total);

            var chest = next != null
                ? AchievementManager.ChestFor(next)
                : ChestCatalog.Find(AchievementCatalog.All[0].ChestId);

            string chestName = chest != null ? chest.Name : "Coffre";

            if (next != null)
            {
                strip.Title.text = pending > 1
                    ? $"{pending} coffres de succès à ouvrir"
                    : $"{chestName} à ouvrir — {next.Name}";
            }
            else
            {
                strip.Title.text = $"{chestName} offerte à chaque famille terminée";
            }

            strip.Detail.text = $"{unlocked} / {total} paliers franchis";
            strip.ShowChest(chest);

            strip.Fill.fillAmount = total > 0 ? (float)unlocked / total : 0f;
            strip.Fill.color = next != null ? theme.Coin : theme.Accent;

            strip.Action.interactable = next != null;
            strip.ActionLabel.text = next != null ? "OUVRIR" : "COFFRE";
            UIFactory.SetButtonColor(strip.Action, next != null ? theme.Accent : theme.PanelAlt);
        }

        /// <summary>Première famille terminée dont le coffre n'a pas encore été pris, ou null.</summary>
        static AchievementInfo NextClaimable()
        {
            foreach (var achievement in AchievementCatalog.All)
            {
                if (AchievementManager.ChestReady(achievement)) return achievement;
            }
            return null;
        }

        void OnChestPressed()
        {
            var next = NextClaimable();
            if (next == null) return;

            var chest = AchievementManager.TakeChest(next);
            if (chest == null) return;

            // Offert : les lots sont attribués sans débit, puis révélés par l'écran d'ouverture.
            var rewards = ChestManager.Grant(chest);
            if (rewards == null || rewards.Count == 0) return;

            RefreshOnShow();
            OpeningRequested?.Invoke(chest, rewards);
        }
    }
}
