using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Pinceau des instruments du HUD (cadran du compteur, jauge d'angle) : des traits dessinés en géométrie, bords
    /// fondus sur un pixel d'écran. Un canvas d'interface n'est pas anticrénelé, et sans ce fondu un trait aussi fin
    /// crénèlerait au moindre mouvement. Les instruments partagent ainsi exactement le même trait.
    ///
    /// Se crée au début de OnPopulateMesh et ne se garde pas : il lit l'échelle du canvas et la teinte du Graphic
    /// du moment.
    /// </summary>
    internal readonly struct SoftMesh
    {
        // Largeur du fondu, en pixels d'écran.
        const float FeatherPixels = 1.1f;
        const int CapSegments = 10;

        readonly VertexHelper vh;
        readonly Color tint;

        /// <summary>Largeur du fondu, en unités de canvas.</summary>
        public readonly float Feather;

        public SoftMesh(Graphic graphic, VertexHelper vertexHelper)
        {
            vh = vertexHelper;
            tint = graphic.color;
            // Le facteur d'échelle n'est connu qu'une fois le canvas mis en page : il est relu à chaque reconstruction.
            Canvas canvas = graphic.canvas;
            Feather = FeatherPixels / Mathf.Max(0.01f, canvas != null ? canvas.scaleFactor : 1f);
        }

        /// <summary>
        /// Rangée d'une bande : quatre sommets en travers de <paramref name="middle"/>, du fondu d'un bord à celui de
        /// l'autre. Reliée à la rangée précédente si <paramref name="connect"/> : une suite de rangées trace une bande
        /// droite ou courbe. Deux rangées au même endroit, de couleurs différentes, font un changement de teinte net.
        /// </summary>
        public void Row(Vector2 middle, Vector2 across, float half, Color color, bool connect)
        {
            int row = vh.currentVertCount;
            Vertex(middle - across * (half + Feather), color, 0f);
            Vertex(middle - across * half, color, 1f);
            Vertex(middle + across * half, color, 1f);
            Vertex(middle + across * (half + Feather), color, 0f);
            if (!connect) return;

            int previous = row - 4;
            for (int k = 0; k < 3; k++)
            {
                vh.AddTriangle(previous + k, previous + k + 1, row + k + 1);
                vh.AddTriangle(row + k + 1, row + k, previous + k);
            }
        }

        /// <summary>
        /// Bout arrondi : un demi-disque posé sur la rangée <paramref name="middle"/>, bombé vers
        /// <paramref name="outward"/>. Son diamètre et son fondu tombent pile sur ceux de la bande : la jointure ne
        /// se voit pas.
        /// </summary>
        public void Cap(Vector2 middle, Vector2 across, Vector2 outward, float half, Color color)
        {
            int hub = vh.currentVertCount;
            Vertex(middle, color, 1f);
            for (int i = 0; i <= CapSegments; i++)
            {
                float phi = Mathf.PI * i / CapSegments;
                Vector2 offset = across * Mathf.Cos(phi) + outward * Mathf.Sin(phi);
                Vertex(middle + offset * half, color, 1f);
                Vertex(middle + offset * (half + Feather), color, 0f);
                if (i == 0) continue;

                int edge = hub + 1 + i * 2;
                int previous = edge - 2;
                vh.AddTriangle(hub, previous, edge);
                vh.AddTriangle(previous, previous + 1, edge + 1);
                vh.AddTriangle(edge + 1, edge, previous);
            }
        }

        /// <summary>Trait droit à bouts arrondis, de <paramref name="from"/> à <paramref name="to"/>.</summary>
        public void Capsule(Vector2 from, Vector2 to, float width, Color color)
        {
            Axes(from, to, out Vector2 along, out Vector2 across);
            float half = width * 0.5f;
            Row(from, across, half, color, false);
            Row(to, across, half, color, true);
            Cap(from, across, -along, half, color);
            Cap(to, across, along, half, color);
        }

        /// <summary>Trait droit à bouts carrés, fondu sur ses quatre bords (grille de 4 × 4 sommets) : graduations.</summary>
        public void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            Axes(from, to, out Vector2 along, out Vector2 across);
            float half = width * 0.5f;
            int start = vh.currentVertCount;

            for (int r = 0; r < 4; r++)
            {
                Vector2 middle = r == 0 ? from - along * Feather : r == 1 ? from : r == 2 ? to : to + along * Feather;
                float alpha = r == 0 || r == 3 ? 0f : 1f;
                Vertex(middle - across * (half + Feather), color, 0f);
                Vertex(middle - across * half, color, alpha);
                Vertex(middle + across * half, color, alpha);
                Vertex(middle + across * (half + Feather), color, 0f);
            }

            for (int r = 0; r < 3; r++)
            {
                for (int a = 0; a < 3; a++)
                {
                    int i = start + r * 4 + a;
                    vh.AddTriangle(i, i + 1, i + 5);
                    vh.AddTriangle(i + 5, i + 4, i);
                }
            }
        }

        static void Axes(Vector2 from, Vector2 to, out Vector2 along, out Vector2 across)
        {
            along = to - from;
            float length = along.magnitude;
            along = length > 0.0001f ? along / length : Vector2.up;
            across = new Vector2(along.y, -along.x);
        }

        void Vertex(Vector2 position, Color color, float alpha)
        {
            Color final = color * tint;
            final.a *= alpha;
            vh.AddVert(position, final, Vector2.zero);
        }
    }
}
