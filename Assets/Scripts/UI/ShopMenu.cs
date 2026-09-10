using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Onglet Boutique : les achats en argent réel — lots de pièces et retrait des publicités.
    /// Les coffres, qui se paient en pièces, ont leur propre onglet (<see cref="ChestsMenu"/>).
    ///
    /// La bannière et les cartes viennent de <see cref="Storefront"/>, comme pour les coffres : les
    /// deux vitrines doivent se lire de la même façon, seul leur contenu change.
    /// </summary>
    public class ShopMenu
    {
        class ShopCard
        {
            public CoinPack Pack;          // nul pour la carte de retrait des publicités
            public StorefrontCard Card;
        }

        public GameObject Root { get; private set; }

        UITheme theme;
        readonly List<ShopCard> cards = new List<ShopCard>();

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "ShopPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            Storefront.BuildHeader(Root.transform, theme, "BOUTIQUE",
                "Achats facturés par ton store. Les pièces sont créditées aussitôt.");

            BuildCards(Root.transform);
        }

        void BuildCards(Transform root)
        {
            var packs = CoinPackCatalog.All;
            var row = Storefront.BuildRow(root, "ShopRow");

            // Le retrait des publicités partage la rangée avec les lots : c'est un achat de même
            // nature, et lui donner une bande à part le ferait passer pour une bannière de pub.
            int count = packs.Length + 1;

            for (int i = 0; i < packs.Length; i++)
            {
                var pack = packs[i];
                var card = Storefront.BuildCard(row, theme, i, count, pack.Id,
                    pack.Name, pack.Description, PackNote(pack), $"ACHETER — {pack.Price}",
                    () => OnPackPressed(pack));

                BuildCoinPreview(card.Preview, pack);
                cards.Add(new ShopCard { Pack = pack, Card = card });
            }

            var adsCard = Storefront.BuildCard(row, theme, packs.Length, count, MonetizationManager.RemoveAdsId,
                "Sans publicité", "Plus aucune coupure entre deux parties.",
                "Achat unique\nvalable sur ce compte", $"ACHETER — {MonetizationManager.RemoveAdsPrice}",
                OnRemoveAdsPressed);

            BuildAdsPreview(adsCard.Preview);
            cards.Add(new ShopCard { Pack = null, Card = adsCard });
        }

        /// <summary>Détail du lot, à la place des chances affichées sur une carte de coffre.</summary>
        static string PackNote(CoinPack pack)
        {
            if (pack.Bonus <= 0) return $"{pack.Coins} pièces";

            int percent = Mathf.RoundToInt(pack.Bonus * 100f / pack.Coins);
            return $"{pack.Coins} pièces\n+ {pack.Bonus} offertes  ({percent}%)";
        }

        /// <summary>
        /// Visuel d'un lot : un halo à la teinte du lot et le total en gros. Faute de modèle 3D pour
        /// les pièces, c'est le nombre lui-même qui sert d'illustration.
        /// </summary>
        void BuildCoinPreview(RectTransform slot, CoinPack pack)
        {
            Storefront.AddPreviewGlow(slot, new Color(pack.Swatch.r, pack.Swatch.g, pack.Swatch.b, 0.22f));

            var coin = UIFactory.AddGlow(slot, "Coin", pack.Swatch,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-92, -92), new Vector2(92, 92));
            coin.raycastTarget = false;

            UIFactory.AddText(slot, "Amount", pack.Total.ToString(), 44, theme.Text, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);
        }

        void BuildAdsPreview(RectTransform slot)
        {
            Storefront.AddPreviewGlow(slot, new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.20f));

            UIFactory.AddText(slot, "Mark", "ZÉRO PUB", 34, theme.Accent, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold | FontStyles.Italic);
        }

        public void RefreshOnShow()
        {
            bool adsRemoved = MonetizationManager.AdsRemoved;

            foreach (var entry in cards)
            {
                if (entry.Pack != null)
                {
                    entry.Card.Action.interactable = true;
                    entry.Card.ActionLabel.text = $"ACHETER — {entry.Pack.Price}";
                    UIFactory.SetButtonColor(entry.Card.Action, theme.Accent);
                    continue;
                }

                // Produit non consommable : une fois acheté, la carte annonce l'état au lieu de
                // proposer un achat que le store refuserait.
                entry.Card.Note.text = adsRemoved
                    ? "Publicités désactivées\nsur ce compte"
                    : "Achat unique\nvalable sur ce compte";
                entry.Card.Action.interactable = !adsRemoved;
                entry.Card.ActionLabel.text = adsRemoved
                    ? "DÉJÀ ACTIF"
                    : $"ACHETER — {MonetizationManager.RemoveAdsPrice}";
                UIFactory.SetButtonColor(entry.Card.Action, adsRemoved ? theme.PanelAlt : theme.Accent);
            }
        }

        void OnPackPressed(CoinPack pack)
        {
            SetAllInteractable(false);
            MonetizationManager.PurchaseCoinPack(pack, _ => RefreshOnShow());
        }

        void OnRemoveAdsPressed()
        {
            SetAllInteractable(false);
            MonetizationManager.PurchaseRemoveAds(_ => RefreshOnShow());
        }

        /// <summary>
        /// Fige la vitrine le temps de la transaction : sans cela, un second appui pendant que le
        /// store répond lancerait un deuxième achat.
        /// </summary>
        void SetAllInteractable(bool value)
        {
            foreach (var entry in cards) entry.Card.Action.interactable = value;
        }
    }
}
