using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Ajoute au chargement un MeshCollider sur les meshes du décor qui n'en ont pas.
    /// Nécessaire car la ville est instanciée depuis des prefabs "aplatis" (routes comprises)
    /// qui n'héritent pas de la génération de colliders faite à l'import des FBX.
    /// </summary>
    public class CityColliderBaker : MonoBehaviour
    {
        [Tooltip("Journalise le nombre de colliders générés.")]
        public bool logResult = true;

        void Awake()
        {
            int created = 0;
            int skippedUnreadable = 0;

            var filters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                // La moto est pilotée par un CharacterController : pas de collider sur son modèle.
                if (filter.GetComponentInParent<MotorcycleController>() != null) continue;

                if (filter.GetComponent<Collider>() != null) continue;

                if (!mesh.isReadable)
                {
                    skippedUnreadable++;
                    continue;
                }

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                created++;
            }

            if (logResult)
            {
                Debug.Log($"CityColliderBaker : {created} collider(s) generes, {skippedUnreadable} mesh(es) non lisibles ignores.");
            }
        }
    }
}
