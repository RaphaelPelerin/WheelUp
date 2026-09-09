using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WheelingMoto.EditorTools
{
    /// <summary>
    /// Les packs importés (Demo City, MotoInteractionAnimsFREE, MotorcyclePack_) utilisent le shader
    /// Standard du Built-in Render Pipeline, incompatible avec URP (rendu rose). Ce menu sélectionne
    /// tous leurs matériaux et lance le convertisseur officiel d'Unity en un clic.
    /// </summary>
    public static class ConvertImportedMaterialsToURP
    {
        static readonly string[] Folders =
        {
            "Assets/Versatile Studio Assets",
            "Assets/MotoInteractionAnimsFREE",
            "Assets/MotorcyclePack_",
        };

        [MenuItem("Tools/Wheeling Moto/Convertir les matériaux importés en URP")]
        public static void ConvertAll()
        {
            var materials = AssetDatabase.FindAssets("t:Material", Folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(m => m != null)
                .Cast<Object>()
                .ToArray();

            if (materials.Length == 0)
            {
                Debug.LogWarning("Aucun matériau trouvé dans les dossiers importés.");
                return;
            }

            Selection.objects = materials;
            EditorApplication.ExecuteMenuItem("Edit/Rendering/Materials/Convert Selected Built-in Materials to URP");
            Debug.Log($"Wheeling Moto : {materials.Length} matériau(x) envoyé(s) à la conversion Built-in -> URP.");
        }
    }
}
