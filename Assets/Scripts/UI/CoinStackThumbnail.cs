using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Visuel 3D d'un lot de pièces, posé dans une carte de la boutique et tournant lentement sur
    /// lui-même. C'est le pendant de <see cref="ChestThumbnail"/> pour les lots : chaque lot a son
    /// propre modèle, et la taille de la carte règle la place qu'il occupe.
    ///
    /// Le modèle n'est pas teinté à la couleur du lot : des pièces sont dorées, et une pile argentée
    /// se lirait comme une autre monnaie. La couleur du lot passe par le halo posé derrière.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoinStackThumbnail : ModelPreviewRig
    {
        /// <summary>
        /// Pile générique, héritée des lots de coffre. Elle ne sert plus que de repli, le temps qu'un
        /// lot reçoive son propre modèle : mieux vaut la bonne pile au mauvais format qu'une carte
        /// vide.
        /// </summary>
        public const string FallbackModelPath = "Rewards/coins";

        public float spinSpeed = 26f;

        GameObject model;

        public static CoinStackThumbnail Create(RawImage output)
        {
            var go = new GameObject("CoinStackThumbnail");
            var thumbnail = go.AddComponent<CoinStackThumbnail>();
            thumbnail.SetupRig(output, AllocateRigPosition());
            return thumbnail;
        }

        /// <summary>
        /// Charge le modèle du lot et le cadre. <paramref name="heightFactor"/> règle la place qu'il
        /// prend dans sa carte : la carte héros lui en donne plus que les deux petites.
        ///
        /// Un lot dont le modèle manque encore retombe sur la pile générique plutôt que de laisser un
        /// trou. Retourne false seulement si même elle est absente — l'appelant garde alors son repli
        /// écrit, le total du lot en gros chiffres.
        /// </summary>
        public bool Show(string resourcePath, float heightFactor)
        {
            if (model == null)
            {
                if (!string.IsNullOrEmpty(resourcePath)) model = LoadModel(resourcePath, warnIfMissing: false);
                if (model == null) model = LoadModel(FallbackModelPath, warnIfMissing: false);

                if (model == null)
                {
                    output.enabled = false;
                    return false;
                }
            }

            Frame(model, heightFactor: heightFactor, margin: 1.1f);
            output.enabled = true;
            return true;
        }

        protected override void OnPreviewUpdate()
        {
            if (model == null) return;

            stage.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
