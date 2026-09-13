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
    /// Récapitulatif affiché au retour au menu : ce que la session a produit, et les missions
    /// qu'elle a bouclées. Sans lui, quitter la Métropole ramenait le joueur au menu sans rien lui
    /// dire de ce qu'il venait de faire.
    ///
    /// L'écran ne distribue rien : les pièces ont été versées au fil de la conduite, à chaque
    /// bandeau. Il ne fait que rendre compte, ce qui lui évite d'être un point de passage obligé —
    /// une session interrompue par une mise en veille ne coûte donc aucune récompense.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunSummaryScreen : MonoBehaviour
    {
        const float PanelWidth = 1040f;
        const float PanelHeight = 620f;
        const float RowHeight = 46f;

        /// <summary>
        /// Grandeurs montrées, dans l'ordre de lecture : d'abord ce que le joueur a cherché à faire
        /// (le wheeling), ensuite ce qu'il a fait en chemin. Les compteurs restés à zéro sont
        /// masqués : une session sans roue avant n'a pas à afficher « 0 s ».
        /// </summary>
        static readonly MissionMetric[] StatOrder =
        {
            MissionMetric.WheelieDistance,
            MissionMetric.WheelieStreak,
            MissionMetric.BalanceTime,
            MissionMetric.StoppieTime,
            MissionMetric.StoppieCount,
            MissionMetric.Distance,
            MissionMetric.TopSpeed,
            MissionMetric.Falls,
        };

        /// <summary>
        /// Construit et affiche le récapitulatif. Retourne false, sans rien construire, quand la
        /// session est trop courte pour mériter un écran : entrer puis ressortir aussitôt ne doit pas
        /// imposer un bilan vide à valider.
        /// </summary>
        public static bool TryShow(Transform canvasRoot, UITheme theme, RunStats stats, Action onContinue)
        {
            if (stats == null || !stats.Meaningful) return false;

            var host = UIFactory.CreateUIObject("RunSummary", canvasRoot);
            UIFactory.SetRect(host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var screen = host.gameObject.AddComponent<RunSummaryScreen>();
            screen.Build(theme, stats, onContinue);
            return true;
        }

        void Build(UITheme theme, RunStats stats, Action onContinue)
        {
            // Voile opaque jusqu'aux bords : il doit couvrir la route, qui continue de défiler derrière.
            var veil = UIFactory.AddPanel(transform, "Veil", new Color(0.04f, 0.04f, 0.05f, 0.93f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            veil.raycastTarget = true;

            var panel = UIFactory.AddPanel(transform, "Panel", theme.Panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-PanelWidth / 2f, -PanelHeight / 2f), new Vector2(PanelWidth / 2f, PanelHeight / 2f),
                rounded: true);

            UIFactory.AddText(panel.transform, "Title", "SESSION TERMINÉE", UITheme.FontTitle, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -78f), new Vector2(-40f, -24f), FontStyles.Bold | FontStyles.Italic);

            BuildStats(panel.transform, theme, stats);
            BuildMissions(panel.transform, theme, stats);
            BuildFooter(panel.transform, theme, stats, onContinue);
        }

        /// <summary>Colonne de gauche : les grandeurs de la session.</summary>
        void BuildStats(Transform panel, UITheme theme, RunStats stats)
        {
            var column = UIFactory.AddPanel(panel, "Stats", Color.clear,
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(40f, 110f), new Vector2(-16f, -96f));
            column.raycastTarget = false;

            float y = 0f;
            foreach (var metric in StatOrder)
            {
                float value = stats.Get(metric);
                if (value <= 0f) continue;

                UIFactory.AddText(column.transform, "Label_" + metric, MissionCatalog.MetricLabel(metric),
                    UITheme.FontBody, theme.TextMuted, TextAnchor.MiddleLeft,
                    new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(0f, y - RowHeight), new Vector2(0f, y));

                UIFactory.AddText(column.transform, "Value_" + metric, MissionCatalog.FormatValue(metric, value),
                    UITheme.FontBody, theme.Text, TextAnchor.MiddleRight,
                    new Vector2(0.62f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - RowHeight), new Vector2(0f, y),
                    FontStyles.Bold);

                y -= RowHeight;
            }

            if (Mathf.Approximately(y, 0f))
            {
                UIFactory.AddText(column.transform, "Empty", "Aucune figure sur cette session.",
                    UITheme.FontBody, theme.TextMuted, TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -RowHeight), new Vector2(0f, 0f),
                    FontStyles.Italic);
            }
        }

        /// <summary>Colonne de droite : les missions bouclées pendant la session.</summary>
        void BuildMissions(Transform panel, UITheme theme, RunStats stats)
        {
            var column = UIFactory.AddPanel(panel, "Missions", Color.clear,
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(16f, 110f), new Vector2(-40f, -96f));
            column.raycastTarget = false;

            UIFactory.AddText(column.transform, "Heading", "RÉCOMPENSES", UITheme.FontHeading, theme.Accent,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -40f), new Vector2(0f, 0f), FontStyles.Bold);

            if (stats.Earned.Count == 0)
            {
                UIFactory.AddText(column.transform, "None", "Rien de bouclé cette fois.\nToute la progression est conservée.",
                    UITheme.FontBody, theme.TextMuted, TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -140f), new Vector2(0f, -54f),
                    FontStyles.Italic);
                return;
            }

            float y = -54f;
            foreach (var notice in stats.Earned)
            {
                var row = UIFactory.AddPanel(column.transform, "Row", theme.PanelAlt,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - RowHeight - 8f), new Vector2(0f, y),
                    rounded: true);
                row.raycastTarget = false;

                UIFactory.AddText(row.transform, "Label", notice.Label, UITheme.FontLabel, theme.Text,
                    TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
                    new Vector2(16f, 0f), new Vector2(0f, 0f));

                // Une mission bouclée n'a encore rien versé : la distinguer ici évite que le joueur
                // compte deux fois, une au bilan et une au moment où il l'encaisse pour de bon.
                UIFactory.AddText(row.transform, "Coins",
                    notice.Pending ? $"{notice.Coins} à prendre" : $"+{notice.Coins}",
                    UITheme.FontLabel, notice.Pending ? theme.TextMuted : theme.Coin,
                    TextAnchor.MiddleRight, new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                    new Vector2(0f, 0f), new Vector2(-16f, 0f), FontStyles.Bold);

                y -= RowHeight + 14f;
            }
        }

        void BuildFooter(Transform panel, UITheme theme, RunStats stats, Action onContinue)
        {
            // Deux totaux distincts : ce qui est déjà sur le compte, et ce qui attend dans l'onglet
            // Missions. Les additionner ferait croire à un solde que le joueur n'a pas encore.
            var lines = new List<string>();
            if (stats.CoinsEarned > 0) lines.Add($"+{stats.CoinsEarned} pièces gagnées");
            if (stats.CoinsPending > 0) lines.Add($"{stats.CoinsPending} pièces à récupérer dans Missions");

            if (lines.Count > 0)
            {
                UIFactory.AddText(panel, "Earned", string.Join("\n", lines), UITheme.FontBody,
                    stats.CoinsEarned > 0 ? theme.Coin : theme.Text, TextAnchor.MiddleLeft,
                    new Vector2(0f, 0f), new Vector2(0.62f, 0f),
                    new Vector2(40f, 20f), new Vector2(0f, 96f), FontStyles.Bold);
            }

            UIFactory.AddButton(panel, "Continue", "CONTINUER", theme.Accent, theme.Text, UITheme.FontBody,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 26f), new Vector2(-40f, 92f),
                () =>
                {
                    // Détruit avant de charger : sur un appareil lent, l'écran restait affiché par-dessus
                    // l'écran de chargement le temps de la bascule de scène.
                    Destroy(gameObject);
                    onContinue?.Invoke();
                },
                ButtonKind.Primary);
        }
    }
}
