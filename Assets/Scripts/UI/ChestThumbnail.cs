using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Petit aperçu 3D d'un coffre fermé, posé dans une carte de la boutique. Il tourne lentement sur
    /// lui-même et ne joue aucune animation : l'ouverture se passe sur l'écran dédié.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestThumbnail : ModelPreviewRig
    {
        public float spinSpeed = 22f;

        GameObject model;
        ChestInfo currentChest;

        public static ChestThumbnail Create(RawImage output)
        {
            var go = new GameObject("ChestThumbnail");
            var thumbnail = go.AddComponent<ChestThumbnail>();
            thumbnail.SetupRig(output, AllocateRigPosition());
            return thumbnail;
        }

        /// <summary>Affiche le coffre fermé. Retourne false si le modèle 3D est absent.</summary>
        public bool Show(ChestInfo chest)
        {
            // Rechargé seulement si le coffre change : la boutique se rafraîchit à chaque retour d'écran.
            if (currentChest == chest && model != null) return true;

            currentChest = chest;
            if (model != null) Destroy(model);

            model = LoadModel(chest?.ClosedModelPath, warnIfMissing: false);
            if (model == null)
            {
                output.enabled = false;
                return false;
            }

            Frame(model, heightFactor: 0.28f, margin: 1.08f);
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
