using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Une carte de boutique : visuel en tête, nom, description, mention en petit, puis le bouton
    /// d'achat calé en bas. Les champs sont exposés pour que chaque onglet remplisse la zone
    /// d'aperçu à sa façon — rendu 3D pour un coffre, pastille pour un lot de pièces.
    /// </summary>
    public class StorefrontCard
    {
        public Image Panel;
        public RectTransform Preview;
        public TextMeshProUGUI Name;
        public TextMeshProUGUI Description;
        public TextMeshProUGUI Note;
        public Button Action;
        public TextMeshProUGUI ActionLabel;
    }

    /// <summary>
    /// Mise en page commune aux deux vitrines (Coffres et Boutique). Elle vit ici plutôt que dans
    /// chaque onglet pour que les deux gardent exactement la même bannière et les mêmes cartes :
    /// dupliquer les mesures, c'est se garantir qu'elles divergeront à la première retouche.
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
        const float ButtonHeight = 56f;
        const float NoteHeight = 100f;          // cinq lignes de mention en corps 14
        const float DescriptionHeight = 46f;    // deux lignes
        const float NameHeight = 34f;

        const float ButtonTop = ButtonBottom + ButtonHeight;
        const float NoteBottom = ButtonTop + 18f;
        const float DescriptionBottom = NoteBottom + NoteHeight + 10f;
        const float NameBottom = DescriptionBottom + DescriptionHeight + 8f;

        /// <summary>Hauteur réservée au bloc de texte : l'aperçu commence juste au-dessus.</summary>
        public const float PreviewBottom = NameBottom + NameHeight + 14f;

        /// <summary>Bandeau de titre, identique d'un onglet à l'autre.</summary>
        public static void BuildHeader(Transform root, UITheme theme, string title, string subtitle)
        {
            // Le solde n'est affiché qu'une fois, dans le badge permanent en haut à droite du menu.
            UIFactory.AddText(root, "Title", title, 30, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -50), new Vector2(-30, -6));

            UIFactory.AddText(root, "Subtitle", subtitle, 16, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -84), new Vector2(-30, -58));
        }

        /// <summary>
        /// Rangée qui accueille les cartes. Les marges extérieures sont portées par la rangée et non
        /// par les cartes : sinon les cartes de bord perdraient la marge en plus de la demi-gouttière
        /// et seraient plus étroites que celle du milieu, qui ne perd que deux demi-gouttières.
        /// </summary>
        public static RectTransform BuildRow(Transform root, string name)
        {
            var row = UIFactory.CreateUIObject(name, root);
            UIFactory.SetRect(row, Vector2.zero, Vector2.one,
                new Vector2(SideMargin, BottomMargin), new Vector2(-SideMargin, -HeaderHeight));
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

            var nameText = UIFactory.AddText(panel.transform, "Name", name, 24, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(CardPadding, NameBottom), new Vector2(-CardPadding, NameBottom + NameHeight),
                FontStyles.Bold | FontStyles.Italic);

            var descriptionText = UIFactory.AddText(panel.transform, "Description", description, 15, theme.TextMuted,
                TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(CardPadding, DescriptionBottom), new Vector2(-CardPadding, DescriptionBottom + DescriptionHeight));

            var noteText = UIFactory.AddText(panel.transform, "Note", note, 14, theme.NavTextInactive,
                TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(CardPadding, NoteBottom), new Vector2(-CardPadding, NoteBottom + NoteHeight));

            var button = UIFactory.AddButton(panel.transform, "Action", actionText, theme.Accent, Color.white, 18,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, ButtonBottom), new Vector2(-18, ButtonTop),
                onAction, ButtonKind.Primary);

            return new StorefrontCard
            {
                Panel = panel,
                Preview = preview,
                Name = nameText,
                Description = descriptionText,
                Note = noteText,
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
