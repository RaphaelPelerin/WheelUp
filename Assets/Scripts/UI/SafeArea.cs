using System;
using UnityEngine;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Aligne un RectTransform sur la zone sûre de l'écran (Screen.safeArea) : sur iPhone, la barre
    /// de la Dynamic Island / l'encoche mangent un côté en paysage, l'indicateur d'accueil mange le
    /// bas, et les quatre coins de la dalle sont arrondis. Tout ce qui doit rester lisible et
    /// cliquable se place à l'intérieur de ce conteneur ; les aplats de fond, eux, restent dehors
    /// pour continuer à couvrir l'écran d'un bord à l'autre.
    ///
    /// La zone sûre du système s'arrête au rectangle inscrit : elle écarte l'îlot et l'indicateur,
    /// mais pas les arrondis, dont l'arc rogne encore les extrémités des bords. D'où
    /// <see cref="CornerInset"/>, une marge supplémentaire appliquée sur les quatre côtés.
    ///
    /// Les ancres étant exprimées en fractions du parent, celui-ci doit couvrir tout l'écran :
    /// la racine du canvas, ou un panneau étiré d'un bord à l'autre.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        /// <summary>
        /// Retrait par défaut, en unités de canvas (référence 1920x1080), soit une douzaine de points
        /// sur un iPhone en paysage : de quoi décoller le contenu de l'arc des coins arrondis sans
        /// creuser une marge visible sur les écrans rectangulaires.
        /// </summary>
        public const float DefaultCornerInset = 24f;

        [SerializeField] float cornerInset = DefaultCornerInset;

        /// <summary>Émis à chaque recalcul : rotation de l'appareil, changement de résolution, premier cadre.</summary>
        public event Action Changed;

        RectTransform rect;
        RectTransform parentRect;

        Rect appliedSafeArea;
        Vector2Int appliedResolution;
        Vector2 appliedParentSize;
        float appliedCornerInset;

        public float CornerInset
        {
            get => cornerInset;
            set
            {
                cornerInset = value;
                Apply(true);
            }
        }

        /// <summary>
        /// Marges effectivement retirées, en unités de canvas : x=gauche, y=bas, z=droite, w=haut.
        /// Sert aux fonds qui doivent déborder jusqu'au bord physique tout en s'arrêtant pile sur un
        /// élément placé, lui, dans la zone sûre.
        /// </summary>
        public Vector4 Insets
        {
            get
            {
                if (rect == null) return Vector4.zero;

                var size = parentRect != null ? parentRect.rect.size : new Vector2(Screen.width, Screen.height);
                return new Vector4(
                    size.x * rect.anchorMin.x + appliedCornerInset,
                    size.y * rect.anchorMin.y + appliedCornerInset,
                    size.x * (1f - rect.anchorMax.x) + appliedCornerInset,
                    size.y * (1f - rect.anchorMax.y) + appliedCornerInset);
            }
        }

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            parentRect = rect.parent as RectTransform;
            Apply(true);
        }

        void OnEnable() => Apply(true);

        // La zone sûre change en cours de partie : l'iPhone bascule de paysage gauche à paysage droit
        // et l'îlot passe d'un bord à l'autre. La comparaison est bien plus économique qu'un
        // repositionnement, donc le sondage par cadre ne coûte rien.
        void LateUpdate() => Apply(false);

        void Apply(bool force)
        {
            if (rect == null) return;

            var safe = Screen.safeArea;
            var resolution = new Vector2Int(Screen.width, Screen.height);
            // La taille du canvas n'est connue qu'après sa première mise à jour : la suivre ici évite
            // de publier des marges calculées sur un rectangle encore vide au premier cadre.
            var parentSize = parentRect != null ? parentRect.rect.size : Vector2.zero;

            if (!force && safe == appliedSafeArea && resolution == appliedResolution && parentSize == appliedParentSize) return;
            if (resolution.x <= 0 || resolution.y <= 0) return;

            appliedSafeArea = safe;
            appliedResolution = resolution;
            appliedParentSize = parentSize;

            var min = new Vector2(safe.xMin / resolution.x, safe.yMin / resolution.y);
            var max = new Vector2(safe.xMax / resolution.x, safe.yMax / resolution.y);

            // Garde-fou : certaines plateformes renvoient une zone sûre vide ou aberrante avant que
            // l'écran ne soit prêt. Replier sur le plein cadre vaut mieux qu'un menu réduit à rien.
            if (max.x - min.x <= 0f || max.y - min.y <= 0f || min.x < 0f || min.y < 0f || max.x > 1f || max.y > 1f)
            {
                min = Vector2.zero;
                max = Vector2.one;
            }

            // Le retrait des coins ne vaut que pour une dalle découpée. Un écran franchement
            // rectangulaire — l'éditeur, un ordinateur, la plupart des Android — renvoie une zone sûre
            // égale à l'écran : lui imposer la marge creuserait un liseré vide sans rien protéger.
            bool rounded = min != Vector2.zero || max != Vector2.one;
            appliedCornerInset = rounded ? cornerInset : 0f;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(appliedCornerInset, appliedCornerInset);
            rect.offsetMax = new Vector2(-appliedCornerInset, -appliedCornerInset);

            Changed?.Invoke();
        }
    }
}
