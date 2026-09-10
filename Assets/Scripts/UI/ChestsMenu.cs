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
    /// Onglet Coffres : une carte par coffre, sur toute la largeur. Chaque carte porte son propre
    /// aperçu 3D en tête, au-dessus de son bouton d'achat. L'ouverture elle-même se joue sur
    /// <see cref="ChestOpeningScreen"/>, qui prend tout l'écran.
    ///
    /// La mise en page des cartes vient de <see cref="Storefront"/>, partagée avec la Boutique.
    /// </summary>
    public class ChestsMenu
    {
        public GameObject Root { get; private set; }

        /// <summary>Levé quand un coffre vient d'être payé et attribué : au menu d'afficher la séquence.</summary>
        public Action<ChestInfo, List<ChestReward>> OpeningRequested;

        class ChestCard
        {
            public ChestInfo Chest;
            public Button Button;
            public TextMeshProUGUI ButtonLabel;
            public ChestThumbnail Thumbnail;
            public GameObject PreviewPlaceholder;
            public TextMeshProUGUI Odds;
        }

        UITheme theme;
        readonly List<ChestCard> cards = new List<ChestCard>();

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "ChestsPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            Storefront.BuildHeader(Root.transform, theme, "COFFRES",
                "Les coffres s'achètent avec les pièces gagnées en jeu.");

            BuildChestCards(Root.transform);
        }

        void BuildChestCards(Transform root)
        {
            var chests = ChestCatalog.All;
            var row = Storefront.BuildRow(root, "ChestRow");

            for (int i = 0; i < chests.Length; i++)
            {
                var chest = chests[i];
                var card = Storefront.BuildCard(row, theme, i, chests.Length, chest.Id,
                    chest.Name, chest.Description, OddsSummary(chest), $"OUVRIR — {chest.Price}",
                    () => OnOpenPressed(chest));

                var thumbnail = BuildPreview(card.Preview, chest, out var placeholder);

                cards.Add(new ChestCard
                {
                    Chest = chest,
                    Button = card.Action,
                    ButtonLabel = card.ActionLabel,
                    Thumbnail = thumbnail,
                    PreviewPlaceholder = placeholder,
                    Odds = card.Note,
                });
            }
        }

        /// <summary>Aperçu 3D du coffre, dans la zone que la carte lui réserve.</summary>
        ChestThumbnail BuildPreview(RectTransform slot, ChestInfo chest, out GameObject placeholder)
        {
            Storefront.AddPreviewGlow(slot, new Color(0.42f, 0.46f, 0.62f, 0.20f));

            placeholder = UIFactory.AddText(slot, "Missing", chest.Name, 16, theme.NavTextInactive,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10),
                FontStyles.Italic).gameObject;

            var render = UIFactory.AddRawImage(slot, "Render",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return ChestThumbnail.Create(render);
        }

        /// <summary>
        /// Chances lues par le joueur : la probabilité que le coffre contienne au moins un lot du type,
        /// et non le poids d'un tirage isolé, qui ne veut rien dire dès qu'il y a plusieurs lots.
        /// Un type dont le vivier est vide disparaît de la liste au lieu d'être promis pour rien.
        /// </summary>
        static string OddsSummary(ChestInfo chest)
        {
            var lines = new List<string> { $"{chest.RewardCount} lots par coffre" };

            foreach (var kind in new[] { ChestRewardKind.Coins, ChestRewardKind.Upgrade, ChestRewardKind.Paint, ChestRewardKind.Moto })
            {
                float chance = ChestManager.ChanceOfAtLeastOne(chest, kind);
                if (chance <= 0f) continue;

                lines.Add($"{FormatChance(chance)}  {KindLabel(kind)}");
            }

            return string.Join("\n", lines);
        }

        static string FormatChance(float chance)
        {
            if (chance >= 0.999f) return "garanti";
            if (chance < 0.01f) return $"{chance * 100f:0.0}%";
            return $"{Mathf.RoundToInt(chance * 100f)}%";
        }

        static string KindLabel(ChestRewardKind kind)
        {
            switch (kind)
            {
                case ChestRewardKind.Moto: return "Moto";
                case ChestRewardKind.Upgrade: return "Amélioration";
                case ChestRewardKind.Paint: return "Peinture";
                default: return "Pièces";
            }
        }

        public void RefreshOnShow()
        {
            foreach (var card in cards)
            {
                if (card.Thumbnail == null) continue;

                bool has3D = card.Thumbnail.Show(card.Chest);
                card.PreviewPlaceholder.SetActive(!has3D);
            }

            RefreshAffordability();
        }

        public void RefreshAffordability()
        {
            foreach (var card in cards)
            {
                // Recalculé ici : débloquer la dernière peinture change les chances de tous les coffres.
                card.Odds.text = OddsSummary(card.Chest);

                bool affordable = ChestManager.CanAfford(card.Chest);
                card.Button.interactable = affordable;
                card.ButtonLabel.text = $"OUVRIR — {card.Chest.Price}";
                UIFactory.SetButtonColor(card.Button, affordable ? theme.Accent : theme.PanelAlt);
            }
        }

        void OnOpenPressed(ChestInfo chest)
        {
            // Les lots sont tirés et crédités tout de suite ; l'écran d'ouverture ne fait que les révéler.
            var rewards = ChestManager.Open(chest);
            if (rewards == null || rewards.Count == 0) return;

            RefreshAffordability();
            OpeningRequested?.Invoke(chest, rewards);
        }
    }
}
