using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>Aperçu 3D d'une moto dans le garage : le modèle tourne lentement sur un plateau.</summary>
    [DisallowMultipleComponent]
    public class MotoPreview : ModelPreviewRig
    {
        // Le plateau est posé loin de l'origine pour ne jamais apparaître dans une autre caméra.

        public float rotationSpeed = 20f;

        /// <summary>Modèle actuellement affiché, pour lui appliquer une peinture (null si aucun).</summary>
        public GameObject CurrentModel => currentModel;

        GameObject currentModel;
        string currentPath;

        public static MotoPreview Create(RawImage output)
        {
            var go = new GameObject("MotoPreview");
            var preview = go.AddComponent<MotoPreview>();
            preview.SetupRig(output, AllocateRigPosition());
            return preview;
        }

        /// <summary>Affiche le modèle situé à ce chemin sous Resources. Retourne false si aucun modèle n'a pu être chargé.</summary>
        public bool Show(string resourcePath)
        {
            if (currentPath == resourcePath) return currentModel != null;

            currentPath = resourcePath;
            if (currentModel != null) Destroy(currentModel);
            currentModel = LoadModel(resourcePath);

            if (currentModel == null)
            {
                output.enabled = false;
                return false;
            }

            Frame(currentModel);
            output.enabled = true;
            return true;
        }

        protected override void OnPreviewUpdate()
        {
            if (currentModel == null) return;

            stage.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
