using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Bandeau de récompense en tête d'une page de progression : l'aperçu 3D du coffre et son bouton
    /// dans la colonne de droite, le texte et la jauge à gauche.
    /// </summary>
    public class ProgressStrip
    {
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Detail;
        public Image Fill;
        public Button Action;
        public TextMeshProUGUI ActionLabel;

        public ChestThumbnail Thumbnail;
        public GameObject ThumbnailPlaceholder;

        /// <summary>Montre le coffre promis, et retombe sur son nom écrit si le modèle 3D manque.</summary>
        public void ShowChest(ChestInfo chest)
        {
            if (Thumbnail == null) return;

            bool has3D = Thumbnail.Show(chest);
            ThumbnailPlaceholder.SetActive(!has3D);
            if (!has3D) ThumbnailPlaceholder.GetComponent<TextMeshProUGUI>().text = chest != null ? chest.Name : "Coffre";
        }
    }

    /// <summary>
    /// Une ligne de progression : un titre, une précision, une jauge, un chiffrage, et un bouton
    /// dans la colonne de droite. Mission du jour et palier de succès s'y rangent de la même façon —
    /// c'est la même chose vue à deux échéances différentes.
    /// </summary>
    public class ProgressRow
    {
        public GameObject Root;
        public TextMeshProUGUI Title;
        public TextMeshProUGUI Detail;
        public TextMeshProUGUI Progress;
        public Image Fill;
        public Button Action;
        public TextMeshProUGUI ActionLabel;
    }

    /// <summary>
    /// Mise en page commune à toutes les pages de progression — missions du jour, de la semaine et
    /// succès. Elle vit ici plutôt que dans chaque écran pour la même raison que
    /// <see cref="Storefront"/> existe pour les deux vitrines : dupliquer les mesures, c'est se
    /// garantir qu'elles divergeront à la première retouche. C'était déjà arrivé — les trois pages
    /// avaient fini avec trois hauteurs de bandeau, trois tailles d'aperçu et deux gabarits de ligne.
    ///
    /// Toute page suit le même plan : le bandeau de récompense en tête, puis la liste défilante.
    /// Rien d'autre. Une page à trois lignes et une page à onze se ressemblent donc trait pour trait.
    /// </summary>
    public static class ProgressBoard
    {
        /// <summary>
        /// Largeur de la colonne des récompenses, le long du bord droit. En pixels et non en
        /// fraction : le bandeau et les lignes doivent tomber sur la même verticale, et deux
        /// ancrages différents dériveraient l'un par rapport à l'autre selon le format de fenêtre.
        /// </summary>
        public const float RewardColumnWidth = 360f;

        /// <summary>Marge intérieure de la colonne, de chaque côté.</summary>
        public const float RewardColumnInset = 22f;

        public const float ButtonHeight = 66f;
        public const float ChestPreviewSize = 110f;

        /// <summary>Hauteur du bandeau : l'aperçu du coffre empilé sur son bouton, marges comprises.</summary>
        public const float StripHeight = 16f + ButtonHeight + 10f + ChestPreviewSize + 16f;

        /// <summary>Respiration entre le bandeau et la liste.</summary>
        public const float StripGap = 14f;

        /// <summary>Hauteur d'une ligne : titre, précision, jauge et chiffrage empilés.</summary>
        public const float RowHeight = 124f;
        public const float RowGap = 12f;

        const float TextLeft = 22f;

        // ---------------------------------------------------------------- bandeau

        /// <summary>Construit le bandeau de récompense, calé en haut de la page.</summary>
        public static ProgressStrip BuildStrip(Transform page, UITheme theme, UnityAction onAction)
        {
            var strip = new ProgressStrip();

            var panel = UIFactory.AddPanel(page, "RewardStrip", theme.Panel,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -StripHeight), new Vector2(0f, 0f),
                rounded: true);
            panel.raycastTarget = false;

            BuildChestPreview(panel.transform, theme, strip);

            // Titre et jauge forment un bloc calé sur le milieu du bandeau : la colonne de droite est
            // plus haute qu'eux, et un texte aligné en bas laisserait un vide au-dessus.
            strip.Title = UIFactory.AddText(panel.transform, "Title", "", UITheme.FontBody, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(TextLeft, 18f), new Vector2(-RewardColumnWidth, 62f), FontStyles.Bold);

            var track = UIFactory.AddPanel(panel.transform, "Track", theme.PanelAlt,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(TextLeft, -8f), new Vector2(-RewardColumnWidth, 4f), rounded: true);
            track.raycastTarget = false;

            strip.Fill = AddFill(track.transform, theme);

            strip.Detail = UIFactory.AddText(panel.transform, "Detail", "", UITheme.FontLabel, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(TextLeft, -50f), new Vector2(-RewardColumnWidth, -16f));

            // Le bouton se cale sous l'aperçu, dans la même colonne : le coffre se voit, et le geste
            // pour l'ouvrir est juste en dessous.
            strip.Action = UIFactory.AddButton(panel.transform, "Claim", "COFFRE", theme.PanelAlt, theme.Text,
                UITheme.FontLabel, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-RewardColumnWidth + RewardColumnInset, 16f),
                new Vector2(-RewardColumnInset, 16f + ButtonHeight), onAction);

            strip.ActionLabel = strip.Action.GetComponentInChildren<TextMeshProUGUI>();
            return strip;
        }

        /// <summary>Carré d'aperçu en haut de la colonne de droite : halo, repli texte, puis le rendu 3D par-dessus.</summary>
        static void BuildChestPreview(Transform strip, UITheme theme, ProgressStrip widget)
        {
            float bottom = 16f + ButtonHeight + 10f;

            // Carré centré dans la colonne : le rendu suit le cadre qu'on lui donne, et un cadre
            // large et bas écraserait le coffre au lieu de le laisser tenir debout.
            float centre = -RewardColumnWidth / 2f;

            var slot = UIFactory.CreateUIObject("Preview", strip);
            UIFactory.SetRect(slot, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(centre - ChestPreviewSize / 2f, bottom),
                new Vector2(centre + ChestPreviewSize / 2f, bottom + ChestPreviewSize));

            Storefront.AddPreviewGlow(slot, new Color(0.42f, 0.46f, 0.62f, 0.20f));

            widget.ThumbnailPlaceholder = UIFactory.AddText(slot, "Missing", "Coffre", UITheme.FontLabel,
                theme.NavTextInactive, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                new Vector2(6f, 6f), new Vector2(-6f, -6f), FontStyles.Italic).gameObject;

            var render = UIFactory.AddRawImage(slot, "Render", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            widget.Thumbnail = ChestThumbnail.Create(render);
        }

        // ---------------------------------------------------------------- liste

        /// <summary>
        /// Zone défilante occupant tout ce qui reste sous le bandeau. Elle est posée même quand les
        /// lignes tiennent sans défiler : c'est ce qui fait qu'une page de trois missions et une page
        /// de onze succès se comportent pareil, et qu'ajouter une ligne ne casse rien.
        /// </summary>
        public static RectTransform BuildScroll(Transform page, string name, int rowCount)
        {
            return UIFactory.AddScrollView(page, name,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, new Vector2(0f, -(StripHeight + StripGap)),
                rowCount * (RowHeight + RowGap));
        }

        /// <summary>Construit la ligne d'indice donné dans le contenu défilant.</summary>
        public static ProgressRow BuildRow(Transform content, UITheme theme, string name, int index, UnityAction onAction)
        {
            var row = new ProgressRow();
            float y = -index * (RowHeight + RowGap);

            var panel = UIFactory.AddPanel(content, name, theme.Panel,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - RowHeight), new Vector2(0f, y),
                rounded: true);
            panel.raycastTarget = false;
            row.Root = panel.gameObject;

            // Quatre bandes empilées dans les 124 px de la carte : titre, précision, jauge, chiffrage.
            // Les bornes sont posées à la main plutôt qu'en pourcentage — la jauge doit garder son
            // épaisseur de 12 px quelle que soit la hauteur de la ligne.
            row.Title = UIFactory.AddText(panel.transform, "Title", "", UITheme.FontBody, theme.Text,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(TextLeft, 88f), new Vector2(-RewardColumnWidth, -8f), FontStyles.Bold);

            row.Detail = UIFactory.AddText(panel.transform, "Detail", "", UITheme.FontLabel, theme.TextMuted,
                TextAnchor.LowerLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(TextLeft, 62f), new Vector2(-RewardColumnWidth, 88f));

            var track = UIFactory.AddPanel(panel.transform, "Track", theme.PanelAlt,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(TextLeft, 40f), new Vector2(-RewardColumnWidth, 52f), rounded: true);
            track.raycastTarget = false;

            row.Fill = AddFill(track.transform, theme);

            row.Progress = UIFactory.AddText(panel.transform, "Progress", "", UITheme.FontLabel, theme.TextMuted,
                TextAnchor.UpperLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(TextLeft, 6f), new Vector2(-RewardColumnWidth, 34f));

            // Colonne de droite, alignée sur celle du bandeau. Le même rectangle sert à tous les
            // états — à faire, à récupérer, encaissé — pour que la colonne ne bouge jamais.
            row.Action = UIFactory.AddButton(panel.transform, "Action", "", theme.PanelAlt, theme.Coin,
                UITheme.FontLabel, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-RewardColumnWidth + RewardColumnInset, -ButtonHeight / 2f),
                new Vector2(-RewardColumnInset, ButtonHeight / 2f), onAction);

            // Le texte est posé à la main : AddButton ne crée le sien que si on lui passe un libellé
            // non vide, et celui-ci change à chaque rafraîchissement.
            row.ActionLabel = UIFactory.AddText(row.Action.transform, "Label", "", UITheme.FontLabel, theme.Coin,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FontStyles.Bold);

            return row;
        }

        static Image AddFill(Transform track, UITheme theme)
        {
            var fill = UIFactory.AddPanel(track, "Fill", theme.Accent,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, rounded: true);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return fill;
        }
    }
}
