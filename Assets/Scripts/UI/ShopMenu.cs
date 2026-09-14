using System.Collections.Generic;
using System.Globalization;
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
    /// La page ne range pas ses offres à égalité, contrairement à l'onglet Coffres : un lot occupe
    /// la grande carte de gauche, les deux autres se partagent la colonne de droite, et le retrait
    /// des publicités descend dans une bande à lui. Trois coffres sont trois choix de même rang ;
    /// trois lots de pièces ne le sont pas — ils ne diffèrent que par la quantité, et une vitrine
    /// qui ne désigne pas l'offre à regarder laisse le joueur comparer trois fois le même produit.
    ///
    /// Les briques (bannière, rangée, carte héros, bande, halo) viennent de <see cref="Storefront"/>,
    /// que l'onglet Coffres emploie aussi : les deux vitrines sont faites de la même matière, même
    /// si elles ne l'assemblent pas pareil.
    /// </summary>
    public class ShopMenu
    {
        /// <summary>Une carte de lot de la colonne de droite, et le lot qu'elle vend.</summary>
        class PackCard
        {
            public CoinPack Pack;
            public Button Action;
            public TextMeshProUGUI ActionLabel;
        }

        // La bande « sans publicité » dit la même chose à la construction et après l'achat : les
        // textes vivent ici plutôt que recopiés aux deux endroits, où ils finiraient par diverger.
        const string AdsTitle = "SANS PUBLICITÉ";
        const string AdsDescription = "Plus aucune coupure entre deux parties.";
        const string AdsAvailableNote = "Achat unique, valable sur ce compte.";
        const string AdsOwnedNote = "Publicités désactivées sur ce compte.";

        const string RibbonText = "MEILLEURE OFFRE";

        /// <summary>Part de la rangée prise par la carte héros. Le reste revient à la colonne de droite.</summary>
        const float HeroWidth = 0.56f;

        // Mesures des cartes de la colonne : même lecture de bas en haut que partout ailleurs.
        const float PackPadding = 22f;
        const float PackButtonBottom = 18f;
        const float PackButtonHeight = 60f;
        const float PackButtonTop = PackButtonBottom + PackButtonHeight;
        const float PackBonusBottom = PackButtonTop + 14f;
        const float PackBonusHeight = 34f;
        const float PackAmountHeight = 50f;
        const float PackInfoSize = 52f;

        /// <summary>
        /// Élévation de la caméra au-dessus de la pile de pièces, en fraction de son rayon. La carte
        /// héros regarde d'un peu plus haut : sa pile est vue en grand, et on veut y lire la face de
        /// la pièce du dessus autant que la tranche de celles du dessous.
        /// </summary>
        const float HeroPreviewElevation = 0.5f;
        const float PackPreviewElevation = 0.42f;

        /// <summary>
        /// Les totaux se lisent « 19 000 » et non « 19000 ». Séparateur espace ordinaire et non
        /// insécable : la police de l'UI est chargée dynamiquement et rien ne garantit qu'elle porte
        /// U+00A0. Le chiffre tient sur une ligne bien assez large pour qu'aucune coupure ne menace.
        /// </summary>
        static readonly NumberFormatInfo ThousandsFormat = new NumberFormatInfo { NumberGroupSeparator = " " };

        public GameObject Root { get; private set; }

        UITheme theme;

        StorefrontHeroCard heroCard;
        CoinPack heroPack;
        StorefrontBand adsBand;
        readonly List<PackCard> packCards = new List<PackCard>();

        public void Build(Transform parent, UITheme t)
        {
            theme = t;

            var panel = UIFactory.AddPanel(parent, "ShopPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            Storefront.BuildHeader(Root.transform, theme, "BOUTIQUE",
                "Achats facturés par ton store. Les pièces sont créditées aussitôt.");

            BuildAdsBand(Root.transform);
            BuildPacks(Root.transform);

            // La bande naît dans son état « disponible ». Un joueur qui a déjà payé le retrait des
            // publicités le verrait donc proposé à la vente jusqu'au premier changement d'onglet.
            RefreshAdsBand();
        }

        /// <summary>
        /// Range les lots : le lot mis en avant sur la carte héros, les autres dans la colonne. Si le
        /// catalogue n'en désigne aucun, le premier prend la place — la page ne doit pas se retrouver
        /// sans sa grande carte parce qu'un booléen manque.
        /// </summary>
        void BuildPacks(Transform root)
        {
            var packs = CoinPackCatalog.All;
            if (packs.Length == 0) return;

            var row = Storefront.BuildRow(root, "ShopRow", Storefront.BottomMargin + Storefront.BandReserve);

            heroPack = System.Array.Find(packs, p => p.Highlight) ?? packs[0];
            BuildHero(row, heroPack);

            var column = UIFactory.CreateUIObject("PackColumn", row);
            UIFactory.SetRect(column, new Vector2(HeroWidth, 0), Vector2.one,
                new Vector2(Storefront.CardGap * 0.5f, 0), Vector2.zero);

            var rest = new List<CoinPack>();
            foreach (var pack in packs)
            {
                if (pack != heroPack) rest.Add(pack);
            }

            for (int i = 0; i < rest.Count; i++)
            {
                BuildPackCard(column, i, rest.Count, rest[i]);
            }
        }

        void BuildHero(Transform row, CoinPack pack)
        {
            heroCard = Storefront.BuildHeroCard(row, theme, HeroWidth, pack.Id,
                pack.Name, AmountMarkup(pack.Total), RibbonText, $"ACHETER — {pack.Price}", pack.Swatch,
                () => OnPackPressed(pack));

            heroCard.SetBonus(BonusText(pack));
            BuildCoinPreview(heroCard.Preview, pack, HeroPreviewElevation);
        }

        /// <summary>Carte <paramref name="index"/> sur <paramref name="count"/>, empilées de haut en bas.</summary>
        void BuildPackCard(Transform column, int index, int count, CoinPack pack)
        {
            // Les cartes se comptent depuis le haut, alors que l'axe Y de l'UI monte : la première
            // carte occupe donc la tranche la plus haute, d'où le retournement des bornes.
            float top = 1f - (float)index / count;
            float bottom = 1f - (float)(index + 1) / count;

            var panel = UIFactory.AddPanel(column, "Card_" + pack.Id, theme.Panel,
                new Vector2(0, bottom), new Vector2(1, top),
                new Vector2(0, Storefront.CardGap * 0.5f), new Vector2(0, -Storefront.CardGap * 0.5f),
                rounded: true);
            panel.raycastTarget = false;

            var button = UIFactory.AddButton(panel.transform, "Action", $"ACHETER — {pack.Price}",
                theme.Accent, Color.white, UITheme.FontLabel,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, PackButtonBottom), new Vector2(-18, PackButtonTop),
                () => OnPackPressed(pack), ButtonKind.Primary);

            float amountBottom = PackBonusBottom;
            string bonus = BonusText(pack);
            if (!string.IsNullOrEmpty(bonus))
            {
                UIFactory.AddText(panel.transform, "Bonus", bonus, UITheme.FontLabel, theme.Coin,
                    TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(PackPadding, PackBonusBottom),
                    new Vector2(-PackPadding, PackBonusBottom + PackBonusHeight),
                    FontStyles.Bold | FontStyles.Italic);

                amountBottom = PackBonusBottom + PackBonusHeight + 8f;
            }

            UIFactory.AddText(panel.transform, "Amount", AmountMarkup(pack.Total), UITheme.FontTitle, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(PackPadding, amountBottom), new Vector2(-PackPadding, amountBottom + PackAmountHeight),
                FontStyles.Bold | FontStyles.Italic);

            // La pastille « i » porte le nom et la description du lot : à cette largeur, ils ne
            // peuvent pas s'écrire sur la carte, alors qu'ils tiennent sur la carte héros.
            UIFactory.AddInfoButton(panel.transform, "Info", theme, pack.Name, pack.Description,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-PackPadding - PackInfoSize, -PackPadding - PackInfoSize),
                new Vector2(-PackPadding, -PackPadding));

            var preview = UIFactory.CreateUIObject("Preview", panel.transform);
            UIFactory.SetRect(preview, Vector2.zero, Vector2.one,
                new Vector2(18, amountBottom + PackAmountHeight + 8f),
                new Vector2(-18 - PackInfoSize, -18));

            BuildCoinPreview(preview, pack, PackPreviewElevation);

            packCards.Add(new PackCard
            {
                Pack = pack,
                Action = button,
                ActionLabel = button.GetComponentInChildren<TextMeshProUGUI>(),
            });
        }

        /// <summary>
        /// Visuel d'un lot : la pile de pièces en 3D sur un halo à la teinte du lot. Le modèle est le
        /// même pour les trois — c'est la taille de la carte qui fait la différence, pas la pile.
        /// Si le modèle manque, le repli reprend l'ancien visuel : le total écrit en gros.
        /// </summary>
        void BuildCoinPreview(RectTransform slot, CoinPack pack, float elevation)
        {
            Storefront.AddPreviewGlow(slot, new Color(pack.Swatch.r, pack.Swatch.g, pack.Swatch.b, 0.22f));

            var fallback = UIFactory.AddText(slot, "Missing", Thousands(pack.Total), UITheme.FontDisplay, theme.Text,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10),
                FontStyles.Bold | FontStyles.Italic);

            var render = UIFactory.AddRawImage(slot, "Render", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var thumbnail = CoinStackThumbnail.Create(render);
            if (thumbnail.Show(pack.ModelResourcePath, elevation)) fallback.gameObject.SetActive(false);
        }

        void BuildAdsBand(Transform root)
        {
            adsBand = Storefront.BuildBand(root, theme, "AdsBand", AdsTitle,
                AdsDescription + " " + AdsAvailableNote,
                MonetizationManager.RemoveAdsPrice, OnRemoveAdsPressed);
        }

        /// <summary>Total du lot, suivi du mot « pièces » en plus petit et en gris.</summary>
        string AmountMarkup(int total)
        {
            return $"{Thousands(total)}<size={UITheme.FontLabel}><color=#{ColorUtility.ToHtmlStringRGB(theme.TextMuted)}> pièces</color></size>";
        }

        static string Thousands(int value)
        {
            return value.ToString("#,0", ThousandsFormat);
        }

        /// <summary>Mention du bonus, ou vide pour un lot qui n'en a pas.</summary>
        static string BonusText(CoinPack pack)
        {
            if (pack.Bonus <= 0) return string.Empty;

            return $"+ {Thousands(pack.Bonus)} OFFERTES · {pack.BonusPercent} %";
        }

        public void RefreshOnShow()
        {
            if (heroCard != null && heroPack != null)
            {
                heroCard.Action.interactable = true;
                heroCard.ActionLabel.text = $"ACHETER — {heroPack.Price}";
                UIFactory.SetButtonColor(heroCard.Action, theme.Accent);
            }

            foreach (var card in packCards)
            {
                card.Action.interactable = true;
                card.ActionLabel.text = $"ACHETER — {card.Pack.Price}";
                UIFactory.SetButtonColor(card.Action, theme.Accent);
            }

            RefreshAdsBand();
        }

        /// <summary>
        /// Produit non consommable : une fois acheté, la bande annonce l'état au lieu de proposer un
        /// achat que le store refuserait. Le filet de marque s'éteint avec elle — une bande éteinte
        /// mais toujours soulignée de rouge continuerait d'appeler le regard pour rien.
        /// </summary>
        void RefreshAdsBand()
        {
            if (adsBand == null) return;

            bool removed = MonetizationManager.AdsRemoved;

            adsBand.Body.text = removed ? AdsOwnedNote : AdsDescription + " " + AdsAvailableNote;
            adsBand.Rule.color = removed ? theme.PanelAlt : theme.Accent;
            adsBand.Action.interactable = !removed;
            adsBand.ActionLabel.text = removed ? "DÉJÀ ACTIF" : MonetizationManager.RemoveAdsPrice;
            UIFactory.SetButtonColor(adsBand.Action, removed ? theme.PanelAlt : theme.Accent);
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
            if (heroCard != null) heroCard.Action.interactable = value;
            foreach (var card in packCards) card.Action.interactable = value;
            if (adsBand != null) adsBand.Action.interactable = value;
        }
    }
}
