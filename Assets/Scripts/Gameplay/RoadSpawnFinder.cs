using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WheelingMoto.Gameplay
{
    /// <summary>Point de départ trouvé sur la chaussée : milieu de la route, cap le long de la ligne droite.</summary>
    public struct RoadSpawn
    {
        public Vector3 Position;
        public float Yaw;
        public float Width;
        public float StraightLength;
    }

    /// <summary>
    /// Cherche le milieu d'une route droite autour d'un point, en sondant le sol par rayons verticaux : le
    /// matériau du triangle touché dit si l'on est sur la chaussée. La ville de démo est un seul grand maillage
    /// à plusieurs matériaux (chaussée, trottoirs, places...), d'où cette lecture au triangle près.
    /// Les rues étant alignées sur les axes du monde, largeur et longueur se mesurent en X et en Z.
    /// </summary>
    public static class RoadSpawnFinder
    {
        const float GridStep = 6f;
        const float ProbeStep = 1f;
        const float MaxProbe = 50f;
        const float RayHeight = 300f;
        const int MaxCandidates = 40;
        const float MinWidth = 5f;
        const float MaxWidth = 30f;
        const float MinStraight = 30f;
        // Préférence pour un départ proche du point prévu, face à une longue ligne droite.
        const float DistancePenalty = 0.3f;

        public static bool TryFind(Vector3 seed, string roadKeyword, float radius, Collider ignore, out RoadSpawn best)
        {
            best = default;
            bool found = false;
            float bestScore = float.NegativeInfinity;
            int candidates = 0;

            // Anneaux carrés autour du point de départ : les points les plus proches sont évalués en premier.
            int rings = Mathf.CeilToInt(radius / GridStep);
            for (int ring = 0; ring <= rings && candidates < MaxCandidates; ring++)
            {
                for (int i = -ring; i <= ring && candidates < MaxCandidates; i++)
                {
                    for (int j = -ring; j <= ring && candidates < MaxCandidates; j++)
                    {
                        if (Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)) != ring) continue;

                        Vector3 point = seed + new Vector3(i * GridStep, 0f, j * GridStep);
                        if (!IsRoad(point, roadKeyword, ignore, out _)) continue;
                        candidates++;

                        if (Evaluate(point, seed, roadKeyword, ignore, out RoadSpawn spawn, out float score) && score > bestScore)
                        {
                            bestScore = score;
                            best = spawn;
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        static bool Evaluate(Vector3 point, Vector3 seed, string keyword, Collider ignore, out RoadSpawn spawn, out float score)
        {
            spawn = default;
            score = 0f;

            float east = Extent(point, Vector3.right, keyword, ignore);
            float west = Extent(point, Vector3.left, keyword, ignore);
            float north = Extent(point, Vector3.forward, keyword, ignore);
            float south = Extent(point, Vector3.back, keyword, ignore);

            bool alongZ = north + south >= east + west;
            float width = alongZ ? east + west : north + south;
            float length = alongZ ? north + south : east + west;
            // Une rue droite : assez large pour rouler, bien plus longue que large (pas un carrefour).
            if (width < MinWidth || width > MaxWidth || length < MinStraight || length < width * 2f) return false;

            // Milieu de la chaussée en travers ; en long, on garde le point sondé.
            Vector3 center = alongZ
                ? point + Vector3.right * ((east - west) * 0.5f)
                : point + Vector3.forward * ((north - south) * 0.5f);
            if (!IsRoad(center, keyword, ignore, out float groundY)) return false;
            center.y = groundY;

            // Cap vers le côté où la ligne droite est la plus longue.
            float ahead = alongZ ? Mathf.Max(north, south) : Mathf.Max(east, west);
            float yaw = alongZ ? (north >= south ? 0f : 180f) : (east >= west ? 90f : 270f);

            spawn = new RoadSpawn { Position = center, Yaw = yaw, Width = width, StraightLength = ahead };
            Vector2 offset = new Vector2(center.x - seed.x, center.z - seed.z);
            score = ahead - DistancePenalty * offset.magnitude;
            return true;
        }

        /// <summary>Distance de chaussée continue depuis le point, dans une direction.</summary>
        static float Extent(Vector3 point, Vector3 direction, string keyword, Collider ignore)
        {
            float distance = 0f;
            while (distance < MaxProbe && IsRoad(point + direction * (distance + ProbeStep), keyword, ignore, out _))
            {
                distance += ProbeStep;
            }
            return distance;
        }

        /// <summary>
        /// Premier élément visible sous le point, vu d'en haut : chaussée si son matériau porte le mot-clé.
        /// Les colliders sans rendu (collision simplifiée, sol de secours) sont traversés.
        /// </summary>
        static bool IsRoad(Vector3 point, string keyword, Collider ignore, out float groundY)
        {
            groundY = 0f;
            Vector3 origin = new Vector3(point.x, point.y + RayHeight, point.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, RayHeight * 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == ignore) continue;
                string material = MaterialAt(hit);
                if (material == null) continue;

                groundY = hit.point.y;
                return material.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        /// <summary>Nom du matériau du triangle touché ; null si le collider n'a pas de rendu associé.</summary>
        static string MaterialAt(RaycastHit hit)
        {
            if (!(hit.collider is MeshCollider meshCollider) || hit.triangleIndex < 0) return null;
            Mesh mesh = meshCollider.sharedMesh;
            var renderer = meshCollider.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null) return null;

            int index = hit.triangleIndex * 3;
            Material[] materials = renderer.sharedMaterials;
            int firstSubMesh = renderer.isPartOfStaticBatch ? renderer.subMeshStartIndex : 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                SubMeshDescriptor subMesh = mesh.GetSubMesh(s);
                if (index < subMesh.indexStart || index >= subMesh.indexStart + subMesh.indexCount) continue;

                int m = s - firstSubMesh;
                return m >= 0 && m < materials.Length && materials[m] != null ? materials[m].name : string.Empty;
            }
            return string.Empty;
        }
    }
}
