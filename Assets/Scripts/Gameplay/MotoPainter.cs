using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Applique la peinture choisie sur un modèle de moto instancié, en teintant la couleur de base
    /// des matériaux dont le nom correspond à une zone peignable.
    /// </summary>
    public static class MotoPainter
    {
        // Le nom de la propriété de couleur dépend du shader : URP/Lit et glTFast n'utilisent pas le même.
        static readonly int[] ColorPropertyIds =
        {
            Shader.PropertyToID("_BaseColor"),
            Shader.PropertyToID("baseColorFactor"),
            Shader.PropertyToID("_Color"),
        };

        public static void Apply(GameObject model, string motoName)
        {
            if (model == null || string.IsNullOrEmpty(motoName)) return;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    var part = MotoCustomization.FindPart(CleanName(material.name));
                    if (part == null) continue;

                    SetColor(material, MotoCustomization.GetColor(motoName, part.Id));
                }
            }
        }

        /// <summary>
        /// Teinte les matériaux d'un modèle dont le nom correspond, quel que soit le shader. Sert aux
        /// icônes de récompense : la bombe de peinture prend la couleur réellement débloquée.
        /// </summary>
        public static void Tint(GameObject model, string materialName, Color color)
        {
            if (model == null || string.IsNullOrEmpty(materialName)) return;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.materials)
                {
                    if (CleanName(material.name) != materialName) continue;

                    SetColor(material, color);
                }
            }
        }

        // Unity suffixe les matériaux instanciés par " (Instance)".
        static string CleanName(string materialName)
        {
            int suffix = materialName.IndexOf(" (Instance)", System.StringComparison.Ordinal);
            return suffix >= 0 ? materialName.Substring(0, suffix) : materialName;
        }

        static void SetColor(Material material, Color color)
        {
            foreach (var propertyId in ColorPropertyIds)
            {
                if (!material.HasProperty(propertyId)) continue;

                material.SetColor(propertyId, color);
                return;
            }
        }
    }
}
