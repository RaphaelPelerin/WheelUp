using UnityEngine;
using UnityEngine.Rendering;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Textures et matériaux d'effet fabriqués à l'exécution. Tout le projet est généré par code, sans
    /// prefabs ni assets importés : les dégradés doux dont dépendent les particules et les halos sont
    /// donc calculés au premier accès, puis partagés.
    /// </summary>
    public static class FxAssets
    {
        static Texture2D dotTexture;
        static Texture2D ringTexture;
        static Texture2D chipTexture;
        static Mesh quadMesh;
        static Sprite glowSprite;
        static Material dotMaterial;
        static Material ringMaterial;
        static Material chipMaterial;

        /// <summary>Point lumineux à bord fondu : la brique de base des étincelles.</summary>
        public static Texture2D Dot
        {
            get
            {
                if (dotTexture == null) dotTexture = Radial(128, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f));
                return dotTexture;
            }
        }

        /// <summary>Anneau fin, pour l'onde de choc de l'ouverture.</summary>
        public static Texture2D Ring
        {
            get
            {
                if (ringTexture == null)
                {
                    ringTexture = Radial(256, r =>
                    {
                        float d = (r - 0.78f) / 0.11f;
                        return Mathf.Exp(-d * d) * Mathf.Clamp01(1f - r);
                    });
                }
                return ringTexture;
            }
        }

        /// <summary>
        /// Halo pour l'UI. Il remplace les panneaux rectangulaires derrière les aperçus : un dégradé
        /// radial n'a pas de bord, donc pas de carré visible sur un fond sombre.
        /// </summary>
        public static Sprite RadialGlow
        {
            get
            {
                if (glowSprite == null)
                {
                    var texture = Radial(256, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.0f) * 0.9f);
                    glowSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                }
                return glowSprite;
            }
        }

        /// <summary>
        /// Confetti : un rectangle arrondi aux bords nets. La texture ronde et fondue des étincelles
        /// ne peut produire que des points flous ; il faut une forme franche pour qu'un grain se lise
        /// comme un morceau de papier.
        /// </summary>
        public static Texture2D Chip
        {
            get
            {
                if (chipTexture == null) chipTexture = RoundedChip(64, 0.86f, 0.54f, 0.16f);
                return chipTexture;
            }
        }

        /// <summary>Quad unitaire : rendre les confettis en mode Mesh leur donne un vrai culbutage.</summary>
        public static Mesh Quad
        {
            get
            {
                if (quadMesh == null)
                {
                    quadMesh = new Mesh { name = "FxQuad" };
                    quadMesh.vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    };
                    quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                    quadMesh.RecalculateNormals();
                    quadMesh.RecalculateBounds();
                }
                return quadMesh;
            }
        }

        public static Material ChipMaterial
        {
            get
            {
                if (chipMaterial == null) chipMaterial = BuildParticleMaterial(Chip);
                return chipMaterial;
            }
        }

        public static Material DotMaterial
        {
            get
            {
                if (dotMaterial == null) dotMaterial = BuildParticleMaterial(Dot);
                return dotMaterial;
            }
        }

        public static Material RingMaterial
        {
            get
            {
                if (ringMaterial == null) ringMaterial = BuildParticleMaterial(Ring);
                return ringMaterial;
            }
        }

        /// <summary>Rectangle arrondi plein, à peine adouci sur le bord pour éviter l'escalier.</summary>
        static Texture2D RoundedChip(int size, float width, float height, float radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "FxChip",
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float halfW = width * 0.5f - radius;
            float halfH = height * 0.5f - radius;
            float edge = 1.6f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x - half) / half) - halfW;
                    float dy = Mathf.Abs((y - half) / half) - halfH;

                    // Distance signée à un rectangle arrondi.
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;

                    float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(-edge, edge, distance));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Dégradé radial, blanc, l'alpha suivant la courbe passée (0 au centre, 1 au bord).</summary>
        static Texture2D Radial(int size, System.Func<float, float> falloff)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "FxRadial",
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float alpha = Mathf.Clamp01(falloff(Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static readonly string[] ShaderCandidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
        };

        /// <summary>
        /// Matériau de particules en fondu alpha classique, et non en additif. L'aperçu est rendu dans
        /// une RenderTexture transparente puis composé par l'UI : un mélange additif n'écrit jamais
        /// dans le canal alpha, les particules seraient donc invisibles une fois composées.
        /// L'impression de lumière vient du bloom et de la douceur des textures.
        /// </summary>
        static Material BuildParticleMaterial(Texture2D texture)
        {
            Shader shader = null;
            foreach (var name in ShaderCandidates)
            {
                shader = Shader.Find(name);
                if (shader != null) break;
            }

            var material = new Material(shader) { name = "FxParticle" };

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            // Les noms de propriétés varient d'un shader candidat à l'autre : on ne pose que ce qui existe.
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;

            return material;
        }
    }
}
