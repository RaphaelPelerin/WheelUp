using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Carrousel de cartes : une seule carte visible à la fois, en volume, et deux flèches pour la
    /// faire défiler. Presser une flèche fait sortir la carte affichée d'un côté pendant que la
    /// suivante entre de l'autre.
    ///
    /// Deux emplacements sont construits une fois pour toutes et échangent leurs rôles à chaque
    /// glissement, plutôt que d'être créés et détruits : un aperçu 3D porte une caméra et une
    /// texture de rendu, qu'on ne veut ni réallouer ni faire clignoter à chaque coup de flèche.
    ///
    /// Les flèches vivent en dehors du masque : c'est lui qui découpe le glissement, et elles
    /// disparaîtraient avec la carte sortante.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapCarousel : MonoBehaviour
    {
        /// <summary>Durée du glissement. Assez court pour rester nerveux, assez long pour qu'on voie ce qui remplace quoi.</summary>
        const float SlideDuration = 0.32f;

        /// <summary>
        /// Gabarit des flèches. Larges de 58 px pour une cible tactile confortable, mais pas plus :
        /// ce qu'elles prennent, c'est la maquette qui le perd, et c'est elle qu'on vient voir.
        /// </summary>
        const float ArrowWidth = 58f;
        const float ArrowHeight = 120f;

        /// <summary>
        /// Taille du chevron dessiné, plus petite que sa zone cliquable : le trait reste fin et
        /// élégant sans que la cible tactile ne rétrécisse avec lui.
        /// </summary>
        const float ArrowGlyphWidth = 34f;
        const float ArrowGlyphHeight = 56f;

        /// <summary>
        /// Retrait du halo par rapport aux bords du masque. Le dégradé s'éteint sur l'ellipse
        /// inscrite dans son cadre : dès que ce cadre tient dans le masque, plus aucune arête n'est
        /// coupée, et le halo redevient une tache de lumière sans contour.
        /// </summary>
        const float GlowInset = 40f;

        /// <summary>Émis dès qu'une nouvelle carte devient la carte affichée, au tout début du glissement.</summary>
        public event Action<MapInfo> Changed;

        /// <summary>Un emplacement : sa maquette 3D, son repli texte, et le cadre qui les porte.</summary>
        class Slot
        {
            public RectTransform Root;
            public MapPreview Preview;
            public TextMeshProUGUI Placeholder;
        }

        Slot front;
        Slot back;
        RectTransform viewport;

        int index;
        int direction;
        float timer;
        bool sliding;

        public MapInfo Current => MapCatalog.All[index];

        public static MapCarousel Create(Transform parent, UITheme theme, MapId start,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var root = UIFactory.CreateUIObject("MapCarousel", parent);
            UIFactory.SetRect(root, anchorMin, anchorMax, offsetMin, offsetMax);

            var carousel = root.gameObject.AddComponent<MapCarousel>();
            carousel.Build(theme, start);
            return carousel;
        }

        void Build(UITheme theme, MapId start)
        {
            index = Mathf.Max(0, Array.FindIndex(MapCatalog.All, m => m.Id == start));

            // Le masque est en retrait des flèches : la carte glisse derrière elles, pas par-dessus.
            var view = UIFactory.AddPanel(transform, "Viewport", Color.clear,
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(ArrowWidth - 6f, 0f), new Vector2(-(ArrowWidth - 6f), 0f));
            view.raycastTarget = false;
            viewport = view.rectTransform;
            viewport.gameObject.AddComponent<RectMask2D>();

            front = BuildSlot(theme, "SlotA");
            back = BuildSlot(theme, "SlotB");

            SetSlotOffset(front, 0f);
            SetSlotOffset(back, 99999f);

            BuildArrow(theme, "ArrowLeft", pointsRight: false, new Vector2(0f, 0.5f), ArrowWidth / 2f, -1);
            BuildArrow(theme, "ArrowRight", pointsRight: true, new Vector2(1f, 0.5f), -ArrowWidth / 2f, 1);

            Apply(front, Current);
        }

        Slot BuildSlot(UITheme theme, string name)
        {
            var slot = new Slot();

            slot.Root = UIFactory.CreateUIObject(name, viewport);
            UIFactory.SetRect(slot.Root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Halo en retrait, et non le halo partagé : celui-ci déborde de son parent pour adoucir
            // les cartes de boutique, alors qu'ici le masque du carrousel coupe tout ce qui dépasse.
            // Un dégradé rond tranché sur quatre côtés se lit comme un rectangle lumineux. En le
            // rentrant à l'intérieur du masque, il s'éteint de lui-même avant d'atteindre la coupe.
            UIFactory.AddGlow(slot.Root, "Glow", new Color(0.42f, 0.46f, 0.62f, 0.18f),
                Vector2.zero, Vector2.one, new Vector2(GlowInset, GlowInset), new Vector2(-GlowInset, -GlowInset));

            slot.Placeholder = UIFactory.AddText(slot.Root, "Missing", "", UITheme.FontHeading, theme.NavTextInactive,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f),
                FontStyles.Italic);

            var render = UIFactory.AddRawImage(slot.Root, "Render", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            slot.Preview = MapPreview.Create(render);

            return slot;
        }

        /// <summary>
        /// Une flèche : un chevron dessiné, posé à même le fond, sans pavé derrière lui. La zone
        /// cliquable reste plus large que le trait — c'est un rectangle transparent qui la porte,
        /// pour que le pouce n'ait pas à viser le dessin.
        /// </summary>
        void BuildArrow(UITheme theme, string name, bool pointsRight, Vector2 anchor, float centreX, int step)
        {
            var hit = UIFactory.CreateUIObject(name, transform);
            UIFactory.SetRect(hit, anchor, anchor,
                new Vector2(centreX - ArrowWidth / 2f, -ArrowHeight / 2f),
                new Vector2(centreX + ArrowWidth / 2f, ArrowHeight / 2f));

            // Image invisible mais bien cible de clic : AddPanel éteindrait le raycast sur une
            // couleur transparente, d'où l'Image posée à la main.
            var area = hit.gameObject.AddComponent<Image>();
            area.color = Color.clear;
            area.raycastTarget = true;

            var glyph = UIFactory.CreateUIObject("Chevron", hit);
            UIFactory.SetRect(glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-ArrowGlyphWidth / 2f, -ArrowGlyphHeight / 2f),
                new Vector2(ArrowGlyphWidth / 2f, ArrowGlyphHeight / 2f));

            var image = glyph.gameObject.AddComponent<Image>();
            image.sprite = FxAssets.Chevron;
            image.color = theme.Text;
            image.raycastTarget = false;

            // Une seule texture pour les deux sens : la flèche de gauche est la même, retournée.
            if (!pointsRight) glyph.localScale = new Vector3(-1f, 1f, 1f);

            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // La teinte porte sur le chevron lui-même, seule chose visible : sans fond à assombrir,
            // c'est lui qui doit réagir à l'appui.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(UITheme.Brand.r, UITheme.Brand.g, UITheme.Brand.b, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            button.colors = colors;

            button.onClick.AddListener(() => Step(step));
        }

        /// <summary>
        /// Fait défiler d'un cran. Sans effet pendant un glissement : enchaîner les appuis ferait
        /// sauter des cartes sans que le joueur les voie passer, et c'est précisément ce que le
        /// glissement sert à éviter.
        /// </summary>
        public void Step(int step)
        {
            if (sliding || MapCatalog.All.Length < 2) return;

            int count = MapCatalog.All.Length;
            index = (index + step % count + count) % count;
            direction = step > 0 ? 1 : -1;

            Apply(back, Current);

            // La carte entrante est postée du côté d'où elle doit venir, puis les deux glissent.
            float width = viewport.rect.width;
            SetSlotOffset(back, direction * width);

            sliding = true;
            timer = 0f;

            // Annoncé tout de suite : le nom et le prix sous le carrousel doivent changer avec la
            // carte, pas un tiers de seconde après elle.
            Changed?.Invoke(Current);
        }

        /// <summary>Affiche une carte sans animation. Sert au premier affichage et aux remises en cohérence.</summary>
        public void Show(MapId id)
        {
            int target = Array.FindIndex(MapCatalog.All, m => m.Id == id);
            if (target < 0 || target == index) return;

            index = target;
            sliding = false;

            Apply(front, Current);
            SetSlotOffset(front, 0f);
            SetSlotOffset(back, 99999f);

            Changed?.Invoke(Current);
        }

        void Update()
        {
            if (!sliding) return;

            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / SlideDuration);

            // Amorti aux deux bouts : le glissement démarre et s'arrête en douceur, comme une page
            // qu'on pousse, au lieu de partir et de s'arrêter net.
            float eased = t * t * (3f - 2f * t);
            float width = viewport.rect.width;

            SetSlotOffset(front, -direction * width * eased);
            SetSlotOffset(back, direction * width * (1f - eased));

            if (t < 1f) return;

            // La carte entrante devient la carte affichée ; l'autre part attendre hors champ.
            (front, back) = (back, front);
            SetSlotOffset(front, 0f);
            SetSlotOffset(back, 99999f);
            sliding = false;
        }

        void Apply(Slot slot, MapInfo map)
        {
            bool has3D = slot.Preview.Show(map);
            slot.Placeholder.gameObject.SetActive(!has3D);
            if (!has3D) slot.Placeholder.text = map.Name;
        }

        /// <summary>
        /// Décale un emplacement horizontalement sans toucher à sa taille : les deux restent étirés
        /// sur tout le masque, seul leur bord gauche et leur bord droit bougent ensemble.
        /// </summary>
        static void SetSlotOffset(Slot slot, float dx)
        {
            slot.Root.offsetMin = new Vector2(dx, 0f);
            slot.Root.offsetMax = new Vector2(dx, 0f);
        }
    }
}
