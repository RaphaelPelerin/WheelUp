using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Une carte de boutique : visuel en tête, nom, pastille « i », puis le bouton d'achat calé en
    /// bas. Les champs sont exposés pour que chaque onglet remplisse la zone d'aperçu à sa façon —
    /// rendu 3D pour un coffre, pastille pour un lot de pièces.
    ///
    /// La description et la mention ne sont plus écrites sur la carte : à cette largeur, il aurait
    /// fallu les composer en corps 14 ou 15. Elles partent dans la page du bouton « i », où elles
    /// tiennent en gros, et la place ainsi libérée revient à l'aperçu.
    /// </summary>
    public class StorefrontCard
    {
        public Image Panel;
        public RectTransform Preview;
        public TextMeshProUGUI Name;
        public InfoButton Info;
        public Button Action;
        public TextMeshProUGUI ActionLabel;

        /// <summary>
        /// Compose la page d'information. Appelée à la construction, puis chaque fois qu'un écran
        /// change ce qu'il a à dire — les chances d'un coffre, l'état d'un achat déjà effectué.
        /// </summary>
        public void SetInfo(string description, string note)
        {
            if (Info == null) return;

            if (string.IsNullOrEmpty(note)) Info.Body = description;
            else if (string.IsNullOrEmpty(description)) Info.Body = note;
            else Info.Body = description + "\n\n" + note;
        }
    }

    /// <summary>
    /// Grande carte d'une vitrine : celle de l'offre mise en avant. Elle a la place d'écrire ce que
    /// la carte ordinaire cache derrière son bouton « i » — d'où le chiffre en gros et la mention,
    /// et d'où l'absence de pastille « i ».
    /// </summary>
    public class StorefrontHeroCard
    {
        public Image Panel;
        public RectTransform Preview;
        public TextMeshProUGUI Name;
        /// <summary>Le chiffre qui porte l'offre : le total de pièces d'un lot.</summary>
        public TextMeshProUGUI Headline;
        public TextMeshProUGUI Ribbon;
        public Button Action;
        public TextMeshProUGUI ActionLabel;

        Image bonusBadge;
        TextMeshProUGUI bonusLabel;

        public void BindBonus(Image badge, TextMeshProUGUI label)
        {
            bonusBadge = badge;
            bonusLabel = label;
        }

        /// <summary>Mention en pastille sous le chiffre. Vide : la pastille disparaît au lieu de rester vide.</summary>
        public void SetBonus(string text)
        {
            bool shown = !string.IsNullOrEmpty(text);
            if (bonusBadge != null) bonusBadge.gameObject.SetActive(shown);
            if (bonusLabel != null && shown) bonusLabel.text = text;
        }
    }

    /// <summary>
    /// Bande pleine largeur posée sous la rangée, pour une offre qui n'est pas du même ordre que les
    /// cartes — le retrait des publicités, qui ne s'achète qu'une fois et ne se compare à aucun lot.
    /// La mettre en carte parmi les cartes la faisait passer pour une marchandise de plus.
    /// </summary>
    public class StorefrontBand
    {
        public Image Panel;
        public Image Rule;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Body;
        public Button Action;
        public TextMeshProUGUI ActionLabel;
    }

    /// <summary>
    /// Briques communes aux deux vitrines (Coffres et Boutique) : la bannière, la rangée, la carte,
    /// la carte héros, la bande et le halo d'aperçu. Elles vivent ici plutôt que dans chaque onglet
    /// pour que les deux vitrines soient faites de la même matière — dupliquer les mesures, c'est se
    /// garantir qu'elles divergeront à la première retouche.
    ///
    /// Ce sont bien des briques et non une mise en page imposée : chaque onglet les assemble à sa
    /// façon. Les Coffres alignent trois cartes égales ; la Boutique met un lot en avant sur une
    /// carte héros et range les autres à côté, parce qu'une vitrine qui vend en argent réel doit
    /// désigner l'offre à regarder, là où trois coffres sont trois choix de même rang.
    ///
    /// La carte se lit de bas en haut : le bouton, puis la mention, la description et le nom
    /// s'empilent au-dessus de lui à hauteur fixe, et l'aperçu prend tout ce qui reste. Dans l'autre
    /// sens (aperçu de hauteur fixe, texte calé sous lui), la dernière ligne passait sous le bouton
    /// dès que la fenêtre était un peu courte.
    /// </summary>
    public static class Storefront
    {
        public const float CardGap = 20f;
        public const float SideMargin = 30f;
        public const float HeaderHeight = 110f;
        public const float BottomMargin = 20f;

        const float CardPadding = 22f;
        const float ButtonBottom = 18f;
        const float ButtonHeight = 64f;         // le libellé est passé en corps 22
        const float NameHeight = 44f;           // une ligne en corps 28
        const float InfoSize = 56f;

        const float ButtonTop = ButtonBottom + ButtonHeight;
        const float NameBottom = ButtonTop + 16f;

        /// <summary>Hauteur réservée au bloc de texte : l'aperçu commence juste au-dessus.</summary>
        public const float PreviewBottom = NameBottom + NameHeight + 14f;

        /// <summary>Hauteur de la bande, gouttière comprise : ce que la rangée doit lui céder en bas.</summary>
        public const float BandHeight = 108f;
        public const float BandReserve = BandHeight + CardGap;

        // Mesures de la carte héros. Même lecture de bas en haut que la carte ordinaire, mais tout
        // est plus grand d'un cran et la mention s'écrit sur la carte au lieu de partir dans le « i ».
        const float HeroPadding = 28f;
        const float HeroButtonBottom = 24f;
        const float HeroButtonHeight = 72f;
        const float HeroButtonTop = HeroButtonBottom + HeroButtonHeight;
        const float HeroBonusBottom = HeroButtonTop + 18f;
        const float HeroBonusHeight = 42f;
        const float HeroHeadlineBottom = HeroBonusBottom + HeroBonusHeight + 14f;
        const float HeroHeadlineHeight = 64f;
        const float HeroNameBottom = HeroHeadlineBottom + HeroHeadlineHeight + 2f;
        const float HeroNameHeight = 42f;
        const float HeroPreviewBottom = HeroNameBottom + HeroNameHeight + 16f;

        const float RibbonWidth = 268f;
        const float RibbonHeight = 46f;
        /// <summary>Épaisseur du liseré qui entoure la carte héros, dessiné par un panneau plus grand dessous.</summary>
        const float HeroOutline = 3f;

        /// <summary>Bandeau de titre, identique d'un onglet à l'autre.</summary>
        public static void BuildHeader(Transform root, UITheme theme, string title, string subtitle)
        {
            // Le solde n'est affiché qu'une fois, dans le badge permanent en haut à droite du menu.
            UIFactory.AddText(root, "Title", title, UITheme.FontTitle, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -56), new Vector2(-30, -6));

            UIFactory.AddText(root, "Subtitle", subtitle, UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -98), new Vector2(-30, -58));
        }

        /// <summary>
        /// Rangée qui accueille les cartes. Les marges extérieures sont portées par la rangée et non
        /// par les cartes : sinon les cartes de bord perdraient la marge en plus de la demi-gouttière
        /// et seraient plus étroites que celle du milieu, qui ne perd que deux demi-gouttières.
        ///
        /// <paramref name="bottom"/> relève le bas de la rangée pour laisser la place à ce qui vient
        /// dessous — une bande, par exemple. Laissé à sa valeur par défaut, la rangée descend jusqu'à
        /// la marge basse de l'écran, comme dans l'onglet Coffres.
        /// </summary>
        public static RectTransform BuildRow(Transform root, string name, float bottom = BottomMargin)
        {
            var row = UIFactory.CreateUIObject(name, root);
            UIFactory.SetRect(row, Vector2.zero, Vector2.one,
                new Vector2(SideMargin, bottom), new Vector2(-SideMargin, -HeaderHeight));
            return row;
        }

        /// <summary>Carte numéro <paramref name="index"/> sur <paramref name="count"/>, à largeur égale.</summary>
        public static StorefrontCard BuildCard(Transform row, UITheme theme, int index, int count, string id,
            string name, string description, string note, string actionText, UnityAction onAction)
        {
            var panel = UIFactory.AddPanel(row, "Card_" + id, theme.Panel,
                new Vector2((float)index / count, 0), new Vector2((float)(index + 1) / count, 1),
                new Vector2(CardGap * 0.5f, 0), new Vector2(-CardGap * 0.5f, 0),
                rounded: true);
            panel.raycastTarget = false;

            // L'aperçu occupe tout ce que le bloc de texte laisse : sa hauteur suit celle de la carte.
            var preview = UIFactory.CreateUIObject("Preview", panel.transform);
            UIFactory.SetRect(preview, Vector2.zero, Vector2.one,
                new Vector2(18, PreviewBottom), new Vector2(-18, -18));

            // Le nom s'arrête avant la pastille : sans cette réserve, un nom long passerait dessous.
            var nameText = UIFactory.AddText(panel.transform, "Name", name, UITheme.FontHeading, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(CardPadding, NameBottom),
                new Vector2(-CardPadding - InfoSize - 12f, NameBottom + NameHeight),
                FontStyles.Bold | FontStyles.Italic);

            float infoCenter = NameBottom + NameHeight * 0.5f;
            var info = UIFactory.AddInfoButton(panel.transform, "Info", theme, name, string.Empty,
                new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-CardPadding - InfoSize, infoCenter - InfoSize * 0.5f),
                new Vector2(-CardPadding, infoCenter + InfoSize * 0.5f));

            var button = UIFactory.AddButton(panel.transform, "Action", actionText, theme.Accent, Color.white,
                UITheme.FontLabel, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, ButtonBottom), new Vector2(-18, ButtonTop),
                onAction, ButtonKind.Primary);

            var card = new StorefrontCard
            {
                Panel = panel,
                Preview = preview,
                Name = nameText,
                Info = info,
                Action = button,
                ActionLabel = button.GetComponentInChildren<TextMeshProUGUI>(),
            };
            card.SetInfo(description, note);
            return card;
        }

        /// <summary>
        /// Carte héros, posée dans une rangée dont elle occupe la fraction <paramref name="width"/> à
        /// gauche. <paramref name="tint"/> est la couleur de l'offre : elle ne sert qu'à réchauffer
        /// imperceptiblement le fond, pour que la carte ne soit pas du même gris que les autres.
        /// </summary>
        public static StorefrontHeroCard BuildHeroCard(Transform row, UITheme theme, float width, string id,
            string name, string headline, string ribbon, string actionText, Color tint, UnityAction onAction)
        {
            var anchorMax = new Vector2(width, 1);

            // Liseré : un panneau arrondi légèrement plus grand, glissé dessous. Un contour dessiné
            // par un composant Outline suivrait le rectangle et non les coins arrondis du sprite.
            var outline = UIFactory.AddPanel(row, "HeroOutline_" + id,
                new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.7f),
                Vector2.zero, anchorMax,
                new Vector2(-HeroOutline, -HeroOutline), new Vector2(-CardGap * 0.5f + HeroOutline, HeroOutline),
                rounded: true);
            outline.raycastTarget = false;

            var panel = UIFactory.AddPanel(row, "Hero_" + id, Color.Lerp(theme.Panel, tint, 0.07f),
                Vector2.zero, anchorMax,
                Vector2.zero, new Vector2(-CardGap * 0.5f, 0),
                rounded: true);
            panel.raycastTarget = false;

            var preview = UIFactory.CreateUIObject("Preview", panel.transform);
            UIFactory.SetRect(preview, Vector2.zero, Vector2.one,
                new Vector2(24, HeroPreviewBottom), new Vector2(-24, -24));

            var nameText = UIFactory.AddText(panel.transform, "Name", name, UITheme.FontHeading, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(HeroPadding, HeroNameBottom), new Vector2(-HeroPadding, HeroNameBottom + HeroNameHeight),
                FontStyles.Bold | FontStyles.Italic);

            var headlineText = UIFactory.AddText(panel.transform, "Headline", headline, UITheme.FontDisplay, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(HeroPadding, HeroHeadlineBottom),
                new Vector2(-HeroPadding, HeroHeadlineBottom + HeroHeadlineHeight),
                FontStyles.Bold | FontStyles.Italic);

            // Pastille de mention : sa largeur suit le texte, d'où le ContentSizeFitter plutôt qu'une
            // mesure fixe — « + 750 offertes » et « + 4 000 offertes · 27 % » n'ont pas la même longueur.
            // Le pivot passe à gauche : avec le pivot centré des autres éléments, la pastille
            // s'élargirait des deux côtés et quitterait l'alignement des textes au-dessus d'elle.
            var badge = UIFactory.AddPanel(panel.transform, "BonusBadge",
                new Color(theme.Coin.r, theme.Coin.g, theme.Coin.b, 0.16f),
                new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, Vector2.zero, rounded: true);
            badge.raycastTarget = false;

            var badgeRect = badge.rectTransform;
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(HeroPadding, HeroBonusBottom + HeroBonusHeight * 0.5f);
            badgeRect.sizeDelta = new Vector2(0f, HeroBonusHeight);

            var badgeLayout = badge.gameObject.AddComponent<HorizontalLayoutGroup>();
            badgeLayout.padding = new RectOffset(18, 18, 0, 0);
            badgeLayout.childForceExpandWidth = false;
            badgeLayout.childForceExpandHeight = true;

            var badgeFitter = badge.gameObject.AddComponent<ContentSizeFitter>();
            badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            badgeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var bonusText = UIFactory.AddText(badge.transform, "BonusLabel", string.Empty, UITheme.FontLabel,
                theme.Coin, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                FontStyles.Bold | FontStyles.Italic);

            var button = UIFactory.AddButton(panel.transform, "Action", actionText, theme.Accent, Color.white,
                UITheme.FontBody, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, HeroButtonBottom), new Vector2(-24, HeroButtonTop),
                onAction, ButtonKind.Primary);

            var ribbonPanel = UIFactory.AddPanel(panel.transform, "Ribbon", theme.Accent,
                new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-RibbonWidth, -RibbonHeight), Vector2.zero, rounded: true);
            ribbonPanel.raycastTarget = false;

            var ribbonText = UIFactory.AddText(ribbonPanel.transform, "Label", ribbon, UITheme.FontLabel, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                FontStyles.Bold | FontStyles.Italic);

            var card = new StorefrontHeroCard
            {
                Panel = panel,
                Preview = preview,
                Name = nameText,
                Headline = headlineText,
                Ribbon = ribbonText,
                Action = button,
                ActionLabel = button.GetComponentInChildren<TextMeshProUGUI>(),
            };
            card.BindBonus(badge, bonusText);
            card.SetBonus(null);
            return card;
        }

        /// <summary>
        /// Bande basse pleine largeur. Elle se pose sur le parent de la rangée, pas dans la rangée :
        /// c'est la rangée qui lui cède <see cref="BandReserve"/> en bas.
        /// </summary>
        public static StorefrontBand BuildBand(Transform root, UITheme theme, string name,
            string title, string body, string actionText, UnityAction onAction)
        {
            var panel = UIFactory.AddPanel(root, name, theme.Panel,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(SideMargin, BottomMargin), new Vector2(-SideMargin, BottomMargin + BandHeight),
                rounded: true);
            panel.raycastTarget = false;

            // Filet vertical à la couleur de marque : il signe la bande comme une offre du jeu sans
            // lui donner le fond rouge d'une bannière publicitaire.
            var rule = UIFactory.AddPanel(panel.transform, "Rule", theme.Accent,
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(0, 18), new Vector2(6, -18), rounded: true);
            rule.raycastTarget = false;

            const float ActionWidth = 230f;
            float textRight = -(ActionWidth + 48f);

            var titleText = UIFactory.AddText(panel.transform, "Title", title, UITheme.FontHeading, theme.Text,
                TextAnchor.LowerLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(30, 2), new Vector2(textRight, 40), FontStyles.Bold | FontStyles.Italic);

            var bodyText = UIFactory.AddText(panel.transform, "Body", body, UITheme.FontLabel, theme.TextMuted,
                TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(30, -36), new Vector2(textRight, -2));

            var button = UIFactory.AddButton(panel.transform, "Action", actionText, theme.Accent, Color.white,
                UITheme.FontLabel, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-ActionWidth - 24f, -30f), new Vector2(-24f, 30f),
                onAction, ButtonKind.Primary);

            return new StorefrontBand
            {
                Panel = panel,
                Rule = rule,
                Title = titleText,
                Body = bodyText,
                Action = button,
                ActionLabel = button.GetComponentInChildren<TextMeshProUGUI>(),
            };
        }

        /// <summary>
        /// Halo derrière un visuel de carte, au lieu d'un panneau : le sujet se détache sans qu'un
        /// rectangle l'encadre, et le halo déborde pour que sa bordure ne se lise pas comme un contour.
        /// </summary>
        public static Image AddPreviewGlow(Transform preview, Color color)
        {
            return UIFactory.AddGlow(preview, "Glow", color,
                Vector2.zero, Vector2.one, new Vector2(-40, -60), new Vector2(40, 60));
        }
    }
}
