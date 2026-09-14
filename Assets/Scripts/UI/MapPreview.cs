using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Aperçu 3D d'une carte dans le carrousel du menu Jouer : une maquette de la ville qui tourne
    /// lentement sur elle-même. Même principe que l'aperçu des motos et des coffres.
    ///
    /// Les maquettes n'existent pas encore sous Resources/Maps : tant qu'elles manquent, l'aperçu se
    /// déclare vide et le carrousel affiche le nom de la carte à la place. Le carrousel, lui, est
    /// complet — il n'y aura rien à recoder le jour où les modèles arriveront.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapPreview : ModelPreviewRig
    {
        /// <summary>Rotation lente : c'est un décor qu'on observe, pas un objet qu'on inspecte.</summary>
        public float spinSpeed = 12f;

        GameObject model;
        MapInfo currentMap;

        public static MapPreview Create(RawImage output)
        {
            var go = new GameObject("MapPreview");
            var preview = go.AddComponent<MapPreview>();
            preview.SetupRig(output, AllocateRigPosition());
            return preview;
        }

        /// <summary>Affiche la maquette de la carte. Retourne false si son modèle est absent.</summary>
        public bool Show(MapInfo map)
        {
            if (currentMap == map && model != null) return true;

            currentMap = map;
            if (model != null) Destroy(model);

            // Sans avertissement : l'absence de maquette est l'état normal pour l'instant, et une
            // console pleine de warnings à chaque coup de flèche masquerait les vrais problèmes.
            model = LoadModel(map?.ModelResourcePath, warnIfMissing: false);
            if (model == null)
            {
                output.enabled = false;
                return false;
            }

            // Cadrage diorama : la maquette est large et plate, l'encadrer comme une sphère la
            // réduisait au quart de la hauteur disponible. Marge serrée — c'est la maquette qu'on
            // vient voir, et le halo derrière elle suffit à la décoller du bord.
            FrameDiorama(model, elevation: 22f, margin: 1.04f);
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
