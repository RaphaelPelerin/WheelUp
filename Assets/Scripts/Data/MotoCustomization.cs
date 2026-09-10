using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>Zone peignable d'une moto, identifiée par les préfixes de noms de matériaux du modèle 3D.</summary>
    public class MotoPaintPart
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string[] MaterialPrefixes;

        public MotoPaintPart(string id, string label, string[] materialPrefixes)
        {
            Id = id;
            Label = label;
            MaterialPrefixes = materialPrefixes;
        }

        public bool Matches(string materialName)
        {
            foreach (var prefix in MaterialPrefixes)
            {
                if (materialName.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    public class PaintColor
    {
        public readonly string Label;
        public readonly Color Color;

        /// <summary>Les couleurs non offertes se débloquent dans les coffres de la boutique.</summary>
        public readonly bool UnlockedByDefault;

        public PaintColor(string label, Color color, bool unlockedByDefault = false)
        {
            Label = label;
            Color = color;
            UnlockedByDefault = unlockedByDefault;
        }
    }

    /// <summary>
    /// Peinture choisie par le joueur, par moto et par zone, persistée dans PlayerPrefs.
    /// Les couleurs sont appliquées en teinte sur la couleur de base du matériau : les modèles importés
    /// portent leur couleur dans la texture, une teinte blanche laisse donc l'aspect d'origine.
    /// </summary>
    public static class MotoCustomization
    {
        public static readonly PaintColor[] Palette =
        {
            new PaintColor("Origine", Color.white, true),
            new PaintColor("Noir", new Color(0.10f, 0.10f, 0.12f), true),
            new PaintColor("Rouge", new Color(0.80f, 0.12f, 0.14f), true),
            new PaintColor("Bleu", new Color(0.13f, 0.35f, 0.82f), true),
            new PaintColor("Vert", new Color(0.24f, 0.72f, 0.30f)),
            new PaintColor("Orange", new Color(0.96f, 0.36f, 0.13f)),
            new PaintColor("Jaune", new Color(0.94f, 0.78f, 0.16f)),
            new PaintColor("Anthracite", new Color(0.32f, 0.34f, 0.38f)),
            new PaintColor("Violet", new Color(0.48f, 0.22f, 0.72f)),
            new PaintColor("Or", new Color(0.83f, 0.66f, 0.28f)),

            // Les indices sont la clé de sauvegarde d'une couleur : on ajoute toujours à la fin,
            // jamais au milieu, sinon les peintures déjà débloquées désignent une autre teinte.
            new PaintColor("Blanc nacré", new Color(0.93f, 0.94f, 0.96f)),
            new PaintColor("Argent", new Color(0.74f, 0.76f, 0.80f)),
            new PaintColor("Bronze", new Color(0.56f, 0.38f, 0.20f)),
            new PaintColor("Cuivre", new Color(0.76f, 0.44f, 0.24f)),
            new PaintColor("Bordeaux", new Color(0.42f, 0.09f, 0.15f)),
            new PaintColor("Bleu nuit", new Color(0.09f, 0.14f, 0.34f)),
            new PaintColor("Bleu ciel", new Color(0.42f, 0.70f, 0.92f)),
            new PaintColor("Turquoise", new Color(0.11f, 0.66f, 0.62f)),
            new PaintColor("Vert kaki", new Color(0.34f, 0.38f, 0.22f)),
            new PaintColor("Vert lime", new Color(0.68f, 0.87f, 0.18f)),
            new PaintColor("Rose", new Color(0.90f, 0.36f, 0.60f)),
            new PaintColor("Corail", new Color(0.96f, 0.45f, 0.38f)),
            new PaintColor("Sable", new Color(0.82f, 0.72f, 0.52f)),
            new PaintColor("Prune", new Color(0.34f, 0.14f, 0.36f)),
        };

        /// <summary>
        /// Zones peignables. Les préfixes correspondent aux noms de matériaux du modèle : si une zone
        /// repeint le mauvais élément sur une nouvelle moto, c'est ici qu'on ajuste.
        /// </summary>
        public static readonly MotoPaintPart[] Parts =
        {
            new MotoPaintPart("body", "Carrosserie", new[] { "gray80" }),
            new MotoPaintPart("trim", "Plastiques", new[] { "gray56", "plastics_d" }),
            new MotoPaintPart("rims", "Jantes", new[] { "gold" }),
            new MotoPaintPart("seat", "Selle", new[] { "alcantara" }),
            new MotoPaintPart("exhaust", "Échappement", new[] { "tocexhst" }),
        };

        public static bool IsColorUnlocked(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= Palette.Length) return false;
            if (Palette[colorIndex].UnlockedByDefault) return true;

            return PlayerPrefs.GetInt(UnlockKey(colorIndex), 0) == 1;
        }

        public static void UnlockColor(int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= Palette.Length) return;

            PlayerPrefs.SetInt(UnlockKey(colorIndex), 1);
        }

        /// <summary>Indices des couleurs encore verrouillées, pour le tirage des coffres.</summary>
        public static System.Collections.Generic.List<int> LockedColorIndices()
        {
            var locked = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Palette.Length; i++)
            {
                if (!IsColorUnlocked(i)) locked.Add(i);
            }
            return locked;
        }

        public static int GetColorIndex(string motoName, string partId)
        {
            int index = Mathf.Clamp(PlayerPrefs.GetInt(Key(motoName, partId), 0), 0, Palette.Length - 1);
            // Une couleur non débloquée ne doit jamais rester appliquée : retour à l'aspect d'origine.
            return IsColorUnlocked(index) ? index : 0;
        }

        public static void SetColorIndex(string motoName, string partId, int index)
        {
            PlayerPrefs.SetInt(Key(motoName, partId), Mathf.Clamp(index, 0, Palette.Length - 1));
        }

        public static Color GetColor(string motoName, string partId)
        {
            return Palette[GetColorIndex(motoName, partId)].Color;
        }

        /// <summary>Zone à laquelle appartient ce matériau, ou null s'il n'est pas peignable.</summary>
        public static MotoPaintPart FindPart(string materialName)
        {
            foreach (var part in Parts)
            {
                if (part.Matches(materialName)) return part;
            }
            return null;
        }

        static string Key(string motoName, string partId) => $"moto_paint_{motoName}_{partId}";

        static string UnlockKey(int colorIndex) => $"paint_unlocked_{colorIndex}";
    }
}
