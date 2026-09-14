using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Ruban d'épaisseur constante dessiné le long d'une ligne brisée : le bitume de la route des
    /// paliers, la portion déjà parcourue, et la ligne médiane discontinue.
    ///
    /// uGUI ne sait pas tracer de courbe — il n'affiche que des quadrilatères texturés. Empiler des
    /// petits rectangles pivotés le long du tracé aurait donné une centaine d'objets par route et
    /// des coudes visibles à chaque jointure ; on génère donc le maillage directement, ce qui tient
    /// la route entière en un seul objet et une seule passe de rendu.
    ///
    /// Le tracé arrive déjà échantillonné (voir <see cref="SetPath"/>) : cette classe ne connaît pas
    /// la forme de la route, seulement comment l'épaissir. C'est <see cref="LevelPopup"/> qui décide
    /// où elle serpente.
    /// </summary>
    // Le CanvasRenderer se déclare ici et non plus haut : Graphic ne réclame que le RectTransform,
    // et laisse à chaque graphique concret le soin d'exiger son renderer — Image et RawImage le font
    // exactement ainsi. Sans cette ligne l'objet naît sans renderer, et le masque de la zone
    // défilante tombe dessus en voulant y poser son rectangle de découpe.
    [RequireComponent(typeof(CanvasRenderer))]
    [DisallowMultipleComponent]
    public class RoadRibbon : MaskableGraphic
    {
        /// <summary>Largeur du ruban, en unités de Canvas.</summary>
        public float Thickness = 30f;

        /// <summary>
        /// Longueur d'un tiret et de l'espace qui le suit, pour la ligne médiane. Tiret à zéro :
        /// le ruban est plein.
        /// </summary>
        public float DashLength;
        public float GapLength;

        readonly List<Vector2> path = new List<Vector2>();

        /// <summary>
        /// Remplace le tracé. Les points sont exprimés dans le repère local du rectangle, origine au
        /// centre. Moins de deux points : rien n'est dessiné.
        /// </summary>
        public void SetPath(IList<Vector2> points)
        {
            path.Clear();
            if (points != null) path.AddRange(points);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (path.Count < 2) return;

            // Les tirets découpent le tracé en tronçons indépendants : chacun est émis comme un ruban
            // à lui, sinon un tiret serait relié au suivant par-dessus l'espace qui les sépare.
            var run = new List<Vector2>();
            float travelled = 0f;
            float period = DashLength + GapLength;
            bool dashed = DashLength > 0f && period > 0f;

            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0) travelled += Vector2.Distance(path[i - 1], path[i]);

                bool visible = !dashed || travelled % period < DashLength;
                if (visible)
                {
                    run.Add(path[i]);
                    continue;
                }

                EmitRun(vh, run);
                run.Clear();
            }

            EmitRun(vh, run);
        }

        /// <summary>
        /// Épaissit un tronçon : deux sommets par point, décalés de part et d'autre de la normale au
        /// tracé. La normale se prend sur la direction moyenne des deux segments voisins — prise sur
        /// un seul, l'extérieur des virages s'ouvrirait en éventail.
        /// </summary>
        void EmitRun(VertexHelper vh, List<Vector2> run)
        {
            if (run.Count < 2) return;

            int start = vh.currentVertCount;
            float half = Thickness * 0.5f;

            for (int i = 0; i < run.Count; i++)
            {
                Vector2 direction;
                if (i == 0) direction = run[1] - run[0];
                else if (i == run.Count - 1) direction = run[i] - run[i - 1];
                else direction = (run[i + 1] - run[i - 1]);

                if (direction.sqrMagnitude < 1e-6f) direction = Vector2.right;
                direction.Normalize();

                var normal = new Vector2(-direction.y, direction.x) * half;

                vh.AddVert(run[i] + normal, color, Vector2.zero);
                vh.AddVert(run[i] - normal, color, Vector2.zero);
            }

            for (int i = 0; i < run.Count - 1; i++)
            {
                int v = start + i * 2;
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }
    }
}
