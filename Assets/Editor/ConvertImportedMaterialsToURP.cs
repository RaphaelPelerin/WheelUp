using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace WheelingMoto.EditorTools
{
    /// <summary>
    /// Les packs importés (Demo City, MotoInteractionAnimsFREE, MotorcyclePack_) utilisent les
    /// shaders du Built-in Render Pipeline, que URP ne sait pas dessiner : leurs objets sortent en
    /// magenta. Ce menu lance le convertisseur officiel de Unity, qui ne se contente pas de changer
    /// le shader — il reporte aussi textures et réglages (_MainTex vers _BaseMap, _Glossiness vers
    /// _Smoothness, mode de fusion, mots-clés).
    ///
    /// La conversion passe par l'API et non par ExecuteMenuItem : le chemin du menu a changé d'une
    /// version d'URP à l'autre — "Convert Selected Built-in Materials to URP" est devenu
    /// "Convert Selected Built-In Materials to Current SRP" — et un chemin périmé échoue sans rien
    /// convertir, en laissant croire le contraire.
    ///
    /// Le compte rendu est établi en relisant l'état réel des matériaux après coup, pour que le
    /// journal ne puisse plus annoncer un succès qui n'a pas eu lieu.
    /// </summary>
    public static class ConvertImportedMaterialsToURP
    {
        [MenuItem("Tools/Wheeling Moto/Convertir les matériaux importés en URP")]
        public static void ConvertAll()
        {
            if (!GraphicsSettings.isScriptableRenderPipelineEnabled)
            {
                Debug.LogError("Aucun pipeline scriptable actif : il n'y a rien à convertir.");
                return;
            }

            var pipeline = GraphicsSettings.currentRenderPipelineAssetType;
            var upgraders = MaterialUpgrader.FetchAllUpgradersForPipeline(pipeline);
            if (upgraders.Count == 0)
            {
                Debug.LogError($"Aucun convertisseur de matériaux disponible pour {pipeline.Name}.");
                return;
            }

            var before = MaterialUpgrader.FetchAllUpgradableMaterialsForPipeline(pipeline);
            if (before.Count == 0)
            {
                Debug.Log("Aucun matériau Built-in à convertir : tout est déjà sur le pipeline actif.");
                return;
            }

            foreach (var material in before)
            {
                Debug.Log($"À convertir : {AssetDatabase.GetAssetPath(material)}");
            }

            MaterialUpgrader.UpgradeProjectFolder(upgraders, "Conversion des matériaux vers URP");
            AssetDatabase.SaveAssets();

            var after = MaterialUpgrader.FetchAllUpgradableMaterialsForPipeline(pipeline);
            Debug.Log($"Conversion terminée : {before.Count - after.Count} matériau(x) convertis, {after.Count} restant(s).");

            // Ce qui résiste n'a pas de convertisseur — une skybox, par exemple. Il faut le reprendre
            // à la main, et mieux vaut le voir nommé que le découvrir à l'écran.
            foreach (var material in after)
            {
                Debug.LogWarning($"Toujours en Built-in : {AssetDatabase.GetAssetPath(material)}");
            }
        }
    }
}
