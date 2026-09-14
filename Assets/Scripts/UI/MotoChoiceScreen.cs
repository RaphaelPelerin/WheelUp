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
    /// Page de choix de moto, présentée quand un palier de niveau est dû (voir
    /// <see cref="LevelRewardCatalog"/>). Trois motos équivalentes en puissance, une seule à garder.
    ///
    /// Bâtie comme <see cref="ChestOpeningScreen"/> : posée sur le canvas racine et non dans la zone
    /// sûre, pour que son fond couvre la dalle entière, découpe et coins arrondis compris. Un seul
    /// exemplaire vit par menu, réveillé à chaque palier.
    ///
    /// La page ne se referme que sur un choix. Il n'y a pas de bouton « plus tard » : le palier reste
    /// dû tant qu'il n'est pas honoré, et le proposer sans le résoudre laisserait un écran à
    /// rouvrir sans fin.
    /// </summary>
    public class MotoChoiceScreen : MonoBehaviour
    {
        const float CardWidth = 520f;
        const float CardHeight = 660f;
        const float CardGap = 40f;
        const float PreviewHeight = 260f;

        /// <summary>Hauteur d'une ligne de la fiche technique.</summary>
        const float StatRow = 44f;

        UITheme theme;
        Transform cardsRoot;
        TextMeshProUGUI titleText;
        TextMeshProUGUI subtitleText;

        readonly List<MotoPreview> previews = new List<MotoPreview>();

        Action onClosed;

        public GameObject Root { get; private set; }
        public bool IsOpen => Root != null && Root.activeSelf;

        public static MotoChoiceScreen Create(Transform canvasRoot, UITheme theme)
        {
            var host = UIFactory.CreateUIObject("MotoChoiceScreen", canvasRoot);
            UIFactory.SetRect(host, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var screen = host.gameObject.AddComponent<MotoChoiceScreen>();
            screen.Build(host, theme);
            return screen;
        }

        void Build(RectTransform host, UITheme uiTheme)
        {
            theme = uiTheme;
            Root = host.gameObject;

            // Fond opaque : la page remplace le menu le temps du choix, elle ne s'y superpose pas.
            var background = UIFactory.AddPanel(host, "Background", theme.Background,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            background.raycastTarget = true;

            titleText = UIFactory.AddText(host, "Title", "", UITheme.FontDisplay, theme.Accent,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -150f), new Vector2(-40f, -70f), FontStyles.Bold | FontStyles.Italic);

            subtitleText = UIFactory.AddText(host, "Subtitle", "", UITheme.FontBody, theme.TextMuted,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(40f, -200f), new Vector2(-40f, -152f));

            var cards = UIFactory.CreateUIObject("Cards", host);
            UIFactory.SetRect(cards, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cardsRoot = cards;

            Root.SetActive(false);
        }

        /// <summary>
        /// Montre les trois motos du palier. <paramref name="closedCallback"/> n'est appelé qu'une
        /// fois le choix fait et la moto équipée.
        /// </summary>
        public void Play(int tier, Action closedCallback)
        {
            onClosed = closedCallback;

            var motos = LevelRewardCatalog.MotosForTier(tier);
            if (motos.Count == 0)
            {
                // Palier vide : le catalogue a changé sous nos pieds. Mieux vaut consommer le palier et
                // rendre la main que laisser le joueur devant une page sans issue.
                Debug.LogWarning($"[MotoChoiceScreen] Palier {tier} sans moto connue : palier abandonné.");
                onClosed?.Invoke();
                return;
            }

            titleText.text = $"NIVEAU {LevelRewardCatalog.LevelForTier(tier)}";
            subtitleText.text = motos.Count > 1
                ? "Choisis ta moto. Les autres restent achetables au garage."
                : "Ta récompense de palier.";

            BuildCards(motos);

            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
        }

        void BuildCards(List<MotoInfo> motos)
        {
            ClearCards();

            float total = motos.Count * CardWidth + (motos.Count - 1) * CardGap;
            float left = -total * 0.5f;

            for (int i = 0; i < motos.Count; i++)
            {
                float x = left + i * (CardWidth + CardGap);
                BuildCard(motos[i], x);
            }
        }

        void BuildCard(MotoInfo moto, float x)
        {
            var card = UIFactory.AddPanel(cardsRoot, "Card_" + moto.Name, theme.Panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -CardHeight * 0.5f - 40f), new Vector2(x + CardWidth, CardHeight * 0.5f - 40f),
                rounded: true);
            card.raycastTarget = false;
            Transform root = card.transform;

            BuildPreview(root, moto);

            UIFactory.AddText(root, "Name", moto.Name, UITheme.FontHeading, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -PreviewHeight - 66f), new Vector2(-16f, -PreviewHeight - 18f),
                FontStyles.Bold);

            UIFactory.AddText(root, "Category", moto.Category.ToUpperInvariant(), UITheme.FontLabel,
                theme.Accent, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -PreviewHeight - 100f), new Vector2(-16f, -PreviewHeight - 68f));

            BuildSpecs(root, moto);

            UIFactory.AddButton(root, "Pick", "CHOISIR", theme.Accent, theme.Text, UITheme.FontBody,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 20f), new Vector2(-20f, 92f),
                () => Choose(moto), ButtonKind.Primary);
        }

        /// <summary>
        /// Aperçu 3D, ou le nom écrit si la moto n'a pas encore de modèle. Le repli affiche le nom
        /// plutôt que le modèle d'une autre moto : montrer trois fois la même machine sous trois noms
        /// différents rendrait le choix incompréhensible.
        /// </summary>
        void BuildPreview(Transform card, MotoInfo moto)
        {
            var slot = UIFactory.AddPanel(card, "PreviewSlot", theme.PanelAlt,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(16f, -PreviewHeight - 12f), new Vector2(-16f, -12f), rounded: true);
            slot.raycastTarget = false;

            if (!string.IsNullOrEmpty(moto.ModelResourcePath))
            {
                var output = UIFactory.AddRawImage(slot.transform, "Preview",
                    Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
                var preview = MotoPreview.Create(output);
                if (preview != null && preview.Show(moto.ModelResourcePath))
                {
                    previews.Add(preview);
                    return;
                }

                if (preview != null) Destroy(preview.gameObject);
                Destroy(output.gameObject);
            }

            UIFactory.AddText(slot.transform, "Missing", moto.Name, UITheme.FontBody, theme.NavTextInactive,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        }

        /// <summary>
        /// Fiche technique du choix. La puissance n'y figure pas : les trois motos d'un palier sont
        /// choisies équivalentes, l'afficher donnerait trois fois le même chiffre et ne dirait rien.
        /// Ne restent que les grandeurs qui les séparent vraiment.
        /// </summary>
        void BuildSpecs(Transform card, MotoInfo moto)
        {
            MotoSpecs s = moto.Specs;
            float top = -PreviewHeight - 118f;

            AddStat(card, "Poids", $"{Mathf.RoundToInt(s.WeightKg)} kg", ref top);
            AddStat(card, "Vitesse de pointe", $"{Mathf.RoundToInt(s.TopSpeedKmh)} km/h", ref top);
            AddStat(card, "Freinage", $"{s.BrakingMps2:0.0} m/s²", ref top);
            AddStat(card, "Maniabilité", $"{s.Handling:0.0} / {MotoStats.MaxHandling:0}", ref top);
        }

        void AddStat(Transform card, string label, string value, ref float top)
        {
            UIFactory.AddText(card, "Label_" + label, label, UITheme.FontLabel, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0.6f, 1f),
                new Vector2(24f, top - StatRow), new Vector2(0f, top));

            UIFactory.AddText(card, "Value_" + label, value, UITheme.FontLabel, theme.Text,
                TextAnchor.MiddleRight, new Vector2(0.4f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, top - StatRow), new Vector2(-24f, top), FontStyles.Bold);

            top -= StatRow;
        }

        void Choose(MotoInfo moto)
        {
            GarageOwnership.SetOwned(moto.Name);

            // Équipée sur-le-champ : sans cela le joueur repartirait sur son ancienne moto et croirait
            // que son choix n'a rien changé.
            Loadout.Select(moto);

            // Le palier n'est marqué encaissé que par l'appelant, dans le rappel de fermeture : cet
            // écran ne sait pas de quel niveau il vient, et tant que le choix n'est pas fait, fermer
            // l'application doit redonner la page au prochain démarrage.
            PlayerPrefs.Save();
            Hide();
        }

        void Hide()
        {
            ClearCards();
            Root.SetActive(false);
            onClosed?.Invoke();
        }

        /// <summary>
        /// Détruit les cartes du palier précédent, aperçus compris. Les rigs d'aperçu libèrent leur
        /// texture de rendu dans leur propre OnDestroy : les laisser vivre coûterait une caméra et une
        /// cible de rendu par moto, à chaque palier.
        /// </summary>
        void ClearCards()
        {
            foreach (var preview in previews)
            {
                if (preview != null) Destroy(preview.gameObject);
            }
            previews.Clear();

            if (cardsRoot == null) return;
            for (int i = cardsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = cardsRoot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        void OnDestroy() => ClearCards();
    }
}
