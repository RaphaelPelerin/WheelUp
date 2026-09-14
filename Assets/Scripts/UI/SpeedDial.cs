using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Cadran du compteur de vitesse, dessiné en géométrie plutôt qu'en texture : net à toutes les résolutions,
    /// sans atlas à fabriquer. Un arc ouvert vers le bas porte la piste, les graduations et l'arc de l'aiguille, qui
    /// vire au rouge de la marque en haut du cadran. Les graduations s'allument au passage de l'aiguille.
    ///
    /// Le trait est celui de <see cref="SoftMesh"/>, partagé avec la jauge d'angle. Tout tient dans un seul maillage,
    /// reconstruit seulement quand l'aiguille bouge.
    ///
    /// Angles en degrés, comptés dans le sens horaire depuis midi ; le cadran est centré sur son rectangle.
    /// </summary>
    public class SpeedDial : MaskableGraphic
    {
        static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color FillColor = new Color(1f, 1f, 1f, 0.96f);
        static readonly Color MajorTickColor = new Color(1f, 1f, 1f, 0.34f);
        static readonly Color MinorTickColor = new Color(1f, 1f, 1f, 0.18f);

        // Part du cadran à partir de laquelle l'arc commence à virer au rouge.
        public const float HotFrom = 0.62f;
        // Pas angulaire maximal d'un segment d'arc : au-delà, la courbe se lirait en facettes.
        const float MaxStep = 2.5f;
        // Une graduation s'allume sur cette part du cadran, à l'approche de l'aiguille.
        const float LightSpan = 0.015f;
        // Écart plus petit que ce qu'un pixel laisse voir : le maillage n'est pas reconstruit.
        const float Epsilon = 0.0002f;

        const float TickGap = 7f;
        const float MajorTickLength = 12f;
        const float MinorTickLength = 6f;
        const float MajorTickWidth = 3f;
        const float MinorTickWidth = 2f;

        float radius = 124f;
        float thickness = 9f;
        float sweep = 240f;
        int intervals = 10;
        float needle;

        /// <summary>Position de l'aiguille, de 0 (butée basse) à 1 (fond du cadran).</summary>
        public float Value
        {
            get => needle;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - needle) < Epsilon) return;
                needle = value;
                SetVerticesDirty();
            }
        }

        /// <param name="outerRadius">Rayon extérieur de l'arc.</param>
        /// <param name="arcThickness">Épaisseur de l'arc.</param>
        /// <param name="arcSweep">Ouverture du cadran, en degrés, centrée sur midi.</param>
        public void Setup(float outerRadius, float arcThickness, float arcSweep)
        {
            radius = outerRadius;
            thickness = arcThickness;
            sweep = arcSweep;
            raycastTarget = false;
            SetVerticesDirty();
        }

        /// <summary>Nombre d'intervalles entre graduations principales ; une graduation secondaire marque chaque milieu.</summary>
        public int Intervals
        {
            get => intervals;
            set
            {
                value = Mathf.Max(1, value);
                if (value == intervals) return;
                intervals = value;
                SetVerticesDirty();
            }
        }

        /// <summary>Angle de l'aiguille pour une position du cadran.</summary>
        float AngleAt(float fraction) => -sweep * 0.5f + sweep * fraction;

        /// <summary>Couleur de l'arc de l'aiguille à cette position : blanc, puis rouge de la marque en haut du cadran.</summary>
        public static Color ColorAt(float fraction) =>
            Color.Lerp(FillColor, UITheme.Brand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HotFrom, 1f, fraction)));

        /// <summary>Direction d'un angle du cadran, depuis son centre.</summary>
        static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        }

        /// <summary>Sens de rotation horaire en ce point du cadran.</summary>
        static Vector2 Clockwise(Vector2 direction) => new Vector2(direction.y, -direction.x);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var mesh = new SoftMesh(this, vh);
            Vector2 center = rectTransform.rect.center;

            AddArc(mesh, center, 1f, false);
            AddTicks(mesh, center, radius - thickness - TickGap);
            // Toujours dessiné, même à l'arrêt : l'aiguille au repos se lit comme une pastille en début de cadran.
            AddArc(mesh, center, needle, true);
        }

        void AddTicks(SoftMesh mesh, Vector2 center, float tickOuter)
        {
            int count = intervals * 2;
            for (int i = 0; i <= count; i++)
            {
                float fraction = (float)i / count;
                bool major = i % 2 == 0;
                float lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fraction - LightSpan, fraction, needle));

                Color litColor = ColorAt(fraction);
                litColor.a = major ? 1f : 0.7f;
                Color color = Color.Lerp(major ? MajorTickColor : MinorTickColor, litColor, lit);

                Vector2 direction = Direction(AngleAt(fraction));
                float length = major ? MajorTickLength : MinorTickLength;
                mesh.Line(center + direction * (tickOuter - length), center + direction * tickOuter,
                    major ? MajorTickWidth : MinorTickWidth, color);
            }
        }

        /// <summary>Arc du début du cadran jusqu'à <paramref name="toFraction"/>, bouts arrondis.</summary>
        void AddArc(SoftMesh mesh, Vector2 center, float toFraction, bool lit)
        {
            float from = AngleAt(0f);
            float to = AngleAt(toFraction);
            float middle = radius - thickness * 0.5f;
            float half = thickness * 0.5f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / MaxStep));

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Color color = lit ? ColorAt(toFraction * t) : TrackColor;
                Vector2 direction = Direction(Mathf.Lerp(from, to, t));
                mesh.Row(center + direction * middle, direction, half, color, i > 0);
            }

            Vector2 start = Direction(from);
            Vector2 end = Direction(to);
            mesh.Cap(center + start * middle, start, -Clockwise(start), half, lit ? ColorAt(0f) : TrackColor);
            mesh.Cap(center + end * middle, end, Clockwise(end), half, lit ? ColorAt(toFraction) : TrackColor);
        }
    }
}
